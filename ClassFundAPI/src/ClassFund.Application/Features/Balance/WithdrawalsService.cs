using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.AuditLogs;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.Application.Features.Balance;

/// <summary>
/// B-02: thủ quỹ xử lý yêu cầu rút toàn bộ số dư (FR-41, BR-26). "Đã trả tiền" trừ đúng số tiền yêu cầu + BalanceTransaction(Withdrawal);
/// "Từ chối" cần lý do, số dư mở lại. Không ghi LedgerEntry (tiền giữ hộ không thuộc quỹ chung / sự kiện).
/// </summary>
public interface IWithdrawalsService
{
    Task<WithdrawalListResult> ListAsync(Guid fundId, WithdrawalStatus? status, CancellationToken cancellationToken);

    Task<WithdrawalRequestDto> MarkPaidAsync(Guid fundId, Guid requestId, CancellationToken cancellationToken);

    Task<WithdrawalRequestDto> RejectAsync(Guid fundId, Guid requestId, string reason, CancellationToken cancellationToken);
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
    public async Task<WithdrawalListResult> ListAsync(Guid fundId, WithdrawalStatus? status, CancellationToken cancellationToken)
    {
        var actorId = await RequireHandlerAsync(fundId, cancellationToken);
        var rows = await QueryAsync(fundId, actorId, null, cancellationToken);
        var items = status is { } wanted ? rows.Where(row => row.Status == wanted).ToArray() : rows;
        return new WithdrawalListResult(
            items,
            rows.Count(row => row.Status == WithdrawalStatus.Requested),
            rows.Count(row => row.Status == WithdrawalStatus.Paid),
            rows.Count(row => row.Status == WithdrawalStatus.Rejected));
    }

    public async Task<WithdrawalRequestDto> MarkPaidAsync(Guid fundId, Guid requestId, CancellationToken cancellationToken)
    {
        var actorId = await RequireHandlerAsync(fundId, cancellationToken);
        await db.InTransactionAsync(
            async token =>
            {
                var request = await LockPendingAsync(fundId, requestId, token);
                var after = await balances.DebitAsync(fundId, request.UserId, request.Amount, BalanceTransactionType.Withdrawal, request.Id, actorId, token);

                request.Status = WithdrawalStatus.Paid;
                request.HandledByUserId = actorId;
                request.HandledAt = clock.GetUtcNow();
                audit.Record(new AuditEntry(
                    BalanceAuditActions.WithdrawalPaid,
                    fundId,
                    actorId,
                    FinancialScope.System,
                    EntityType: nameof(WithdrawalRequest),
                    EntityId: request.Id,
                    Before: new { status = nameof(WithdrawalStatus.Requested) },
                    After: new { status = nameof(WithdrawalStatus.Paid), request.Amount, balance = after },
                    IsSelfAction: actorId == request.UserId));
                notifier.Send(
                    [request.UserId],
                    new NotificationMessage(
                        fundId,
                        FinancialScope.System,
                        null,
                        NotificationType.WithdrawalProcessed,
                        "Thủ quỹ đã trả tiền rút số dư",
                        $"Bạn đã nhận {AuditSummary.FormatMoney(request.Amount)}. Số dư cá nhân còn {AuditSummary.FormatMoney(after)}.",
                        nameof(WithdrawalRequest),
                        request.Id));
                await db.SaveChangesAsync(token);
                return request;
            },
            cancellationToken);

        return (await QueryAsync(fundId, actorId, requestId, cancellationToken)).Single();
    }

    public async Task<WithdrawalRequestDto> RejectAsync(Guid fundId, Guid requestId, string reason, CancellationToken cancellationToken)
    {
        var actorId = await RequireHandlerAsync(fundId, cancellationToken);
        var note = string.IsNullOrWhiteSpace(reason)
            ? throw new BusinessRuleException(BalanceErrors.ReasonRequired, BalanceErrors.ReasonRequiredMessage)
            : reason.Trim();

        await db.InTransactionAsync(
            async token =>
            {
                var request = await LockPendingAsync(fundId, requestId, token);
                request.Status = WithdrawalStatus.Rejected;
                request.HandledByUserId = actorId;
                request.HandledAt = clock.GetUtcNow();
                request.RejectionReason = note;
                audit.Record(new AuditEntry(
                    BalanceAuditActions.WithdrawalReject,
                    fundId,
                    actorId,
                    FinancialScope.System,
                    EntityType: nameof(WithdrawalRequest),
                    EntityId: request.Id,
                    Before: new { status = nameof(WithdrawalStatus.Requested) },
                    After: new { status = nameof(WithdrawalStatus.Rejected), request.Amount },
                    IsSelfAction: actorId == request.UserId,
                    Note: note));
                notifier.Send(
                    [request.UserId],
                    new NotificationMessage(
                        fundId,
                        FinancialScope.System,
                        null,
                        NotificationType.WithdrawalProcessed,
                        "Yêu cầu rút số dư bị từ chối",
                        $"Lý do: {note}. Số dư của bạn đã được mở lại.",
                        nameof(WithdrawalRequest),
                        request.Id));
                await db.SaveChangesAsync(token);
                return request;
            },
            cancellationToken);

        return (await QueryAsync(fundId, actorId, requestId, cancellationToken)).Single();
    }

    private async Task<WithdrawalRequest> LockPendingAsync(Guid fundId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.LockAsync<WithdrawalRequest>(requestId, cancellationToken);
        if (request is null || request.FundId != fundId)
        {
            throw new NotFoundException();
        }

        return request.Status == WithdrawalStatus.Requested
            ? request
            : throw new ConflictException(BalanceErrors.NotPending, BalanceErrors.NotPendingMessage);
    }

    private async Task<IReadOnlyList<WithdrawalRequestDto>> QueryAsync(Guid fundId, Guid actorId, Guid? onlyId, CancellationToken cancellationToken)
    {
        var requests = db.Query<WithdrawalRequest>().Where(request => request.FundId == fundId);
        if (onlyId is { } id)
        {
            requests = requests.Where(request => request.Id == id);
        }

        var rows = await db.ToListAsync(
            from request in requests
            join user in db.Query<User>() on request.UserId equals user.Id
            join item in db.Query<User>() on request.HandledByUserId equals (Guid?)item.Id into handlers
            from handler in handlers.DefaultIfEmpty()
            select new { request, user.FullName, user.Email, HandledByName = handler == null ? null : handler.FullName },
            cancellationToken);

        return rows
            .OrderBy(row => row.request.Status == WithdrawalStatus.Requested ? 0 : 1)
            .ThenByDescending(row => row.request.HandledAt ?? row.request.RequestedAt)
            .Select(row => new WithdrawalRequestDto(
                row.request.Id,
                row.request.UserId,
                row.FullName,
                row.Email,
                row.request.Amount,
                row.request.Status,
                row.request.RequestedAt,
                row.request.HandledAt,
                row.HandledByName,
                row.request.RejectionReason,
                row.request.UserId == actorId))
            .ToArray();
    }

    /// <summary>Controller đã yêu cầu <c>Balance.Handle</c>; kiểm lại để service không dựa vào nơi gọi.</summary>
    private async Task<Guid> RequireHandlerAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenAccessException();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        return access.Permissions.Contains(Permissions.BalanceHandle) ? userId : throw new ForbiddenAccessException();
    }
}
