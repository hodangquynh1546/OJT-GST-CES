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

/// <summary>M1 – đăng ký giáo viên, đăng nhập một ô, đăng xuất, tài khoản cá nhân, đổi mật khẩu (docs/api/backend-api.md – Auth).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Prefix + "/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(IAuthService authService, IAntiforgery antiforgery) : ApiControllerBase
{
    /// <summary>Giáo viên tự đăng ký; đăng nhập luôn sau khi đăng ký.</summary>
    [HttpPost("register-teacher")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ApiProblems.ContentType)]
    public async Task<ActionResult<SessionResponse>> RegisterTeacher(RegisterTeacherRequest request, CancellationToken cancellationToken)
    {
        var session = await authService.RegisterTeacherAsync(
            new RegisterTeacherCommand(request.FullName!, request.Email!, request.Password!),
            cancellationToken);
        return Created("me", await StartSessionAsync(session));
    }

    /// <summary>Đăng nhập bằng email hoặc tên đăng nhập; không chọn vai trò.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ApiProblems.ContentType)]
    public async Task<ActionResult<SessionResponse>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await StartSessionAsync(await authService.LoginAsync(request.EffectiveLogin, request.Password!, cancellationToken)));

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
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MeDto>> GetMe(CancellationToken cancellationToken) =>
        Ok(await authService.GetMeAsync(cancellationToken));

    [HttpPut("me")]
    [AllowTemporaryPassword]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    public async Task<ActionResult<MeDto>> UpdateMe(UpdateMeRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.UpdateMeAsync(request.FullName!, cancellationToken));

    /// <summary>Đổi mật khẩu (kể cả mật khẩu tạm): phiên hiện tại nhận cookie mới, các phiên khác hết hiệu lực.</summary>
    [HttpPost("change-password")]
    [AllowTemporaryPassword]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    public async Task<ActionResult<SessionResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var session = await authService.ChangePasswordAsync(
            new ChangePasswordCommand(request.CurrentPassword!, request.NewPassword!),
            cancellationToken);
        return Ok(await StartSessionAsync(session));
    }

    private async Task<SessionResponse> StartSessionAsync(AuthSession session)
    {
        await SessionCookie.SignInAsync(HttpContext, session.Ticket);
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return new SessionResponse(tokens.RequestToken!, session.Me);
    }
}
