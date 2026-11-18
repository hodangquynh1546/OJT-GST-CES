using ClassFund.Application.Common.Exceptions;
using ClassFund.Domain.Exceptions;

namespace ClassFund.Application.Features.Auth;

public interface IAuthService
{
    /// <summary>Tự đăng ký tài khoản (FR-01): tạo User <c>SelfRegistered</c>, đăng nhập luôn.</summary>
    /// <exception cref="ConflictException">Email đã tồn tại (<c>auth.email_exists</c>).</exception>
    /// <exception cref="BusinessRuleException">Email, họ tên hoặc mật khẩu vi phạm quy tắc.</exception>
    Task<AuthSession> RegisterAsync(string fullName, string email, string password, CancellationToken cancellationToken);

    /// <summary>Đăng nhập bằng email + mật khẩu (FR-02); băm lại mật khẩu khi tham số hash cũ.</summary>
    /// <exception cref="AuthenticationFailedException">Sai thông tin (<c>auth.invalid_credentials</c>) hoặc tài khoản bị khóa.</exception>
    Task<AuthSession> LoginAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Người dùng của phiên hiện tại kèm quyền cấp tài khoản và các quỹ đang tham gia.</summary>
    Task<CurrentUserDto> GetMeAsync(CancellationToken cancellationToken);

    /// <summary>Sửa họ tên (FR-04).</summary>
    Task<CurrentUserDto> UpdateProfileAsync(string fullName, CancellationToken cancellationToken);

    /// <summary>
    /// Đổi mật khẩu (FR-03, FR-04, BR-29): kiểm mật khẩu cũ, tắt <c>MustChangePassword</c>, đổi SecurityStamp
    /// (phiên cũ hết hiệu lực) và trả phiên mới.
    /// </summary>
    Task<AuthSession> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken);
}
