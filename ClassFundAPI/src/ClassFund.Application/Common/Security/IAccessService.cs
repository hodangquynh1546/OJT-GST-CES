using ClassFund.Domain.Authorization;

namespace ClassFund.Application.Common.Security;

/// <summary>Vai trò và permission của một người dùng trong một quỹ.</summary>
public sealed record FundAccess(Guid FundId, FundRole Role, IReadOnlySet<string> Permissions);

/// <summary>Một quỹ người dùng có vai trò; <see cref="LinkedStudents"/> là họ tên con được liên kết (chỉ với phụ huynh).</summary>
public sealed record FundMembership(
    Guid FundId,
    string FundName,
    FundRole Role,
    IReadOnlySet<string> Permissions,
    IReadOnlyList<string> LinkedStudents);

/// <summary>Suy ra quyền từ dữ liệu theo docs/business-analysis/roles-and-permissions.md mục 1.</summary>
public interface IAccessService
{
    /// <summary>Quyền cấp tài khoản, không gắn với quỹ (ví dụ <c>Fund.Create</c>).</summary>
    Task<IReadOnlySet<string>> GetAccountPermissionsAsync(Guid userId, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.NotFoundException">Quỹ không tồn tại hoặc người dùng không thuộc quỹ.</exception>
    Task<FundAccess> GetFundAccessAsync(Guid userId, Guid fundId, CancellationToken cancellationToken);

    /// <summary>Mọi quỹ người dùng có vai trò, sắp theo tên quỹ (cho <c>GET /auth/me</c>).</summary>
    Task<IReadOnlyList<FundMembership>> GetMembershipsAsync(Guid userId, CancellationToken cancellationToken);
}
