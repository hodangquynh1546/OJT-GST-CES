using System.Net;
using ClassFund.Api.Middleware;
using ClassFund.Application.Common.Auditing;

namespace ClassFund.Api.Hosting;

/// <summary>Correlation ID và IP của request hiện tại, ghi kèm AuditLog.</summary>
public sealed class HttpRequestInfo(IHttpContextAccessor httpContextAccessor) : IRequestInfo
{
    public string? CorrelationId =>
        httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string;

    public IPAddress? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
}
