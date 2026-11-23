using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Features.Events;
using ClassFund.Domain.Exceptions;

namespace ClassFund.Application.Features.Charges;

/// <summary>
/// Khoản thu chung của quỹ (FR-18, FR-24; BR-10). Phạm vi M6a là quỹ chung (<c>EventId = null</c>);
/// khoản thu sự kiện do M6b mở rộng.
/// </summary>
public interface IChargesService
{
    /// <summary>Danh sách khoản thu. Mọi thành viên xem được; chỉ người có <c>Charge.Manage</c> thấy tiến độ thu.</summary>
    Task<PagedResult<ChargeDto>> ListAsync(Guid fundId, ChargeFilter filter, CancellationToken cancellationToken);

    /// <exception cref="NotFoundException">Không có khoản thu chung này trong quỹ.</exception>
    Task<ChargeDto> GetAsync(Guid fundId, Guid chargeId, CancellationToken cancellationToken);

    /// <summary>
    /// Tạo khoản thu và nghĩa vụ cho MỌI thành viên đang hoạt động (gồm thủ quỹ); gửi thông báo <c>GeneralChargeCreated</c>.
    /// </summary>
    /// <exception cref="BusinessRuleException">Quỹ đã tổng kết, số tiền sai, hạn đóng đã qua, danh mục không dùng được (<c>category.unusable</c>: không tồn tại, đã ngừng hoặc không phải loại Thu).</exception>
    Task<ChargeDto> CreateAsync(Guid fundId, CreateChargeInput input, CancellationToken cancellationToken);

    /// <summary>Danh sách đã đóng / chưa đóng của một khoản thu.</summary>
    Task<PagedResult<AssignmentRowDto>> ListAssignmentsAsync(Guid fundId, Guid chargeId, AssignmentFilter filter, CancellationToken cancellationToken);

    /// <summary>Ngừng nhận tiền: không còn nộp mới, khoản đang chờ xác nhận vẫn xử lý được.</summary>
    /// <exception cref="ConflictException">Khoản thu không còn mở (<c>charge.invalid_status</c>).</exception>
    Task<ChargeDto> CloseAsync(Guid fundId, Guid chargeId, CancellationToken cancellationToken);

    /// <summary>Hủy khoản thu tạo nhầm: chỉ khi chưa có ai nộp (<c>charge.has_payments</c>).</summary>
    Task<ChargeDto> CancelAsync(Guid fundId, Guid chargeId, CancellationToken cancellationToken);

    /// <summary>
    /// Gửi thông báo nhắc cho người CHƯA đóng của khoản thu đang mở (không nhắc người đang chờ xác nhận).
    /// Mỗi khoản thu nhắc tối đa một lần mỗi giờ.
    /// </summary>
    /// <exception cref="ConflictException">Khoản thu không còn mở hoặc vừa nhắc (<c>charge.remind_too_soon</c>).</exception>
    /// <exception cref="BusinessRuleException">Không còn ai chưa đóng (<c>charge.nothing_to_remind</c>).</exception>
    Task<RemindResult> RemindAsync(Guid fundId, Guid chargeId, CancellationToken cancellationToken);

    /// <summary>
    /// "Nhắc đóng" một người tham gia sự kiện còn khoản chưa đóng (E-03): thông báo <c>EventUnpaidReminder</c> chỉ cho người đó;
    /// 409 <c>charge.remind_too_soon</c> (1 giờ / người / sự kiện), 422 <c>charge.nothing_to_remind</c>.
    /// </summary>
    Task<RemindResult> RemindEventParticipantAsync(Guid fundId, Guid eventId, Guid userId, CancellationToken cancellationToken);

    // ---- Khoản thu sự kiện (M6b) ----

    /// <summary>
    /// Tạo khoản thu sự kiện, chỉ gán cho người đã xác nhận tham gia (<c>Accepted</c>, BR-18);
    /// gửi thông báo <c>EventChargeCreated</c> chỉ đến những người được gán.
    /// </summary>
    /// <exception cref="BusinessRuleException">Sự kiện không còn mở, danh mục không dùng được, hạn đóng đã qua.</exception>
    Task<EventChargeDto> CreateEventChargeAsync(Guid fundId, Guid eventId, CreateChargeInput input, CancellationToken cancellationToken);

    /// <summary>Ngừng nhận tiền cho khoản thu sự kiện.</summary>
    Task<EventChargeDto> CloseEventChargeAsync(Guid fundId, Guid eventId, Guid chargeId, CancellationToken cancellationToken);

    /// <summary>Hủy khoản thu sự kiện: chỉ khi chưa có ai nộp.</summary>
    Task<EventChargeDto> CancelEventChargeAsync(Guid fundId, Guid eventId, Guid chargeId, CancellationToken cancellationToken);

    /// <summary>Danh sách đã đóng / chưa đóng của một khoản thu sự kiện (tương tự general).</summary>
    Task<PagedResult<AssignmentRowDto>> ListEventAssignmentsAsync(Guid fundId, Guid eventId, Guid chargeId, AssignmentFilter filter, CancellationToken cancellationToken);
}
