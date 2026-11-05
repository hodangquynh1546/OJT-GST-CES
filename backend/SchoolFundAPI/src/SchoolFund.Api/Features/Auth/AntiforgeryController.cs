using Asp.Versioning;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SchoolFund.Api.Features.Auth;

public sealed record AntiforgeryResponse(string AntiforgeryToken);

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AntiforgeryController(IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>Cấp token antiforgery để gửi trong header X-XSRF-TOKEN.</summary>
    [HttpGet("antiforgery")]
    [AllowAnonymous]
    public ActionResult<AntiforgeryResponse> Get()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryResponse(tokens.RequestToken!));
    }
}
