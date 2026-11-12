using System.Text.Json;
using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Transactions;

/// <inheritdoc />
/// <remarks>
/// Quyền đã được <c>[RequirePermission]</c> kiểm tra ở API; service kiểm tra lại qua <see cref="IAccessService"/>.
/// Phạm vi công khai (TD-84, TD-96): một truy vấn gốc <see cref="Visible"/> lọc <c>IsPublic</c> trước mọi điều kiện, và
/// <see cref="Project"/> của bản công khai không chọn cột nội bộ – dữ liệu đó không rời khỏi database.
/// Sửa / hủy khóa dòng giao dịch (<c>SELECT … FOR UPDATE</c>, TD-93) để sửa không ghi đè khoản vừa bị hủy và hai lệnh hủy không cùng lọt.
/// </remarks>
public sealed class TransactionsService(
    IAppDb db,
    IAccessService accessService,
    ICurrentUser currentUser,
    AuditTrail auditTrail,
    TimeProvider clock) : ITransactionsService
{
    private const string IdempotencyConstraint = "UQ_FundTransaction_FundId_IdempotencyKey";
    private const string EntityType = nameof(FundTransaction);

    private sealed record TransactionRow(
        Guid Id,
        TransactionType Type,
        long Amount,
        DateOnly OccurredOn,
        Guid CategoryId,
        string? CategoryName,
        string Description,
        bool IsCancelled,
        DateTimeOffset? CancelledAt,
        string? Note,
        bool? IsPublic,
        string? CancelReason,
        DateTimeOffset? CreatedAt,
        DateTimeOffset? UpdatedAt,
        string? CreatedByName,
        string? UpdatedByName,
        string? CancelledByName);

    private Guid UserId => currentUser.UserId
        ?? throw new InvalidOperationException("Thao tác cần người dùng đã đăng nhập.");

    public async Task<PagedResult<TransactionResponse>> ListAsync(
        Guid fundId,
        TransactionFilter filter,
        CancellationToken cancellationToken)
    {
        var fullView = await FullViewAsync(fundId, cancellationToken);
        var query = Visible(fundId, fullView);

        query = filter.Status switch
        {
            TransactionStatusFilter.Active => query.Where(transaction => !transaction.IsCancelled),
            TransactionStatusFilter.Cancelled => query.Where(transaction => transaction.IsCancelled),
            _ => query,
        };

        if (filter.Type is { } type)
        {
            query = query.Where(transaction => transaction.Type == type);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(transaction => transaction.CategoryId == categoryId);
        }

        if (filter.From is { } from)
        {
            query = query.Where(transaction => transaction.OccurredOn >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(transaction => transaction.OccurredOn <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();

            // Bản công khai không tìm trong ghi chú: kết quả tìm không được để lộ nội dung ghi chú (P-06).
            query = fullView
                ? query.Where(transaction =>
                    transaction.Description.ToLower().Contains(term)
                    || (transaction.Note != null && transaction.Note.ToLower().Contains(term)))
                : query.Where(transaction => transaction.Description.ToLower().Contains(term));
        }

        var total = await db.CountAsync(query, cancellationToken);
        var page = query
            .OrderByDescending(transaction => transaction.OccurredOn)
            .ThenByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize);

        var rows = await db.ToListAsync(Project(page, fullView), cancellationToken);
        return new PagedResult<TransactionResponse>(
            rows.Select(row => ToResponse(row, fullView)).ToArray(),
            total,
            filter.Page,
            filter.PageSize);
    }

    public async Task<TransactionResponse> GetAsync(Guid fundId, Guid id, CancellationToken cancellationToken)
    {
        var fullView = await FullViewAsync(fundId, cancellationToken);
        return await FindAsync(fundId, id, fullView, cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionHistoryItem>> GetHistoryAsync(Guid fundId, Guid id, CancellationToken cancellationToken)
    {
        await RequireAsync(fundId, Permissions.LedgerViewFull, cancellationToken);
        if (!await db.AnyAsync(db.Query<FundTransaction>().Where(item => item.Id == id && item.FundId == fundId), cancellationToken))
        {
            throw new NotFoundException();
        }

        var entityId = id.ToString();
        var logs = await db.ToListAsync(
            db.Query<AuditLog>()
                .Where(log => log.EntityType == EntityType && log.EntityId == entityId)
                .OrderBy(log => log.OccurredAt)
                .ThenBy(log => log.Id)
                .Select(log => new { log.OccurredAt, log.Action, log.ActorUserId, log.OldValues, log.NewValues }),
            cancellationToken);

        var actorIds = logs.Where(log => log.ActorUserId != null).Select(log => log.ActorUserId!.Value).Distinct().ToArray();
        var actorNames = (await db.ToListAsync(
                db.Query<User>().Where(user => actorIds.Contains(user.Id)).Select(user => new { user.Id, user.FullName }),
                cancellationToken))
            .ToDictionary(user => user.Id, user => user.FullName);

        return logs
            .Select(log => new TransactionHistoryItem(
                log.OccurredAt,
                log.Action,
                log.ActorUserId is { } actorId ? actorNames.GetValueOrDefault(actorId) : null,
                ParseJson(log.OldValues),
                ParseJson(log.NewValues)))
            .ToArray();
    }

    public async Task<TransactionResponse> CreateAsync(
        Guid fundId,
        TransactionType type,
        TransactionValues values,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = UserId;
        await RequireAsync(fundId, Permissions.TransactionManage, cancellationToken);

        if (await FindIdByKeyAsync(fundId, idempotencyKey, cancellationToken) is { } existingId)
        {
            return await SavedAsync(fundId, existingId, replayed: true, cancellationToken);
        }

        var now = clock.GetUtcNow();
        EnsureNotInFuture(values.OccurredOn, now);
        await EnsureCategoryUsableAsync(fundId, type, values.CategoryId, currentCategoryId: null, cancellationToken);

        var description = values.Description.Trim();
        var note = NormalizeNote(values.Note);
        var transaction = new FundTransaction(
            fundId, type, values.Amount, values.OccurredOn, values.CategoryId, description, note, values.IsPublic, userId, idempotencyKey, now);
        db.Add(transaction);
        auditTrail.Record(
            TransactionsAuditActions.Create,
            AuditCategory.Change,
            userId,
            EntityType,
            transaction.Id,
            newValues: Snapshot(type, values.Amount, values.OccurredOn, values.CategoryId, description, note, values.IsPublic));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == IdempotencyConstraint)
        {
            // Hai request cùng khóa chạy song song: request thua trả lại khoản của request thắng.
            var winnerId = await FindIdByKeyAsync(fundId, idempotencyKey, cancellationToken)
                ?? throw new InvalidOperationException("Không tìm thấy khoản đã lưu với khóa chống lưu trùng.", exception);
            return await SavedAsync(fundId, winnerId, replayed: true, cancellationToken);
        }

        return await SavedAsync(fundId, transaction.Id, replayed: false, cancellationToken);
    }

    public async Task<TransactionResponse> UpdateAsync(Guid fundId, Guid id, TransactionValues values, CancellationToken cancellationToken)
    {
        var userId = UserId;
        await RequireAsync(fundId, Permissions.TransactionManage, cancellationToken);

        await db.InTransactionAsync(
            async token =>
            {
                var transaction = await LockAsync(fundId, id, token);
                transaction.EnsureActive();

                var now = clock.GetUtcNow();
                EnsureNotInFuture(values.OccurredOn, now);
                await EnsureCategoryUsableAsync(fundId, transaction.Type, values.CategoryId, transaction.CategoryId, token);

                var description = values.Description.Trim();
                var note = NormalizeNote(values.Note);
                var oldValues = Snapshot(
                    transaction.Type, transaction.Amount, transaction.OccurredOn, transaction.CategoryId, transaction.Description, transaction.Note, transaction.IsPublic);
                var newValues = Snapshot(transaction.Type, values.Amount, values.OccurredOn, values.CategoryId, description, note, values.IsPublic);
                if (oldValues.Equals(newValues))
                {
                    return false;
                }

                transaction.Update(values.Amount, values.OccurredOn, values.CategoryId, description, note, values.IsPublic, userId, now);
                auditTrail.Record(TransactionsAuditActions.Update, AuditCategory.Change, userId, EntityType, transaction.Id, oldValues, newValues);
                await db.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);

        return await SavedAsync(fundId, id, replayed: null, cancellationToken);
    }

    public async Task<TransactionResponse> CancelAsync(Guid fundId, Guid id, string reason, CancellationToken cancellationToken)
    {
        var userId = UserId;
        await RequireAsync(fundId, Permissions.TransactionManage, cancellationToken);

        await db.InTransactionAsync(
            async token =>
            {
                var transaction = await LockAsync(fundId, id, token);
                transaction.Cancel(reason.Trim(), userId, clock.GetUtcNow());
                auditTrail.Record(
                    TransactionsAuditActions.Cancel,
                    AuditCategory.Change,
                    userId,
                    EntityType,
                    transaction.Id,
                    new { isCancelled = false },
                    new { isCancelled = true, reason = transaction.CancelReason });
                await db.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);

        return await SavedAsync(fundId, id, replayed: null, cancellationToken);
    }

    /// <summary>Thành viên quỹ (không phải → 404); <c>true</c> khi xem bản đầy đủ (<c>Ledger.ViewFull</c>).</summary>
    private async Task<bool> FullViewAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.FundView))
        {
            throw new ForbiddenAccessException();
        }

        return access.Permissions.Contains(Permissions.LedgerViewFull);
    }

    private async Task RequireAsync(Guid fundId, string permission, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);
        if (!access.Permissions.Contains(permission))
        {
            throw new ForbiddenAccessException();
        }
    }

    /// <summary>Khoản không công khai bị loại ngay từ truy vấn gốc, trước mọi bộ lọc, đếm và phân trang (NFR-08).</summary>
    private IQueryable<FundTransaction> Visible(Guid fundId, bool fullView)
    {
        var query = db.Query<FundTransaction>().Where(transaction => transaction.FundId == fundId);
        return fullView ? query : query.Where(transaction => transaction.IsPublic);
    }

    /// <summary>Bản công khai không chọn các cột nội bộ (TD-96).</summary>
    private IQueryable<TransactionRow> Project(IQueryable<FundTransaction> query, bool fullView)
    {
        var categories = db.Query<Category>();
        if (!fullView)
        {
            return query.Select(transaction => new TransactionRow(
                transaction.Id,
                transaction.Type,
                transaction.Amount,
                transaction.OccurredOn,
                transaction.CategoryId,
                categories.Where(category => category.Id == transaction.CategoryId).Select(category => category.Name).FirstOrDefault(),
                transaction.Description,
                transaction.IsCancelled,
                transaction.CancelledAt,
                null, null, null, null, null, null, null, null));
        }

        var users = db.Query<User>();
        return query.Select(transaction => new TransactionRow(
            transaction.Id,
            transaction.Type,
            transaction.Amount,
            transaction.OccurredOn,
            transaction.CategoryId,
            categories.Where(category => category.Id == transaction.CategoryId).Select(category => category.Name).FirstOrDefault(),
            transaction.Description,
            transaction.IsCancelled,
            transaction.CancelledAt,
            transaction.Note,
            transaction.IsPublic,
            transaction.CancelReason,
            transaction.CreatedAt,
            transaction.UpdatedAt,
            users.Where(user => user.Id == transaction.CreatedBy).Select(user => user.FullName).FirstOrDefault(),
            users.Where(user => user.Id == transaction.UpdatedBy).Select(user => user.FullName).FirstOrDefault(),
            users.Where(user => user.Id == transaction.CancelledBy).Select(user => user.FullName).FirstOrDefault()));
    }

    private static TransactionResponse ToResponse(TransactionRow row, bool fullView) => new(
        row.Id,
        row.Type,
        row.Amount,
        row.OccurredOn,
        row.CategoryId,
        row.CategoryName,
        row.Description,
        row.IsCancelled,
        row.CancelledAt is { } cancelledAt ? VietnamTime.DateOf(cancelledAt) : null,
        row.Note,
        row.IsPublic,
        row.CancelReason,
        fullView ? row.CancelledAt : null,
        row.CreatedAt,
        row.UpdatedAt,
        row.CreatedByName,
        row.UpdatedByName,
        row.CancelledByName);

    private async Task<TransactionResponse> FindAsync(Guid fundId, Guid id, bool fullView, CancellationToken cancellationToken)
    {
        var row = await db.FirstOrDefaultAsync(
            Project(Visible(fundId, fullView).Where(transaction => transaction.Id == id), fullView),
            cancellationToken) ?? throw new NotFoundException();
        return ToResponse(row, fullView);
    }

    /// <summary>Kết quả ghi luôn là bản đầy đủ (người ghi là thủ quỹ) kèm cờ số dư âm.</summary>
    private async Task<TransactionResponse> SavedAsync(Guid fundId, Guid id, bool? replayed, CancellationToken cancellationToken)
    {
        var response = await FindAsync(fundId, id, fullView: true, cancellationToken);
        return response with { Replayed = replayed, IsNegativeBalance = await IsBalanceNegativeAsync(fundId, cancellationToken) };
    }

    /// <summary>Số dư không lưu riêng, luôn tính từ các khoản chưa hủy (BR-02).</summary>
    private async Task<bool> IsBalanceNegativeAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var totals = await db.ToListAsync(
            db.Query<FundTransaction>()
                .Where(transaction => transaction.FundId == fundId && !transaction.IsCancelled)
                .GroupBy(transaction => transaction.Type)
                .Select(group => new { Type = group.Key, Total = group.Sum(transaction => transaction.Amount) }),
            cancellationToken);

        var income = totals.Where(item => item.Type == TransactionType.Income).Sum(item => item.Total);
        var expense = totals.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Total);
        return income < expense;
    }

    private Task<Guid?> FindIdByKeyAsync(Guid fundId, Guid idempotencyKey, CancellationToken cancellationToken) =>
        db.FirstOrDefaultAsync(
            db.Query<FundTransaction>()
                .Where(transaction => transaction.FundId == fundId && transaction.IdempotencyKey == idempotencyKey)
                .Select(transaction => (Guid?)transaction.Id),
            cancellationToken);

    private async Task<FundTransaction> LockAsync(Guid fundId, Guid id, CancellationToken cancellationToken)
    {
        var transaction = await db.LockAsync<FundTransaction>(id, cancellationToken);
        return transaction is not null && transaction.FundId == fundId ? transaction : throw new NotFoundException();
    }

    /// <summary>BR-07: so với ngày hiện tại theo giờ Việt Nam.</summary>
    private static void EnsureNotInFuture(DateOnly occurredOn, DateTimeOffset now)
    {
        if (occurredOn > VietnamTime.DateOf(now))
        {
            throw TransactionsErrors.FutureDate();
        }
    }

    /// <summary>Danh mục cùng quỹ, cùng loại; danh mục đã ẩn không chọn được, trừ khi khoản đang dùng nó (BR-08).</summary>
    private async Task EnsureCategoryUsableAsync(
        Guid fundId,
        TransactionType type,
        Guid categoryId,
        Guid? currentCategoryId,
        CancellationToken cancellationToken)
    {
        var isHidden = await db.FirstOrDefaultAsync(
            db.Query<Category>()
                .Where(category => category.Id == categoryId && category.FundId == fundId && category.Type == type)
                .Select(category => (bool?)category.IsHidden),
            cancellationToken);

        if (isHidden is null || (isHidden.Value && categoryId != currentCategoryId))
        {
            throw TransactionsErrors.CategoryInvalid();
        }
    }

    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private static object Snapshot(
        TransactionType type,
        long amount,
        DateOnly occurredOn,
        Guid categoryId,
        string description,
        string? note,
        bool isPublic) =>
        new { type = type.ToString(), amount, occurredOn, categoryId, description, note, isPublic };

    private static JsonElement? ParseJson(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<JsonElement>(json);
}
