using SchoolFund.Domain.Authorization;

namespace SchoolFund.Application.Common.Security;

/// <summary>Vai trò và permission của một người dùng trong một quỹ.</summary>
public sealed record FundAccess(Guid FundId, FundRole Role, IReadOnlySet<string> Permissions);

/// <summary>Suy ra quyền từ dữ liệu theo docs/business-analysis/roles-and-permissions.md mục 1.</summary>
public interface IAccessService
{
    /// <summary>Quyền cấp tài khoản, không gắn với quỹ (ví dụ <c>Fund.Create</c>).</summary>
    Task<IReadOnlySet<string>> GetAccountPermissionsAsync(Guid userId, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.NotFoundException">Quỹ không tồn tại hoặc người dùng không thuộc quỹ.</exception>
    Task<FundAccess> GetFundAccessAsync(Guid userId, Guid fundId, CancellationToken cancellationToken);
}
