using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ClassFund.Application.Common.Security;

namespace ClassFund.Api.Authorization;

/// <summary>Phát cookie phiên <c>cf_session</c>: chỉ chứa mã người dùng và SecurityStamp.</summary>
public static class SessionCookie
{
    public static async Task SignInAsync(HttpContext context, SessionTicket ticket)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(SessionClaims.UserId, ticket.UserId.ToString()),
                new Claim(SessionClaims.SecurityStamp, ticket.SecurityStamp),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        // Token antiforgery cấp trong cùng response phải gắn với người dùng vừa đăng nhập.
        context.User = principal;
    }

    public static Task SignOutAsync(HttpContext context) =>
        context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}
