using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Domain.Authorization;

namespace ClassFund.IntegrationTests.Infrastructure;

/// <summary>
/// Endpoint chỉ có trong test, đại diện cho "mọi chức năng khác": một endpoint chỉ cần đăng nhập
/// và một endpoint theo quỹ có <c>[RequirePermission]</c>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/test-probe")]
public sealed class ProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();

    [HttpGet("funds/{fundId}")]
    [RequirePermission(Permissions.FundView)]
    public IActionResult GetFund(Guid fundId) => Ok();
}
