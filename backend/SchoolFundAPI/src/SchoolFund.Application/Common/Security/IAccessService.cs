using SchoolFund.Domain.Authorization;

namespace SchoolFund.Application.Common.Security;

/// <summary>Vai trò và permission của người dùng hiện tại trong một quỹ.</summary>
public sealed record FundAccess(Guid FundId, FundRole Role, IReadOnlySet<string> Permissions);

/// <summary>Suy ra quyền từ dữ liệu theo docs/business-analysis/roles-and-permissions.md mục 1.</summary>
public interface IAccessService
{
    /// <summary>Quyền cấp tài khoản, không gắn với quỹ (ví dụ <c>Fund.Create</c>).</summary>
    Task<IReadOnlySet<string>> GetAccountPermissionsAsync(CancellationToken cancellationToken);

    /// <returns><c>null</c> khi người dùng không thuộc quỹ.</returns>
    Task<FundAccess?> GetFundAccessAsync(Guid fundId, CancellationToken cancellationToken);
}
