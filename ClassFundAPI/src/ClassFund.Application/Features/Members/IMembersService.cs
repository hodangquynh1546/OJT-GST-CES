using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Paging;
using ClassFund.Domain.Exceptions;

namespace ClassFund.Application.Features.Members;

/// <summary>Quản lý thành viên quỹ (FR-09 → FR-11, AZ-02, AZ-07, BR-30, OQ-15, OQ-20). Quyền <c>Member.Manage</c> do controller kiểm tra.</summary>
public interface IMembersService
{
    Task<PagedResult<MemberDto>> ListAsync(Guid fundId, MemberFilter filter, CancellationToken cancellationToken);

    /// <summary>Thêm một email (FR-09).</summary>
    /// <exception cref="ConflictException">Đã là thành viên đang hoạt động (<c>member.already_exists</c>).</exception>
    /// <exception cref="BusinessRuleException">Email / họ tên không hợp lệ, hoặc quỹ đã tổng kết.</exception>
    Task<AddMemberResult> AddAsync(Guid fundId, string email, string? fullName, CancellationToken cancellationToken);

    /// <summary>Thêm nhiều email (OQ-20): mỗi email xử lý độc lập, lỗi một email không ảnh hưởng email khác.</summary>
    Task<BulkAddMembersResult> AddManyAsync(Guid fundId, IReadOnlyList<string> emails, CancellationToken cancellationToken);

    /// <summary>Sửa họ tên (FR-11); chỉ tài khoản do thủ quỹ cấp và chỉ ở quỹ này (AZ-07).</summary>
    Task<MemberDto> UpdateAsync(Guid fundId, Guid memberId, string fullName, CancellationToken cancellationToken);

    /// <summary>Cấp lại mật khẩu tạm (FR-10): trả MỘT lần, đổi SecurityStamp (BR-29) và bắt đổi mật khẩu.</summary>
    Task<TemporaryPasswordResult> ResetPasswordAsync(Guid fundId, Guid memberId, CancellationToken cancellationToken);

    /// <summary>Xem trước hệ quả của việc ngừng (OQ-15) để cảnh báo trước khi xác nhận.</summary>
    Task<RemovalPreviewDto> GetRemovalPreviewAsync(Guid fundId, Guid memberId, CancellationToken cancellationToken);

    /// <summary>Ngừng thành viên (FR-11, OQ-15, BR-30): giữ dữ liệu lịch sử, hủy nghĩa vụ chưa đóng của khoản đang mở.</summary>
    Task<RemoveMemberResult> RemoveAsync(Guid fundId, Guid memberId, CancellationToken cancellationToken);
}
