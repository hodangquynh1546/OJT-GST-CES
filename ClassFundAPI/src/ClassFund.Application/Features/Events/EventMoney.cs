using ClassFund.Application.Common.Persistence;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Events;

/// <summary>
/// Tiền của một người với cả sự kiện, tính từ nghĩa vụ (ChargeAssignment) và thanh toán (Payment) của các khoản thu sự kiện.
/// Chỉ đọc – khoản thu / thanh toán do M6 ghi.
/// </summary>
public sealed record ParticipantMoney(
    Guid EventId,
    Guid UserId,
    long Due,
    long Paid,
    long UnpaidAmount,
    int PendingCount,
    bool HasSelfDeclared,
    bool HasAssignment,
    bool HasAnyPayment)
{
    public static ParticipantMoney None(Guid eventId, Guid userId) => new(eventId, userId, 0, 0, 0, 0, false, false, false);

    /// <summary>Chờ xác nhận ưu tiên; chưa có khoản nào → NoCharge; còn nghĩa vụ Unpaid → Chưa đóng; còn lại Đã đóng.</summary>
    public ParticipantPaymentStatus Status =>
        PendingCount > 0 ? ParticipantPaymentStatus.Pending
        : !HasAssignment ? ParticipantPaymentStatus.NoCharge
        : UnpaidAmount > 0 ? ParticipantPaymentStatus.Unpaid
        : ParticipantPaymentStatus.Paid;

    /// <summary>
    /// BR-19 (nhóm chốt 2026-11-19): bị loại khi kết thúc nếu chưa đóng khoản thu nào của sự kiện
    /// (không có khoản Paid, không có thanh toán chờ / đã xác nhận) – kể cả khi sự kiện chưa áp khoản thu nào cho người này.
    /// Đã đóng ít nhất một khoản thì giữ lại, đóng đủ hay chưa đều được (quyết toán chia tiền thừa theo tỷ lệ số đã đóng).
    /// </summary>
    public bool ExcludedOnCompletion => !HasAnyPayment;
}

public static class EventMoney
{
    /// <summary>Tiền theo (sự kiện, người) cho các sự kiện cho trước. Bỏ khoản thu / nghĩa vụ đã hủy và thanh toán bị từ chối.</summary>
    public static async Task<IReadOnlyDictionary<(Guid EventId, Guid UserId), ParticipantMoney>> LoadAsync(
        IAppDb db,
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken)
    {
        if (eventIds.Count == 0)
        {
            return new Dictionary<(Guid, Guid), ParticipantMoney>();
        }

        var ids = eventIds.ToArray();
        var assignments = await db.ToListAsync(
            from assignment in db.Query<ChargeAssignment>()
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.EventId != null && ids.Contains(charge.EventId.Value)
                && charge.Status != ChargeStatus.Cancelled
                && assignment.Status != AssignmentStatus.Cancelled
            select new { EventId = charge.EventId!.Value, assignment.UserId, assignment.AmountDue, assignment.Status },
            cancellationToken);

        var payments = await db.ToListAsync(
            from payment in db.Query<Payment>()
            join assignment in db.Query<ChargeAssignment>() on payment.AssignmentId equals assignment.Id
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.EventId != null && ids.Contains(charge.EventId.Value)
                && charge.Status != ChargeStatus.Cancelled
                && payment.Status != PaymentStatus.Rejected
            select new { EventId = charge.EventId!.Value, assignment.UserId, payment.Amount, payment.Status, payment.ConfirmationMode },
            cancellationToken);

        var keys = assignments.Select(row => (row.EventId, row.UserId))
            .Concat(payments.Select(row => (row.EventId, row.UserId)))
            .Distinct();

        var result = new Dictionary<(Guid EventId, Guid UserId), ParticipantMoney>();
        foreach (var key in keys)
        {
            var mine = assignments.Where(row => row.EventId == key.EventId && row.UserId == key.UserId).ToArray();
            var paid = payments.Where(row => row.EventId == key.EventId && row.UserId == key.UserId).ToArray();
            result[key] = new ParticipantMoney(
                key.EventId,
                key.UserId,
                Due: mine.Where(row => row.Status != AssignmentStatus.Excluded).Sum(row => row.AmountDue),
                Paid: paid.Where(row => row.Status == PaymentStatus.Confirmed).Sum(row => row.Amount),
                UnpaidAmount: mine.Where(row => row.Status == AssignmentStatus.Unpaid).Sum(row => row.AmountDue),
                PendingCount: paid.Count(row => row.Status == PaymentStatus.Submitted),
                HasSelfDeclared: paid.Any(row => row.Status == PaymentStatus.Confirmed && row.ConfirmationMode == ConfirmationMode.SelfDeclared),
                HasAssignment: mine.Any(row => row.Status != AssignmentStatus.Excluded),
                HasAnyPayment: paid.Length > 0 || mine.Any(row => row.Status == AssignmentStatus.Paid));
        }

        return result;
    }

    public static ParticipantMoney For(IReadOnlyDictionary<(Guid EventId, Guid UserId), ParticipantMoney> money, Guid eventId, Guid userId) =>
        money.TryGetValue((eventId, userId), out var value) ? value : ParticipantMoney.None(eventId, userId);
}
