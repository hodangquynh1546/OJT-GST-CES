using Asp.Versioning;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using ClassFund.Infrastructure.Persistence;

namespace ClassFund.IntegrationTests.Infrastructure;

/// <summary>
/// Endpoint chỉ có trong test (không có trong ClassFund.Api), đại diện cho "một module bất kỳ":
/// kiểm tra pipeline nền (xác thực, phân quyền theo quỹ, lỗi, phân trang) và đăng nhập nhanh cho test.
/// </summary>
[ApiVersion(1)]
[Route(ApiRoutes.Prefix + "/test-probe")]
public sealed class ProbeController(AppDbContext db, IAntiforgery antiforgery) : ApiControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { ok = true });

    [HttpGet("funds/{fundId}")]
    [RequirePermission(Permissions.FundView)]
    public IActionResult GetFund(Guid fundId) => Ok(new { fundId });

    [HttpPost("echo")]
    public ActionResult<ProbeEcho> Echo(ProbeEcho body) => Ok(body);

    [HttpGet("page")]
    public IActionResult Page([FromQuery] PageRequest request) => Ok(request);

    [HttpGet("errors/{kind}")]
    public IActionResult Error(string kind) => kind switch
    {
        "rule" => throw new BusinessRuleException("probe.rule", "Vi phạm quy tắc."),
        "conflict" => throw new ConflictException("probe.conflict", "Xung đột."),
        "not-found" => throw new NotFoundException(),
        "forbidden" => throw new ForbiddenAccessException(),
        _ => throw new InvalidOperationException("secret-detail-should-not-leak"),
    };

    /// <summary>Phát cookie phiên cho tài khoản đã seed (thay cho đăng nhập thật).</summary>
    [HttpPost("sign-in/{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> SignIn(Guid userId)
    {
        var stamp = await db.Users.Where(user => user.Id == userId).Select(user => user.SecurityStamp).SingleAsync();
        await SessionCookie.SignInAsync(HttpContext, new SessionTicket(userId, stamp));

        // Như đăng nhập thật: token antiforgery mới gắn với người dùng vừa đăng nhập.
        return Ok(new { userId, antiforgeryToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }
}

/// <summary>Body mẫu để kiểm tra định dạng JSON chung: enum chuỗi, ngày <c>yyyy-MM-dd</c>, tiền <c>long</c>.</summary>
public sealed record ProbeEcho(ClassFund.Domain.Enums.TransactionType Type, DateOnly OccurredOn, long Amount, DateTimeOffset At);
