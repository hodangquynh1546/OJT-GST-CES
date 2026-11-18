using ClassFund.Application.Common.Security;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Auth;

/// <summary>Một quỹ người dùng là thành viên đang hoạt động, kèm permission suy ra từ vai trò.</summary>
public sealed record MembershipDto(Guid FundId, string FundName, MemberRole Role, IReadOnlyCollection<string> Permissions);

/// <summary>Người dùng hiện tại (<c>GET /auth/me</c>); khớp <c>core/auth/auth.models.ts</c>.</summary>
public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string FullName,
    bool MustChangePassword,
    IReadOnlyCollection<string> AccountPermissions,
    IReadOnlyList<MembershipDto> Memberships);

/// <summary>Kết quả đổi phiên: <see cref="Ticket"/> để controller phát cookie, <see cref="User"/> trả về client.</summary>
public sealed record AuthSession(SessionTicket Ticket, CurrentUserDto User);
