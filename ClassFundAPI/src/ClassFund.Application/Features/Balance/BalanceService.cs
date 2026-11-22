using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Balance;

/// <summary>
/// Số dư cá nhân theo (quỹ, người) – tiền thủ quỹ giữ hộ, độc lập quỹ chung và sự kiện (FR-40 → FR-42, BR-25, BR-26).
/// </summary>
public interface IBalanceService
{
    /// <summary>
    /// Cộng số dư (M9 tiền thừa quyết toán, M14 tổng kết cuối năm). Tạo PersonalBalance nếu chưa có, ghi BalanceTransaction kèm BalanceAfter.
    /// <b>Chỉ gọi trong <c>IAppDb.InTransactionAsync</c> của module gọi</b>; không tự SaveChanges.
    /// </summary>
    /// <returns>Số dư sau khi cộng.</returns>
    Task<long> CreditAsync(Guid fundId, Guid userId, long amount, BalanceTransactionType type, Guid? referenceId, Guid actorUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Trừ số dư (M6b đóng khoản thu bằng số dư – <c>ChargePayment</c>; rút tiền – <c>Withdrawal</c>). Không đủ → 422 <c>balance.insufficient</c>;
    /// đóng khoản thu khi đang có yêu cầu rút chờ → 422 <c>balance.locked_by_withdrawal</c> (BR-26).
    /// <b>Chỉ gọi trong transaction của module gọi</b>; không tự SaveChanges.
    /// </summary>
    /// <returns>Số dư sau khi trừ.</returns>
    Task<long> DebitAsync(Guid fundId, Guid userId, long amount, BalanceTransactionType type, Guid? referenceId, Guid actorUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Số dư hiện có của người dùng ở quỹ (0 nếu chưa có), khóa dòng số dư để quyết định dùng bao nhiêu cho một khoản đóng.
    /// <b>Chỉ gọi trong transaction của module gọi.</b>
    /// </summary>
    Task<long> GetBalanceForUpdateAsync(Guid fundId, Guid userId, CancellationToken cancellationToken);

    Task<bool> HasPendingWithdrawalAsync(Guid fundId, Guid userId, CancellationToken cancellationToken);

    /// <summary>B-01: số dư, yêu cầu rút gần nhất, lịch sử biến động, số dư ở các quỹ khác.</summary>
    Task<MyBalanceDto> GetMineAsync(Guid fundId, CancellationToken cancellationToken);

    /// <summary>Rút TOÀN BỘ số dư (OQ-11): số tiền = số dư lúc gửi; mỗi người một yêu cầu đang chờ. Quỹ đã tổng kết vẫn rút được (BR-37).</summary>
    Task<MyBalanceDto> RequestWithdrawalAsync(Guid fundId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class BalanceService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    AuditTrail audit,
    TimeProvider clock) : IBalanceService
{
    public const int HistoryLimit = 100;

    public Task<long> CreditAsync(Guid fundId, Guid userId, long amount, BalanceTransactionType type, Guid? referenceId, Guid actorUserId, CancellationToken cancellationToken)
    {
        MoneyRule.EnsureValid(amount);
        return ApplyAsync(fundId, userId, amount, type, referenceId, actorUserId, cancellationToken);
    }

    public async Task<long> DebitAsync(Guid fundId, Guid userId, long amount, BalanceTransactionType type, Guid? referenceId, Guid actorUserId, CancellationToken cancellationToken)
    {
        MoneyRule.EnsureValid(amount);
        if (type == BalanceTransactionType.ChargePayment)
        {
            WithdrawalRule.EnsureCanSpend(await HasPendingWithdrawalAsync(fundId, userId, cancellationToken));
        }

        return await ApplyAsync(fundId, userId, -amount, type, referenceId, actorUserId, cancellationToken);
    }

    public async Task<long> GetBalanceForUpdateAsync(Guid fundId, Guid userId, CancellationToken cancellationToken)
    {
        WithdrawalRule.EnsureCanSpend(await HasPendingWithdrawalAsync(fundId, userId, cancellationToken));
        return (await LockExistingAsync(fundId, userId, cancellationToken))?.Balance ?? 0;
    }

    public Task<bool> HasPendingWithdrawalAsync(Guid fundId, Guid userId, CancellationToken cancellationToken) =>
        db.AnyAsync(
            db.Query<WithdrawalRequest>().Where(request =>
                request.FundId == fundId && request.UserId == userId && request.Status == WithdrawalStatus.Requested),
            cancellationToken);

    public async Task<MyBalanceDto> GetMineAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = await RequireAsync(fundId, Permissions.BalanceViewOwn, cancellationToken);
        return await BuildMineAsync(fundId, userId, cancellationToken);
    }

    public async Task<MyBalanceDto> RequestWithdrawalAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = await RequireAsync(fundId, Permissions.BalanceWithdraw, cancellationToken);
        try
        {
            await db.InTransactionAsync(
                async token =>
                {
                    var balance = await LockExistingAsync(fundId, userId, token);
                    var amount = WithdrawalRule.AmountToRequest(balance?.Balance ?? 0, await HasPendingWithdrawalAsync(fundId, userId, token));

                    var request = new WithdrawalRequest(fundId, userId, amount, clock.GetUtcNow());
                    db.Add(request);
                    audit.Record(new AuditEntry(
                        BalanceAuditActions.WithdrawalRequest,
                        fundId,
                        userId,
                        FinancialScope.System,
                        EntityType: nameof(WithdrawalRequest),
                        EntityId: request.Id,
                        After: new { amount, status = nameof(WithdrawalStatus.Requested) },
                        Note: "Rút toàn bộ số dư cá nhân."));
                    await db.SaveChangesAsync(token);
                    return request;
                },
                cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            // Hai lần bấm cùng lúc: unique "một yêu cầu đang chờ / người / quỹ" là chốt chặn cuối.
            throw new BusinessRuleException(WithdrawalRule.PendingExistsCode, "Bạn đã có yêu cầu rút tiền đang chờ thủ quỹ xử lý.");
        }

        return await BuildMineAsync(fundId, userId, cancellationToken);
    }

    // ---------- Nội bộ ----------

    private async Task<long> ApplyAsync(Guid fundId, Guid userId, long delta, BalanceTransactionType type, Guid? referenceId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var balance = await LockExistingAsync(fundId, userId, cancellationToken);
        if (balance is null)
        {
            balance = new PersonalBalance(fundId, userId);
            db.Add(balance);
        }

        var after = balance.Balance + delta;
        if (after < 0)
        {
            throw new BusinessRuleException(BalanceErrors.Insufficient, BalanceErrors.InsufficientMessage);
        }

        balance.Balance = after;
        db.Add(new BalanceTransaction(fundId, userId, type, delta, after, referenceId, actorUserId, clock.GetUtcNow()));
        return after;
    }

    /// <summary>Khóa dòng số dư (<c>SELECT … FOR UPDATE</c>) để hai thao tác đồng thời không làm âm / lệch số dư; null khi chưa có.</summary>
    private async Task<PersonalBalance?> LockExistingAsync(Guid fundId, Guid userId, CancellationToken cancellationToken)
    {
        var id = await db.FirstOrDefaultAsync(
            db.Query<PersonalBalance>().Where(item => item.FundId == fundId && item.UserId == userId).Select(item => (Guid?)item.Id),
            cancellationToken);
        return id is { } balanceId ? await db.LockAsync<PersonalBalance>(balanceId, cancellationToken) : null;
    }

    private async Task<MyBalanceDto> BuildMineAsync(Guid fundId, Guid userId, CancellationToken cancellationToken)
    {
        var fundName = await db.FirstOrDefaultAsync(db.Query<Fund>().Where(fund => fund.Id == fundId).Select(fund => fund.Name), cancellationToken)
            ?? throw new NotFoundException();
        var balance = await db.FirstOrDefaultAsync(
            db.Query<PersonalBalance>().Where(item => item.FundId == fundId && item.UserId == userId).Select(item => (long?)item.Balance),
            cancellationToken) ?? 0;

        var latest = await db.FirstOrDefaultAsync(
            from request in db.Query<WithdrawalRequest>()
            where request.FundId == fundId && request.UserId == userId
            join user in db.Query<User>() on request.HandledByUserId equals (Guid?)user.Id into handlers
            from handler in handlers.DefaultIfEmpty()
            // Yêu cầu đang chờ (nếu có) trước, rồi tới kết quả lần gần nhất.
            orderby request.Status == WithdrawalStatus.Requested ? 0 : 1, request.RequestedAt descending, request.HandledAt descending
            select new MyWithdrawalDto(
                request.Id,
                request.Amount,
                request.Status,
                request.RequestedAt,
                request.HandledAt,
                handler == null ? null : handler.FullName,
                request.RejectionReason),
            cancellationToken);

        var history = await HistoryAsync(fundId, userId, cancellationToken);

        var others = await db.ToListAsync(
            from member in db.Query<FundMember>()
            where member.UserId == userId && member.Status == MemberStatus.Active && member.FundId != fundId
            join fund in db.Query<Fund>() on member.FundId equals fund.Id
            join item in db.Query<PersonalBalance>().Where(item => item.UserId == userId) on fund.Id equals item.FundId into balances
            from personal in balances.DefaultIfEmpty()
            orderby fund.Name
            select new OtherFundBalanceDto(fund.Id, fund.Name, fund.Status == FundStatus.Archived, personal == null ? 0 : personal.Balance),
            cancellationToken);

        return new MyBalanceDto(fundName, balance, latest, history, others);
    }

    /// <summary>Lịch sử mới nhất trước, mỗi dòng có nội dung + phạm vi gốc (sự kiện nào, khoản thu nào).</summary>
    private async Task<IReadOnlyList<BalanceHistoryItemDto>> HistoryAsync(Guid fundId, Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.ToListAsync(
            db.Query<BalanceTransaction>()
                .Where(item => item.FundId == fundId && item.UserId == userId)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .Take(HistoryLimit),
            cancellationToken);

        var references = rows.Where(row => row.ReferenceId != null).Select(row => row.ReferenceId!.Value).Distinct().ToArray();

        // Tiền thừa quyết toán: SettlementLine → Settlement → (sự kiện | cuối năm quỹ chung).
        var settlements = await db.ToListAsync(
            from line in db.Query<SettlementLine>()
            where references.Contains(line.Id)
            join settlement in db.Query<Settlement>() on line.SettlementId equals settlement.Id
            join item in db.Query<Event>() on settlement.EventId equals (Guid?)item.Id into events
            from fundEvent in events.DefaultIfEmpty()
            select new { line.Id, settlement.EventId, EventName = fundEvent == null ? null : fundEvent.Name },
            cancellationToken);

        // Đóng khoản thu bằng số dư: Payment → nghĩa vụ → khoản thu.
        var payments = await db.ToListAsync(
            from payment in db.Query<Payment>()
            where references.Contains(payment.Id)
            join assignment in db.Query<ChargeAssignment>() on payment.AssignmentId equals assignment.Id
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            join item in db.Query<Event>() on charge.EventId equals (Guid?)item.Id into events
            from fundEvent in events.DefaultIfEmpty()
            select new { payment.Id, charge.Title, charge.EventId, EventName = fundEvent == null ? null : fundEvent.Name },
            cancellationToken);

        return rows.Select(row =>
        {
            var settlement = settlements.FirstOrDefault(item => item.Id == row.ReferenceId);
            var payment = payments.FirstOrDefault(item => item.Id == row.ReferenceId);
            var (description, scope, eventName) = row.Type switch
            {
                BalanceTransactionType.SettlementCredit => settlement is null
                    ? ("Tiền thừa quyết toán", (FinancialScope?)null, (string?)null)
                    : settlement.EventId is null
                        ? ("Tiền thừa tổng kết cuối năm", FinancialScope.General, null)
                        : ("Tiền thừa quyết toán", FinancialScope.Event, settlement.EventName),
                BalanceTransactionType.ChargePayment => payment is null
                    ? ("Đóng khoản thu bằng số dư", null, null)
                    : ($"Đóng “{payment.Title}”", payment.EventId is null ? FinancialScope.General : FinancialScope.Event, payment.EventName),
                BalanceTransactionType.ChargeRefund => payment is null
                    ? ("Hoàn số dư do thanh toán bị từ chối", null, null)
                    : ($"Hoàn số dư – thanh toán “{payment.Title}” bị từ chối", payment.EventId is null ? FinancialScope.General : FinancialScope.Event, payment.EventName),
                BalanceTransactionType.Withdrawal => ("Rút số dư – thủ quỹ đã trả", null, null),
                _ => ("Điều chỉnh số dư", null, null),
            };
            return new BalanceHistoryItemDto(row.Id, row.Type, row.Amount, row.BalanceAfter, row.CreatedAt, description, scope, eventName);
        }).ToArray();
    }

    private async Task<Guid> RequireAsync(Guid fundId, string permission, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenAccessException();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        return access.Permissions.Contains(permission) ? userId : throw new ForbiddenAccessException();
    }
}
