using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Paging;
using ClassFund.Domain.Exceptions;

namespace ClassFund.Application.Features.Payments;

/// <summary>
/// Nộp và xác nhận thanh toán khoản thu chung (FR-20 → FR-23; BR-10 → BR-12, BR-14).
/// Chỉ <c>Confirmed</c> mới sinh bút toán thu. Mỗi nghĩa vụ chỉ có một lần nộp đang chờ/đã xác nhận.
/// </summary>
public interface IPaymentsService
{
    /// <summary>Các khoản cần đóng của chính người dùng (chưa hủy), khoản chưa xong lên trước.</summary>
    Task<IReadOnlyList<MyAssignmentItemDto>> ListMineAsync(Guid fundId, CancellationToken cancellationToken);

    /// <summary>
    /// Nộp tiền cho nghĩa vụ của chính mình. Chuyển khoản bắt buộc có minh chứng (BR-11). Người có
    /// <c>Payment.Confirm</c> nộp cho mình thì tự xác nhận trong cùng thao tác, đánh dấu tự ghi nhận (BR-12).
    /// </summary>
    /// <exception cref="NotFoundException">Nghĩa vụ không phải của người dùng (AZ-04).</exception>
    /// <exception cref="ConflictException">Đã nộp / đang chờ (<c>payment.not_payable</c>) hoặc khoản thu đã đóng.</exception>
    /// <exception cref="BusinessRuleException">Thiếu minh chứng, phương thức chưa hỗ trợ, quỹ đã tổng kết.</exception>
    Task<PaymentDto> SubmitAsync(Guid fundId, Guid assignmentId, SubmitPaymentInput input, CancellationToken cancellationToken);

    /// <summary>Hàng chờ / lịch sử xử lý của thủ quỹ.</summary>
    Task<PagedResult<PaymentQueueItemDto>> ListAsync(Guid fundId, PaymentFilter filter, CancellationToken cancellationToken);

    /// <summary>Xác nhận: ghi bút toán thu <c>Source = Payment</c>, nghĩa vụ → Paid, thông báo cho người nộp.</summary>
    /// <exception cref="ConflictException">Đã được xử lý (<c>payment.already_processed</c>).</exception>
    Task<PaymentQueueItemDto> ConfirmAsync(Guid fundId, Guid paymentId, CancellationToken cancellationToken);

    /// <summary>Từ chối kèm lý do bắt buộc: nghĩa vụ trở lại Unpaid để nộp lại (BR-14).</summary>
    Task<PaymentQueueItemDto> RejectAsync(Guid fundId, Guid paymentId, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Thủ quỹ ghi nhận học sinh đã đưa TIỀN MẶT (gửi + xác nhận trong một thao tác, <c>Verified</c>): ghi bút toán thu,
    /// nghĩa vụ → Paid, thông báo cho học sinh. Ghi cho chính mình thì là tự ghi nhận (BR-12).
    /// </summary>
    /// <exception cref="ConflictException">Nghĩa vụ không còn ở trạng thái chưa đóng, hoặc khoản thu đã đóng.</exception>
    Task<PaymentDto> RecordCashAsync(Guid fundId, Guid assignmentId, CancellationToken cancellationToken);
}
