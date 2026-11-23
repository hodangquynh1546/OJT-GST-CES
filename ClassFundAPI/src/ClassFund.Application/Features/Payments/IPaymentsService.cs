using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Features.Charges;
using ClassFund.Domain.Exceptions;

namespace ClassFund.Application.Features.Payments;

/// <summary>
/// Thu tiền khoản thu chung và khoản thu sự kiện (mentor, 2026-11): học sinh chỉ xem; thủ quỹ tích "đã thu" kèm hình thức
/// Tiền mặt / Chuyển khoản. Tích cho chính mình = tự ghi nhận (BR-12). Mỗi lần tích sinh bút toán thu đúng phạm vi.
/// </summary>
public interface IPaymentsService
{
    /// <summary>Các khoản cần đóng của chính người dùng (chưa hủy), khoản chưa nộp lên trước.</summary>
    Task<IReadOnlyList<MyAssignmentItemDto>> ListMineAsync(Guid fundId, CancellationToken cancellationToken);

    /// <summary>
    /// Thủ quỹ ghi nhận người này đã nộp: Payment <c>Confirmed</c>, bút toán thu, nghĩa vụ → Paid, thông báo cho người nộp.
    /// </summary>
    /// <exception cref="ConflictException">Đã ghi nhận thu (<c>payment.not_payable</c>) hoặc khoản thu / sự kiện đã đóng.</exception>
    /// <exception cref="BusinessRuleException">Hình thức không hợp lệ, quỹ đã tổng kết.</exception>
    Task<PaymentDto> CollectAsync(Guid fundId, Guid assignmentId, CollectPaymentInput input, CancellationToken cancellationToken);

    /// <summary>
    /// Hủy ghi nhận thu (tích nhầm): sinh bút toán đảo, nghĩa vụ về Chưa nộp. Chỉ khi khoản thu và sự kiện còn mở.
    /// </summary>
    Task<PaymentDto> UncollectAsync(Guid fundId, Guid assignmentId, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// "Nhắc đóng" một người còn khoản chưa nộp (khoản thu chung hoặc sự kiện): thông báo riêng cho người đó, ghi lịch sử nhắc và audit.
    /// 409 <c>charge.remind_too_soon</c> (1 giờ / người / khoản), 422 <c>charge.nothing_to_remind</c> khi đã nộp.
    /// </summary>
    Task<RemindResult> RemindAsync(Guid fundId, Guid assignmentId, CancellationToken cancellationToken);
}
