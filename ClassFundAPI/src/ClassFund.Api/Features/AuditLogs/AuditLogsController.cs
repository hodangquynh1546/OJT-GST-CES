using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Features.AuditLogs;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.AuditLogs;

/// <summary>M12 – Nhật ký hoạt động (FR-45 → FR-47). Chỉ đọc; học sinh không có <c>Audit.View</c> → 403.</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/audit-logs")]
[RequirePermission(Permissions.AuditView)]
public sealed class AuditLogsController(IAuditLogsService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<AuditLogDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<AuditLogDto>> List(Guid fundId, [FromQuery] AuditLogListRequest request, CancellationToken ct) =>
        service.ListAsync(
            fundId,
            new AuditLogFilter(
                request.From,
                request.To,
                request.ActorId,
                request.Scope,
                request.EventId,
                request.Action,
                request.SelfOnly,
                request.Page,
                request.PageSize),
            ct);

    /// <summary>Người đã thao tác trong quỹ – cho ô lọc "Người thực hiện".</summary>
    [HttpGet("actors")]
    [ProducesResponseType<IReadOnlyList<AuditActorDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AuditActorDto>> Actors(Guid fundId, CancellationToken ct) => service.ListActorsAsync(fundId, ct);
}
