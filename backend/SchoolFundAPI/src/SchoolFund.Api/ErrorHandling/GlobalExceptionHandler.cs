using Microsoft.AspNetCore.Diagnostics;
using SchoolFund.Application.Common.Exceptions;
using SchoolFund.Domain.Exceptions;

namespace SchoolFund.Api.ErrorHandling;

/// <summary>Đổi exception thành ProblemDetails. Lỗi không lường trước không lộ thông tin kỹ thuật.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public const string InternalErrorCode = "internal_error";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var (status, code, title) = exception switch
        {
            BusinessRuleException rule => (StatusCodes.Status422UnprocessableEntity, rule.Code, rule.Message),
            NotFoundException notFound => (StatusCodes.Status404NotFound, "not_found", notFound.Message),
            ForbiddenAccessException forbidden => (StatusCodes.Status403Forbidden, forbidden.Code, forbidden.Message),
            ConflictException conflict => (StatusCodes.Status409Conflict, conflict.Code, conflict.Message),
            _ => (StatusCodes.Status500InternalServerError, InternalErrorCode, "Đã có lỗi xảy ra. Vui lòng thử lại."),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception");
        }

        await ApiProblems.WriteAsync(httpContext, status, code, title);
        return true;
    }
}
