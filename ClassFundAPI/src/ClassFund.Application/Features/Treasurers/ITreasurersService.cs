using ClassFund.Application.Common.Exceptions;
using ClassFund.Domain.Exceptions;

namespace ClassFund.Application.Features.Treasurers;

/// <summary>
/// Phân quyền / thu hồi thủ quỹ (FR-12, FR-13, AZ-02, AZ-03). Chỉ thủ quỹ chính (<c>Treasurer.Manage</c>, kiểm ở controller).
/// Đổi vai trò có hiệu lực ngay ở request kế tiếp: <c>AccessService</c> đọc DB mỗi request nên không cần đăng nhập lại.
/// </summary>
public interface ITreasurersService
{
    /// <summary>Đội thủ quỹ: thủ quỹ chính rồi các thủ quỹ được phân quyền (đang hoạt động).</summary>
    Task<IReadOnlyList<TreasurerDto>> ListAsync(Guid fundId, CancellationToken cancellationToken);

    /// <summary>Nâng một học sinh đang hoạt động của quỹ thành thủ quỹ.</summary>
    /// <exception cref="NotFoundException">Không có thành viên này trong quỹ.</exception>
    /// <exception cref="ConflictException">Đã là thủ quỹ (<c>treasurer.already_treasurer</c>).</exception>
    /// <exception cref="BusinessRuleException">Thành viên đã bị ngừng, hoặc quỹ đã tổng kết.</exception>
    Task<TreasurerDto> GrantAsync(Guid fundId, Guid memberId, CancellationToken cancellationToken);

    /// <summary>Đưa một thủ quỹ về học sinh.</summary>
    /// <exception cref="BusinessRuleException">Là thủ quỹ chính (<c>treasurer.cannot_revoke_owner</c>, AZ-03).</exception>
    /// <exception cref="ConflictException">Không phải thủ quỹ (<c>treasurer.not_treasurer</c>).</exception>
    Task<TreasurerDto> RevokeAsync(Guid fundId, Guid memberId, CancellationToken cancellationToken);
}
