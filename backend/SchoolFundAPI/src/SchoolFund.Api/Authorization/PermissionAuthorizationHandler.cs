using Microsoft.AspNetCore.Authorization;
using SchoolFund.Application.Common.Exceptions;
using SchoolFund.Application.Common.Security;

namespace SchoolFund.Api.Authorization;

/// <summary>
/// Thứ tự kiểm tra theo roles-and-permissions.md mục 3: không thuộc quỹ → 404,
/// còn mật khẩu tạm → 403 password.change_required, thiếu permission → 403 forbidden.
/// </summary>
public sealed class PermissionAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ICurrentUser currentUser,
    IAccessService accessService) : AuthorizationHandler<PermissionRequirement>
{
    public const string FundIdRouteKey = "fundId";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!currentUser.IsAuthenticated)
        {
            return;
        }

        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Không có HttpContext khi kiểm tra quyền.");
        var cancellationToken = httpContext.RequestAborted;
        var routeFundId = httpContext.GetRouteValue(FundIdRouteKey)?.ToString();

        var permissions = routeFundId is null
            ? await accessService.GetAccountPermissionsAsync(cancellationToken)
            : await GetFundPermissionsAsync(routeFundId, cancellationToken);

        if (currentUser.MustChangePassword)
        {
            throw new ForbiddenAccessException(
                ForbiddenAccessException.PasswordChangeRequiredCode,
                "Bạn cần đặt mật khẩu mới trước khi tiếp tục.");
        }

        if (!permissions.Contains(requirement.Permission))
        {
            throw new ForbiddenAccessException();
        }

        context.Succeed(requirement);
    }

    private async Task<IReadOnlySet<string>> GetFundPermissionsAsync(string routeFundId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(routeFundId, out var fundId))
        {
            throw new NotFoundException();
        }

        var access = await accessService.GetFundAccessAsync(fundId, cancellationToken)
            ?? throw new NotFoundException();
        return access.Permissions;
    }
}
