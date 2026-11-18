using Asp.Versioning;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Api.Hosting;
using ClassFund.Application.Features.Auth;

namespace ClassFund.Api.Features.Auth;

/// <summary>Đăng ký, đăng nhập, phiên và tài khoản của chính người dùng (FR-01 → FR-04).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Prefix + "/auth")]
public sealed class AuthController(IAuthService service, IAntiforgery antiforgery) : ApiControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var session = await service.RegisterAsync(request.FullName, request.Email, request.Password, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, await StartSessionAsync(session));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    public async Task<SessionResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        await StartSessionAsync(await service.LoginAsync(request.Email, request.Password, cancellationToken));

    [HttpPost("logout")]
    [AllowTemporaryPassword]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> Logout()
    {
        await SessionCookie.SignOutAsync(HttpContext);
        return NoContent();
    }

    [HttpGet("me")]
    [AllowTemporaryPassword]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    public Task<CurrentUserDto> Me(CancellationToken cancellationToken) => service.GetMeAsync(cancellationToken);

    [HttpPut("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    public Task<CurrentUserDto> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        service.UpdateProfileAsync(request.FullName, cancellationToken);

    [HttpPost("change-password")]
    [AllowTemporaryPassword]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    public async Task<SessionResponse> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken) =>
        await StartSessionAsync(await service.ChangePasswordAsync(request.CurrentPassword, request.NewPassword, cancellationToken));

    /// <summary>Phát cookie phiên mới rồi cấp token antiforgery gắn với phiên đó (token cũ của người ẩn danh không còn dùng được).</summary>
    private async Task<SessionResponse> StartSessionAsync(AuthSession session)
    {
        await SessionCookie.SignInAsync(HttpContext, session.Ticket);
        return new SessionResponse(antiforgery.GetAndStoreTokens(HttpContext).RequestToken!, session.User);
    }
}
