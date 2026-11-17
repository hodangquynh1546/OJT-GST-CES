using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Middleware;

namespace ClassFund.Api.ErrorHandling;

/// <summary>Tạo lỗi application/problem+json với code, title, detail, traceId, correlationId.</summary>
public static class ApiProblems
{
    public const string ContentType = "application/problem+json";

    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string code,
        string title,
        string? detail = null,
        IDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.ItemKey];
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        return problem;
    }

    public static Task WriteAsync(HttpContext context, int status, string code, string title, string? detail = null)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(
            Create(context, status, code, title, detail),
            options: null,
            contentType: ContentType,
            cancellationToken: context.RequestAborted);
    }
}
