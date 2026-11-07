using System.Text.RegularExpressions;
using Serilog.Context;

namespace ClassFund.Api.Middleware;

/// <summary>Gắn mã tương quan cho mỗi request: nhận từ client nếu hợp lệ, không thì tự sinh.</summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        string? supplied = context.Request.Headers[HeaderName];
        var correlationId = supplied is not null && SafeValue().IsMatch(supplied)
            ? supplied
            : Guid.CreateVersion7().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(ItemKey, correlationId))
        {
            await next(context);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex SafeValue();
}
