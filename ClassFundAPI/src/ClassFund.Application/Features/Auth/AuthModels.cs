using ClassFund.Application.Common.Security;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Auth;

public sealed record RegisterTeacherCommand(string FullName, string Email, string Password);

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

/// <summary>Dữ liệu để API phát cookie phiên: chỉ mã người dùng và SecurityStamp.</summary>
public sealed record SessionTicket(Guid UserId, string SecurityStamp);

/// <summary>Kết quả của <c>GET /auth/me</c>.</summary>
public sealed record MeResult(
    Guid Id,
    UserKind Kind,
    string FullName,
    string? Email,
    string? Username,
    bool MustChangePassword,
    IReadOnlySet<string> AccountPermissions,
    IReadOnlyList<FundMembership> Memberships);

/// <summary>Đăng nhập / đăng ký / đổi mật khẩu thành công: phiên mới và thông tin người dùng.</summary>
public sealed record AuthSessionResult(SessionTicket Session, MeResult Me);
