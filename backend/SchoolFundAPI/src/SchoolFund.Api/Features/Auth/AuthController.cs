using Asp.Versioning;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SchoolFund.Api.Authorization;
using SchoolFund.Api.Hosting;
using SchoolFund.Application.Features.Auth;

namespace SchoolFund.Api.Features.Auth;

/// <summary>Đăng ký giáo viên, đăng nhập một ô, tài khoản cá nhân, đổi mật khẩu, đăng xuất (docs/api/backend-api.md mục Auth).</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(AuthService authService, IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>Giáo viên tự đăng ký; đăng nhập luôn sau khi đăng ký.</summary>
    [HttpPost("register-teacher")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SessionResponse>> RegisterTeacher(
        RegisterTeacherRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterTeacherAsync(
            new RegisterTeacherCommand(request.FullName!, request.Email!, request.Password!),
            cancellationToken);
        return Created("me", await StartSessionAsync(result));
    }

    /// <summary>Đăng nhập bằng email hoặc tên đăng nhập; không chọn vai trò.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<SessionResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.EffectiveLogin, request.Password!, cancellationToken);
        return Ok(await StartSessionAsync(result));
    }

    [HttpPost("logout")]
    [AllowTemporaryPassword]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await SessionCookie.SignOutAsync(HttpContext);
        return NoContent();
    }

    [HttpGet("me")]
    [AllowTemporaryPassword]
    public async Task<ActionResult<MeResponse>> GetMe(CancellationToken cancellationToken) =>
        MeResponse.From(await authService.GetMeAsync(cancellationToken));

    [HttpPut("me")]
    [AllowTemporaryPassword]
    public async Task<ActionResult<MeResponse>> UpdateMe(UpdateMeRequest request, CancellationToken cancellationToken) =>
        MeResponse.From(await authService.UpdateMeAsync(request.FullName!, cancellationToken));

    /// <summary>Đổi mật khẩu (kể cả mật khẩu tạm): phiên hiện tại nhận cookie mới, các phiên khác hết hiệu lực.</summary>
    [HttpPost("change-password")]
    [AllowTemporaryPassword]
    public async Task<ActionResult<SessionResponse>> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.ChangePasswordAsync(
            new ChangePasswordCommand(request.CurrentPassword!, request.NewPassword!),
            cancellationToken);
        return Ok(await StartSessionAsync(result));
    }

    private async Task<SessionResponse> StartSessionAsync(AuthSessionResult result)
    {
        await SessionCookie.SignInAsync(HttpContext, result.Session);
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return new SessionResponse(tokens.RequestToken!, MeResponse.From(result.Me));
    }
}
