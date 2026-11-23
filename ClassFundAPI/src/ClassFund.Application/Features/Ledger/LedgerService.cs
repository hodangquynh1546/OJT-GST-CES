using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Attachments;
using ClassFund.Application.Features.Categories;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Ledger;

/// <summary>
/// Sổ thu chi theo phạm vi (FR-26 → FR-29). Chỉ ghi thêm: khoản chi ghi tay và bút toán đảo;
/// thu sinh từ thanh toán đã xác nhận (M6, BR-39). Học sinh chỉ đọc (OQ-14).
/// </summary>
public interface ILedgerService
{
    /// <summary>
    /// Lịch sử của một phạm vi + KPI. Học sinh chỉ thấy khoản chi / bút toán đảo (không thấy ai đã đóng, OQ-14);
    /// sổ sự kiện chỉ người đã nhận tham gia mới xem được (không thì 404, như AZ-05).
    /// </summary>
    Task<LedgerListResult> ListAsync(Guid fundId, LedgerFilter filter, CancellationToken cancellationToken);

    Task<LedgerEntryDetailDto> GetAsync(Guid fundId, Guid entryId, CancellationToken cancellationToken);

    /// <summary>Ghi khoản chi: phạm vi mở, danh mục chi đang dùng, chứng từ khi &gt; 500.000đ, chi ≤ số dư phạm vi (BR-38).</summary>
    Task<LedgerEntryDetailDto> CreateExpenseAsync(Guid fundId, CreateExpenseInput input, CancellationToken cancellationToken);

    /// <summary>Bút toán đảo cùng phạm vi, ngược loại, cùng số tiền; lý do bắt buộc; một dòng chỉ đảo một lần (BR-08).</summary>
    Task<LedgerEntryDetailDto> ReverseAsync(Guid fundId, Guid entryId, string reason, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class LedgerService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    ICategoriesService categories,
    IAttachmentsService attachments,
    AuditTrail audit,
    TimeProvider clock) : ILedgerService
{
    public const int ReversalTitleMaxLength = 150;
    public const string ReversalTitlePrefix = "Đảo: ";

    private static readonly IReadOnlySet<AttachmentKind> EvidenceKinds = new HashSet<AttachmentKind> { AttachmentKind.Receipt, AttachmentKind.Document };

    public async Task<LedgerListResult> ListAsync(Guid fundId, LedgerFilter filter, CancellationToken cancellationToken)
    {
        var (viewerId, access) = await ViewerAsync(fundId, cancellationToken);
        var manager = CanManage(access);
        if (filter.EventId is { } eventId)
        {
            await EnsureCanViewEventAsync(fundId, eventId, manager, cancellationToken);
        }

        var entries = Visible(LedgerBalances.InScope(db, fundId, filter.EventId), manager, viewerId);
        if (filter.Type is { } type)
        {
            entries = entries.Where(entry => entry.Type == type);
        }

        if (filter.CategoryId is { } categoryId)
        {
            entries = entries.Where(entry => entry.CategoryId == categoryId);
        }

        if (filter.From is { } from)
        {
            entries = entries.Where(entry => entry.TransactionDate >= from);
        }

        if (filter.To is { } to)
        {
            entries = entries.Where(entry => entry.TransactionDate <= to);
        }

        var total = await db.CountAsync(entries, cancellationToken);
        var page = entries
            .OrderByDescending(entry => entry.TransactionDate)
            .ThenByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize);

        var items = await ProjectAsync(fundId, page, cancellationToken);
        var allTime = await LedgerBalances.ForScopeAsync(db, fundId, filter.EventId, cancellationToken);
        var period = filter.From is null && filter.To is null
            ? allTime
            : await LedgerBalances.ForScopeAsync(db, fundId, filter.EventId, cancellationToken, filter.From, filter.To);

        return new LedgerListResult(items, total, filter.Page, filter.PageSize, new LedgerSummaryDto(allTime.Balance, period.TotalIncome, period.TotalExpense));
    }

    public async Task<LedgerEntryDetailDto> GetAsync(Guid fundId, Guid entryId, CancellationToken cancellationToken)
    {
        var (viewerId, access) = await ViewerAsync(fundId, cancellationToken);
        var manager = CanManage(access);
        var entry = await db.FirstOrDefaultAsync(
            db.Query<LedgerEntry>().Where(item => item.Id == entryId && item.FundId == fundId),
            cancellationToken) ?? throw new NotFoundException();

        if (!manager && !await db.AnyAsync(Visible(db.Query<LedgerEntry>().Where(item => item.Id == entry.Id), false, viewerId), cancellationToken))
        {
            throw new NotFoundException();
        }

        if (entry.EventId is { } eventId)
        {
            await EnsureCanViewEventAsync(fundId, eventId, manager, cancellationToken);
        }

        return await DetailAsync(fundId, entry, access, cancellationToken);
    }

    public async Task<LedgerEntryDetailDto> CreateExpenseAsync(Guid fundId, CreateExpenseInput input, CancellationToken cancellationToken)
    {
        var (userId, access) = await ManagerAsync(fundId, cancellationToken);
        MoneyRule.EnsureValid(input.Amount);
        EnsureNotFuture(input.TransactionDate);

        var title = Clean(input.Title)!;
        var purpose = Clean(input.Purpose);
        var note = Clean(input.Note);

        var entry = await db.InTransactionAsync(
            async token =>
            {
                // Khóa theo phạm vi (BR-38): hai khoản chi song song của cùng phạm vi phải chờ nhau rồi mới tính số dư.
                var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                YearEndRule.EnsureFundActive(fund.Status);

                string? eventName = null;
                if (input.EventId is { } eventId)
                {
                    var fundEvent = await db.LockAsync<Event>(eventId, token);
                    if (fundEvent is null || fundEvent.FundId != fundId)
                    {
                        throw new NotFoundException();
                    }

                    if (!EventStatusRules.AcceptsExpenses(fundEvent.Status))
                    {
                        // Còn mở: chưa đóng danh sách nên chưa được ghi chi; đã quyết toán / hủy: sổ đã khóa.
                        throw fundEvent.Status == EventStatus.Open
                            ? new BusinessRuleException(EventStatusRules.ListNotClosedCode, EventStatusRules.ListNotClosedMessage)
                            : new BusinessRuleException(LedgerErrors.EventClosed, LedgerErrors.EventClosedMessage);
                    }

                    eventName = fundEvent.Name;
                }

                await categories.EnsureUsableAsync(fundId, input.CategoryId, LedgerType.Expense, token);

                var balance = await LedgerBalances.ForScopeAsync(db, fundId, input.EventId, token);
                SpendingRule.EnsureWithinBalance(input.Amount, balance.Balance);

                var created = new LedgerEntry(
                    fundId,
                    input.EventId,
                    LedgerType.Expense,
                    LedgerSource.Manual,
                    input.CategoryId,
                    title,
                    purpose,
                    input.Amount,
                    input.TransactionDate,
                    userId,
                    clock.GetUtcNow())
                {
                    Note = note,
                };

                var attached = await attachments.AttachToAsync(fundId, input.AttachmentIds, created.Id, EvidenceKinds, token);
                EvidenceRule.EnsureProvided(input.Amount, attached > 0);

                db.Add(created);
                var categoryName = await CategoryNameAsync(input.CategoryId, token);
                audit.Record(new AuditEntry(
                    LedgerAuditActions.Create,
                    fundId,
                    userId,
                    input.EventId is null ? FinancialScope.General : FinancialScope.Event,
                    EventId: input.EventId,
                    EntityType: nameof(LedgerEntry),
                    EntityId: created.Id,
                    After: new
                    {
                        type = nameof(LedgerType.Expense),
                        created.Title,
                        created.Amount,
                        category = categoryName,
                        created.TransactionDate,
                        created.Purpose,
                        attachmentCount = attached,
                        eventName,
                    },
                    Note: note));
                await db.SaveChangesAsync(token);
                return created;
            },
            cancellationToken);

        return await DetailAsync(fundId, entry, access, cancellationToken);
    }

    public async Task<LedgerEntryDetailDto> ReverseAsync(Guid fundId, Guid entryId, string reason, CancellationToken cancellationToken)
    {
        var (userId, access) = await ManagerAsync(fundId, cancellationToken);
        var cleanReason = Clean(reason) ?? throw new BusinessRuleException(LedgerErrors.ReasonRequired, LedgerErrors.ReasonRequiredMessage);

        try
        {
            var reversal = await db.InTransactionAsync(
                async token =>
                {
                    var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                    var original = await db.LockAsync<LedgerEntry>(entryId, token);
                    if (original is null || original.FundId != fundId)
                    {
                        throw new NotFoundException();
                    }

                    YearEndRule.EnsureFundActive(fund.Status);
                    if (original.Source != LedgerSource.Manual || original.Type != LedgerType.Expense || original.ReversesEntryId is not null)
                    {
                        throw new BusinessRuleException(LedgerErrors.NotReversible, LedgerErrors.NotReversibleMessage);
                    }

                    if (await db.AnyAsync(db.Query<LedgerEntry>().Where(item => item.ReversesEntryId == original.Id), token))
                    {
                        throw new ConflictException(LedgerErrors.AlreadyReversed, LedgerErrors.AlreadyReversedMessage);
                    }

                    if (original.EventId is { } eventId)
                    {
                        var fundEvent = await db.LockAsync<Event>(eventId, token) ?? throw new NotFoundException();
                        if (!EventStatusRules.AcceptsExpenses(fundEvent.Status))
                        {
                            throw new BusinessRuleException(LedgerErrors.ScopeLocked, LedgerErrors.ScopeLockedMessage);
                        }
                    }

                    var now = clock.GetUtcNow();
                    var entry = new LedgerEntry(
                        fundId,
                        original.EventId,
                        LedgerType.Income,
                        LedgerSource.Manual,
                        original.CategoryId,
                        ReversalTitle(original.Title),
                        purpose: null,
                        original.Amount,
                        VietnamTime.DateOf(now),
                        userId,
                        now)
                    {
                        ReversesEntryId = original.Id,
                        Note = cleanReason,
                    };
                    db.Add(entry);
                    audit.Record(new AuditEntry(
                        LedgerAuditActions.Reverse,
                        fundId,
                        userId,
                        original.EventId is null ? FinancialScope.General : FinancialScope.Event,
                        EventId: original.EventId,
                        EntityType: nameof(LedgerEntry),
                        EntityId: original.Id,
                        Before: new { original.Title, original.Amount, type = original.Type.ToString(), original.TransactionDate },
                        After: new { entry.Title, entry.Amount, type = entry.Type.ToString(), entry.TransactionDate },
                        Note: cleanReason));
                    await db.SaveChangesAsync(token);
                    return entry;
                },
                cancellationToken);

            return await DetailAsync(fundId, reversal, access, cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            // Hai người đảo cùng lúc: unique index ReversesEntryId là chốt chặn cuối (BR-08).
            throw new ConflictException(LedgerErrors.AlreadyReversed, LedgerErrors.AlreadyReversedMessage);
        }
    }

    public static string ReversalTitle(string originalTitle)
    {
        var title = ReversalTitlePrefix + originalTitle;
        return title.Length <= ReversalTitleMaxLength ? title : title[..(ReversalTitleMaxLength - 1)] + "…";
    }

    // ---------- Đọc ----------

    private async Task<IReadOnlyList<LedgerEntryDto>> ProjectAsync(Guid fundId, IQueryable<LedgerEntry> entries, CancellationToken cancellationToken)
    {
        var reversals = db.Query<LedgerEntry>().Where(item => item.FundId == fundId && item.ReversesEntryId != null);
        var files = db.Query<Attachment>().Where(item => item.FundId == fundId);

        var rows = await db.ToListAsync(
            from entry in entries
            join user in db.Query<User>() on entry.PerformedByUserId equals user.Id
            join category in db.Query<Category>() on entry.CategoryId equals (Guid?)category.Id into categoryRows
            from category in categoryRows.DefaultIfEmpty()
            join item in db.Query<Event>() on entry.EventId equals (Guid?)item.Id into eventRows
            from fundEvent in eventRows.DefaultIfEmpty()
            select new
            {
                entry,
                PerformedByName = user.FullName,
                CategoryName = category == null ? null : category.Name,
                EventName = fundEvent == null ? null : fundEvent.Name,
                ReversedBy = reversals.Where(item => item.ReversesEntryId == entry.Id).Select(item => (Guid?)item.Id).FirstOrDefault(),
                AttachmentCount = files.Count(item => item.OwnerId == entry.Id),
            },
            cancellationToken);

        return rows
            .OrderByDescending(row => row.entry.TransactionDate)
            .ThenByDescending(row => row.entry.CreatedAt)
            .ThenByDescending(row => row.entry.Id)
            .Select(row => ToDto(row.entry, row.EventName, row.CategoryName, row.PerformedByName, row.ReversedBy, row.AttachmentCount))
            .ToArray();
    }

    private async Task<LedgerEntryDetailDto> DetailAsync(Guid fundId, LedgerEntry entry, FundAccess access, CancellationToken cancellationToken)
    {
        var dto = (await ProjectAsync(fundId, db.Query<LedgerEntry>().Where(item => item.Id == entry.Id), cancellationToken)).Single();
        var files = await attachments.ListByOwnerAsync(fundId, entry.Id, cancellationToken);
        var role = await db.FirstOrDefaultAsync(
            db.Query<FundMember>()
                .Where(member => member.FundId == fundId && member.UserId == entry.PerformedByUserId)
                .Select(member => (MemberRole?)member.Role),
            cancellationToken);

        LedgerReversalDto? reversedBy = null;
        if (dto.ReversedByEntryId is { } reversalId)
        {
            reversedBy = await db.FirstOrDefaultAsync(
                from item in db.Query<LedgerEntry>()
                where item.Id == reversalId
                join user in db.Query<User>() on item.PerformedByUserId equals user.Id
                select new LedgerReversalDto(item.Id, item.Amount, item.TransactionDate, item.Note, user.FullName, item.CreatedAt),
                cancellationToken);
        }

        LedgerLinkDto? reverses = null;
        if (entry.ReversesEntryId is { } originalId)
        {
            reverses = await db.FirstOrDefaultAsync(
                db.Query<LedgerEntry>()
                    .Where(item => item.Id == originalId)
                    .Select(item => new LedgerLinkDto(item.Id, item.Title, item.TransactionDate)),
                cancellationToken);
        }

        var payerName = entry.PaymentId is { } paymentId
            ? await db.FirstOrDefaultAsync(
                from payment in db.Query<Payment>()
                where payment.Id == paymentId
                join user in db.Query<User>() on payment.PayerUserId equals user.Id
                select user.FullName,
                cancellationToken)
            : null;

        var canReverse = CanManage(access)
            && entry.Source == LedgerSource.Manual
            && entry.Type == LedgerType.Expense
            && entry.ReversesEntryId is null
            && reversedBy is null
            && await ScopeOpenForReversalAsync(fundId, entry.EventId, cancellationToken);

        var history = access.Permissions.Contains(Permissions.AuditView)
            ? await HistoryAsync(fundId, entry.Id, cancellationToken)
            : [];

        return new LedgerEntryDetailDto(
            dto,
            entry.Purpose,
            // Ghi chú của bút toán đảo là lý do đảo; của khoản chi là ghi chú người ghi nhập.
            entry.Note,
            role,
            files,
            reversedBy,
            reverses,
            canReverse,
            history,
            payerName);
    }

    private async Task<bool> ScopeOpenForReversalAsync(Guid fundId, Guid? eventId, CancellationToken cancellationToken)
    {
        var fundActive = await db.AnyAsync(
            db.Query<Fund>().Where(fund => fund.Id == fundId && fund.Status == FundStatus.Active), cancellationToken);
        if (!fundActive || eventId is not { } id)
        {
            return fundActive;
        }

        var status = await db.FirstOrDefaultAsync(
            db.Query<Event>().Where(item => item.Id == id).Select(item => (EventStatus?)item.Status), cancellationToken);
        return status is { } value && EventStatusRules.AcceptsPayments(value);
    }

    /// <summary>Nhật ký của dòng này (ghi, đảo) – audit ghi EntityId = dòng gốc cho cả hai thao tác.</summary>
    private async Task<IReadOnlyList<LedgerHistoryDto>> HistoryAsync(Guid fundId, Guid entryId, CancellationToken cancellationToken)
    {
        var key = entryId.ToString();
        var rows = await db.ToListAsync(
            from log in db.Query<AuditLog>()
            where log.FundId == fundId && log.EntityType == nameof(LedgerEntry) && log.EntityId == key
            join user in db.Query<User>() on log.ActorUserId equals (Guid?)user.Id into actors
            from actor in actors.DefaultIfEmpty()
            orderby log.At
            select new LedgerHistoryDto(log.Action, actor == null ? "Hệ thống" : actor.FullName, log.At),
            cancellationToken);
        return rows;
    }

    private Task<string?> CategoryNameAsync(Guid categoryId, CancellationToken cancellationToken) =>
        db.FirstOrDefaultAsync(db.Query<Category>().Where(category => category.Id == categoryId).Select(category => category.Name), cancellationToken);

    /// <summary>
    /// Học sinh thấy khoản chi ghi tay, bút toán đảo của chúng và các khoản thu do CHÍNH MÌNH đã nộp; không thấy ai khác đã đóng (OQ-14).
    /// </summary>
    private IQueryable<LedgerEntry> Visible(IQueryable<LedgerEntry> entries, bool manager, Guid viewerId)
    {
        if (manager)
        {
            return entries;
        }

        var mine = db.Query<Payment>().Where(payment => payment.PayerUserId == viewerId);
        return entries.Where(entry =>
            entry.Source == LedgerSource.Manual
            || (entry.Source == LedgerSource.Payment && mine.Any(payment => payment.Id == entry.PaymentId)));
    }

    /// <summary>Sự kiện phải thuộc quỹ; người không quản lý phải là người đã nhận tham gia (như chứng từ chi sự kiện, AZ-05).</summary>
    private async Task EnsureCanViewEventAsync(Guid fundId, Guid eventId, bool manager, CancellationToken cancellationToken)
    {
        var exists = await db.AnyAsync(db.Query<Event>().Where(item => item.Id == eventId && item.FundId == fundId), cancellationToken);
        if (!exists)
        {
            throw new NotFoundException();
        }

        if (manager)
        {
            return;
        }

        var userId = CurrentUserId();
        var accepted = await db.AnyAsync(
            db.Query<EventParticipant>().Where(participant =>
                participant.EventId == eventId && participant.UserId == userId && participant.Response == ParticipationResponse.Accepted),
            cancellationToken);
        if (!accepted)
        {
            throw new NotFoundException();
        }
    }

    private void EnsureNotFuture(DateOnly transactionDate)
    {
        if (transactionDate > VietnamTime.DateOf(clock.GetUtcNow()))
        {
            throw new BusinessRuleException(LedgerErrors.FutureDate, LedgerErrors.FutureDateMessage);
        }
    }

    private async Task<(Guid UserId, FundAccess Access)> ViewerAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        return (userId, await accessService.GetFundAccessAsync(userId, fundId, cancellationToken));
    }

    /// <summary>Controller đã yêu cầu <c>Ledger.Manage</c>; kiểm lại để service không dựa vào nơi gọi.</summary>
    private async Task<(Guid UserId, FundAccess Access)> ManagerAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(fundId, cancellationToken);
        return CanManage(viewer.Access) ? viewer : throw new ForbiddenAccessException();
    }

    private static bool CanManage(FundAccess access) => access.Permissions.Contains(Permissions.LedgerManage);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static LedgerEntryDto ToDto(LedgerEntry entry, string? eventName, string? categoryName, string performedByName, Guid? reversedBy, int attachmentCount) =>
        new(
            entry.Id,
            entry.EventId,
            eventName,
            entry.Type,
            entry.Source,
            entry.CategoryId,
            categoryName,
            entry.Title,
            entry.Amount,
            entry.TransactionDate,
            entry.PerformedByUserId,
            performedByName,
            entry.IsSelfDeclared,
            entry.ReversesEntryId,
            reversedBy,
            attachmentCount,
            entry.CreatedAt);

    private Guid CurrentUserId() => currentUser.UserId ?? throw new ForbiddenAccessException();
}
