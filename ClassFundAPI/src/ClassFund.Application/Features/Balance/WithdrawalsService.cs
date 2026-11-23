using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.AuditLogs;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Balance;

/// <summary>
/// B-02 "Rút số dư": thủ quỹ rút số dư giúp từng thành viên theo số tiền nhập (≥ 10.000đ, bội của 1.000đ, ≤ số dư).
/// Số dư chỉ vào từ quyết toán / hoàn tiền khi đóng danh sách và chỉ ra khi rút. Học sinh chỉ xem (B-01).
/// Không ghi LedgerEntry (tiền giữ hộ không thuộc quỹ chung / sự kiện). Vẫn rút được khi quỹ đã tổng kết (BR-37).
/// </summary>
public interface IWithdrawalsService
{
    /// <summary>Thành viên của quỹ kèm số dư cá nhân; <paramref name="search"/> lọc theo tên / email.</summary>
    Task<MemberBalancesResult> ListBalancesAsync(Guid fundId, string? search, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Rút <paramref name="amount"/> từ số dư của <paramref name="userId"/>: kiểm tra định dạng, khóa dòng số dư rồi kiểm tra ≤ số dư,
    /// trừ + <c>BalanceTransaction(Withdrawal)</c> + lịch sử rút + audit (tự ghi nhận khi rút của chính mình) + thông báo.
    /// </summary>
    Task<MemberBalanceDto> WithdrawAsync(Guid fundId, Guid userId, long amount, CancellationToken cancellationToken);

    /// <summary>Lịch sử rút, mới nhất trước.</summary>
    Task<PagedResult<WithdrawalDto>> HistoryAsync(Guid fundId, int page, int pageSize, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class WithdrawalsService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    IBalanceService balances,
    AuditTrail audit,
    Notifier notifier,
    TimeProvider clock) : IWithdrawalsService
{
    public async Task<MemberBalancesResult> ListBalancesAsync(Guid fundId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        await RequireHandlerAsync(fundId, cancellationToken);

        var members = from member in db.Query<FundMember>()
                      where member.FundId == fundId
                      join user in db.Query<User>() on member.UserId equals user.Id
                      select new { user.Id, user.FullName, user.Email, member.Role, member.Status };

        var needle = search?.Trim();
        if (!string.IsNullOrEmpty(needle))
        {
            var lowered = needle.ToLowerInvariant();
            members = members.Where(row => row.FullName.ToLower().Contains(lowered) || row.Email.Contains(lowered));
        }

        var query = from row in members
                    join item in db.Query<PersonalBalance>().Where(balance => balance.FundId == fundId) on row.Id equals item.UserId into balances
                    from personal in balances.DefaultIfEmpty()
                    select new MemberBalanceRow
                    {
                        UserId = row.Id,
                        FullName = row.FullName,
                        Email = row.Email,
                        Role = row.Role,
                        IsActive = row.Status == MemberStatus.Active,
                        Balance = personal == null ? 0 : personal.Balance,
                    };

        var total = await db.CountAsync(query, cancellationToken);
        var rows = await db.ToListAsync(
            query
                .OrderByDescending(row => row.Balance)
                .ThenBy(row => row.FullName)
                .ThenBy(row => row.UserId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize),
            cancellationToken);
        var held = (await db.ToListAsync(db.Query<PersonalBalance>().Where(item => item.FundId == fundId).Select(item => item.Balance), cancellationToken)).Sum();

        return new MemberBalancesResult(
            rows.Select(row => new MemberBalanceDto(row.UserId, row.FullName, row.Email, row.Role, row.IsActive, row.Balance)).ToArray(),
            total,
            page,
            pageSize,
            held);
    }

    public async Task<MemberBalanceDto> WithdrawAsync(Guid fundId, Guid userId, long amount, CancellationToken cancellationToken)
    {
        var actorId = await RequireHandlerAsync(fundId, cancellationToken);
        WithdrawalRule.EnsureValidAmount(amount);

        await db.InTransactionAsync(
            async token =>
            {
                var member = await db.FirstOrDefaultAsync(
                    from item in db.Query<FundMember>()
                    where item.FundId == fundId && item.UserId == userId
                    join user in db.Query<User>() on item.UserId equals user.Id
                    select new { user.FullName },
                    token) ?? throw new NotFoundException();

                var now = clock.GetUtcNow();
                var record = new WithdrawalRequest(fundId, userId, amount, now)
                {
                    Status = WithdrawalStatus.Paid,
                    HandledByUserId = actorId,
                    HandledAt = now,
                };

                // DebitAsync khóa dòng số dư rồi kiểm tra lại: không đủ → 422 balance.insufficient (hai lần rút đồng thời không làm âm).
                var before = await db.FirstOrDefaultAsync(
                    db.Query<PersonalBalance>().Where(item => item.FundId == fundId && item.UserId == userId).Select(item => (long?)item.Balance),
                    token) ?? 0;
                var after = await balances.DebitAsync(fundId, userId, amount, BalanceTransactionType.Withdrawal, record.Id, actorId, token);
                db.Add(record);

                audit.Record(new AuditEntry(
                    BalanceAuditActions.Withdraw,
                    fundId,
                    actorId,
                    FinancialScope.System,
                    EntityType: nameof(WithdrawalRequest),
                    EntityId: record.Id,
                    Before: new { balance = before },
                    After: new { payerName = member.FullName, amount, balance = after },
                    IsSelfAction: actorId == userId,
                    Note: actorId == userId ? "Thủ quỹ tự rút số dư của chính mình." : null));
                notifier.Send(
                    [userId],
                    new NotificationMessage(
                        fundId,
                        FinancialScope.System,
                        null,
                        NotificationType.WithdrawalProcessed,
                        "Thủ quỹ đã trả tiền rút số dư",
                        $"Bạn đã nhận {AuditSummary.FormatMoney(amount)}. Số dư cá nhân còn {AuditSummary.FormatMoney(after)}.",
                        nameof(WithdrawalRequest),
                        record.Id));
                await db.SaveChangesAsync(token);
                return record.Id;
            },
            cancellationToken);

        var result = await ListBalancesAsync(fundId, null, 1, 1000, cancellationToken);
        return result.Items.Single(item => item.UserId == userId);
    }

    public async Task<PagedResult<WithdrawalDto>> HistoryAsync(Guid fundId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await RequireHandlerAsync(fundId, cancellationToken);

        var query = from request in db.Query<WithdrawalRequest>()
                    where request.FundId == fundId
                    join user in db.Query<User>() on request.UserId equals user.Id
                    join item in db.Query<User>() on request.HandledByUserId equals (Guid?)item.Id into handlers
                    from handler in handlers.DefaultIfEmpty()
                    select new WithdrawalRow
                    {
                        Id = request.Id,
                        UserId = request.UserId,
                        FullName = user.FullName,
                        Amount = request.Amount,
                        Status = request.Status,
                        At = request.HandledAt ?? request.RequestedAt,
                        HandledByName = handler == null ? null : handler.FullName,
                        Note = request.RejectionReason,
                    };

        var total = await db.CountAsync(query, cancellationToken);
        var rows = await db.ToListAsync(query.OrderByDescending(row => row.At).ThenByDescending(row => row.Id).Skip((page - 1) * pageSize).Take(pageSize), cancellationToken);
        return new PagedResult<WithdrawalDto>(
            rows.Select(row => new WithdrawalDto(row.Id, row.UserId, row.FullName, row.Amount, row.Status, row.At, row.HandledByName, row.Note)).ToArray(),
            total,
            page,
            pageSize);
    }

    /// <summary>Controller đã yêu cầu <c>Balance.Handle</c>; kiểm lại để service không dựa vào nơi gọi.</summary>
    private async Task<Guid> RequireHandlerAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenAccessException();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        return access.Permissions.Contains(Permissions.BalanceHandle) ? userId : throw new ForbiddenAccessException();
    }

    // Dùng khởi tạo thuộc tính (không constructor) để EF Core dịch được phép chiếu.
    private sealed class MemberBalanceRow
    {
        public required Guid UserId { get; init; }

        public required string FullName { get; init; }

        public required string Email { get; init; }

        public required MemberRole Role { get; init; }

        public required bool IsActive { get; init; }

        public required long Balance { get; init; }
    }

    private sealed class WithdrawalRow
    {
        public required Guid Id { get; init; }

        public required Guid UserId { get; init; }

        public required string FullName { get; init; }

        public required long Amount { get; init; }

        public required WithdrawalStatus Status { get; init; }

        public required DateTimeOffset At { get; init; }

        public required string? HandledByName { get; init; }

        public required string? Note { get; init; }
    }
}
