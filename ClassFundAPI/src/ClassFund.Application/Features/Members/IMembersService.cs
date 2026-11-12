namespace ClassFund.Application.Features.Members;

/// <summary>Danh sách thành viên quỹ cho GVCN và thủ quỹ (<c>Member.View</c>). FR-12.</summary>
public interface IMembersService
{
    /// <summary>
    /// GVCN, thủ quỹ, học sinh (gồm cả học sinh chưa có tài khoản); không gồm phụ huynh. Thứ tự: GVCN, thủ quỹ, học sinh,
    /// trong mỗi nhóm theo họ tên. Học sinh đang là thủ quỹ chỉ xuất hiện một lần, vai trò <c>Treasurer</c>.
    /// </summary>
    /// <exception cref="Common.Exceptions.ForbiddenAccessException">Không có <c>Member.View</c>.</exception>
    Task<IReadOnlyList<MemberResponse>> ListAsync(Guid fundId, CancellationToken cancellationToken);
}
