using System.Text.Json;
using SchoolFund.Api.Middleware;
using SchoolFund.Application.Common.Auditing;
using SchoolFund.Application.Common.Persistence;
using SchoolFund.Application.Common.Security;
using SchoolFund.Domain.Entities;
using SchoolFund.Domain.Enums;

namespace SchoolFund.Api.Auditing;

/// <summary>Lấy người thực hiện, correlation id và IP từ request hiện tại.</summary>
public sealed class HttpAuditWriter(
    IAppDb db,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider) : IAuditWriter
{
    public void Record(
        AuditCategory category,
        string action,
        string entityType,
        string entityId,
        object? oldValues,
        object? newValues)
    {
        var httpContext = httpContextAccessor.HttpContext;

        db.Add(new AuditLog(
            timeProvider.GetUtcNow(),
            currentUser.UserId,
            category,
            action,
            entityType,
            entityId,
            ToJson(oldValues),
            ToJson(newValues),
            httpContext?.Items[CorrelationIdMiddleware.ItemKey] as string,
            httpContext?.Connection.RemoteIpAddress));
    }

    private static string? ToJson(object? values) =>
        values is null ? null : JsonSerializer.Serialize(values, JsonSerializerOptions.Web);
}
