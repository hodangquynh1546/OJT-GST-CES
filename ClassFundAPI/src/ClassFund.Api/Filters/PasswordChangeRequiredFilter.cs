using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ClassFund.Api.Authorization;
using ClassFund.Api.ErrorHandling;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Security;

namespace ClassFund.Api.Filters;

/// <summary>
/// Tài khoản còn mật khẩu tạm chỉ được gọi endpoint ẩn danh hoặc có <see cref="AllowTemporaryPasswordAttribute"/>;
/// còn lại trả 403 <c>password.change_required</c>. Endpoint có <c>[RequirePermission]</c> đã được
/// <see cref="PermissionAuthorizationHandler"/> chặn trước (sau bước kiểm tra thuộc quỹ → 404).
/// </summary>
public sealed class PasswordChangeRequiredFilter(ICurrentUser currentUser) : IAuthorizationFilter
{
    public const string Message = "Bạn cần đặt mật khẩu mới trước khi tiếp tục.";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!currentUser.MustChangePassword)
        {
            return;
        }

        var metadata = context.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any() || metadata.OfType<AllowTemporaryPasswordAttribute>().Any())
        {
            return;
        }

        var problem = ApiProblems.Create(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            ForbiddenAccessException.PasswordChangeRequiredCode,
            Message);
        context.Result = new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status403Forbidden,
            ContentTypes = { ApiProblems.ContentType },
        };
    }
}
