using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Auth;

namespace ClassFund.Api.Features.Auth;

/// <summary>Đăng ký giáo viên. Không có trường vai trò: tài khoản luôn là giáo viên (D-24).</summary>
public sealed record RegisterTeacherRequest(string? FullName, string? Email, string? Password, string? ConfirmPassword);

/// <summary>
/// Một ô "Email hoặc tên đăng nhập" (D-34), không có tham số vai trò.
/// <see cref="Email"/> chỉ để tương thích client cũ; <see cref="Login"/> được ưu tiên.
/// </summary>
public sealed record LoginRequest(string? Login, string? Email, string? Password)
{
    public string EffectiveLogin => string.IsNullOrWhiteSpace(Login) ? Email ?? string.Empty : Login;
}

public sealed record UpdateMeRequest(string? FullName);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record MembershipResponse(
    Guid FundId,
    string FundName,
    string Role,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> LinkedStudents);

/// <summary>Response của <c>GET /auth/me</c>.</summary>
public sealed record MeResponse(
    Guid Id,
    string Kind,
    string FullName,
    string? Email,
    string? Username,
    bool MustChangePassword,
    IReadOnlyList<string> AccountPermissions,
    IReadOnlyList<MembershipResponse> Memberships)
{
    public static MeResponse From(MeResult me) =>
        new(
            me.Id,
            me.Kind.ToString(),
            me.FullName,
            me.Email,
            me.Username,
            me.MustChangePassword,
            Sorted(me.AccountPermissions),
            me.Memberships.Select(From).ToArray());

    private static MembershipResponse From(FundMembership membership) =>
        new(
            membership.FundId,
            membership.FundName,
            membership.Role.ToString(),
            Sorted(membership.Permissions),
            membership.LinkedStudents);

    private static string[] Sorted(IEnumerable<string> permissions) => permissions.Order(StringComparer.Ordinal).ToArray();
}

/// <summary>Đăng ký / đăng nhập / đổi mật khẩu: token antiforgery mới (gửi lại ở header X-XSRF-TOKEN) và thông tin người dùng.</summary>
public sealed record SessionResponse(string AntiforgeryToken, MeResponse User);
