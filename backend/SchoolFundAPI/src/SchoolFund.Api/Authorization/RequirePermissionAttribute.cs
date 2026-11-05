using Microsoft.AspNetCore.Authorization;

namespace SchoolFund.Api.Authorization;

/// <summary>Yêu cầu một permission; quỹ lấy từ route <c>fundId</c>, không có thì xét quyền cấp tài khoản.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string Permission { get; } = permission;

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PermissionRequirement(Permission);
    }
}
