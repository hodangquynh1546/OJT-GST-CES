using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Auth;

public sealed record RegisterTeacherCommand(string FullName, string Email, string Password);

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

/// <summary>Một quỹ người dùng có vai trò. <see cref="LinkedStudents"/>: họ tên con (chỉ phụ huynh, còn lại rỗng).</summary>
public sealed record MembershipDto(
    Guid FundId,
    string FundName,
    FundRole Role,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> LinkedStudents);

/// <summary>Response của <c>GET /auth/me</c> (backend-api.md – Auth). Permission sắp theo thứ tự chữ cái.</summary>
public sealed record MeDto(
    Guid Id,
    UserKind Kind,
    string FullName,
    string? Email,
    string? Username,
    bool MustChangePassword,
    IReadOnlyList<string> AccountPermissions,
    IReadOnlyList<MembershipDto> Memberships);

/// <summary>Đăng ký / đăng nhập / đổi mật khẩu thành công: dữ liệu để API phát cookie phiên và thông tin người dùng.</summary>
public sealed record AuthSession(SessionTicket Ticket, MeDto Me);
