using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Paging;
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
    /// <exception cref="BusinessRuleException">Quỹ đã tổng kết, số tiền sai, hạn đóng đã qua.</exception>
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
}
