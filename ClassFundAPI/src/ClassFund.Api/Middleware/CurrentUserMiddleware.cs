using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ClassFund.Api.Authorization;
using ClassFund.Application.Common.Security;

namespace ClassFund.Api.Middleware;

/// <summary>Đối chiếu cookie phiên với dữ liệu mới nhất; phiên hết hiệu lực được coi như chưa đăng nhập.</summary>
public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CurrentUser currentUser, ISessionStore sessions)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var session = await FindSessionAsync(context, sessions);
            if (session is not null)
            {
                currentUser.Set(session);
            }
            else
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
        }

        await next(context);
    }

    private static async Task<ActiveSession?> FindSessionAsync(HttpContext context, ISessionStore sessions)
    {
        var stamp = context.User.FindFirstValue(SessionClaims.SecurityStamp);
        return Guid.TryParse(context.User.FindFirstValue(SessionClaims.UserId), out var userId) && stamp is not null
            ? await sessions.FindActiveAsync(userId, stamp, context.RequestAborted)
            : null;
    }
}
