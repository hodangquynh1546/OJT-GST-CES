using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SchoolFund.Api.ErrorHandling;

namespace SchoolFund.Api.Filters;

/// <summary>Bắt buộc header X-XSRF-TOKEN hợp lệ cho mọi request ghi.</summary>
public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public const string FailedCode = "antiforgery_failed";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            var problem = ApiProblems.Create(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                FailedCode,
                "Phiên làm việc không hợp lệ. Vui lòng tải lại trang.");
            context.Result = new BadRequestObjectResult(problem) { ContentTypes = { ApiProblems.ContentType } };
        }
    }
}
