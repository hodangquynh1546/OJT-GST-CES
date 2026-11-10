namespace ClassFund.Application.Features.Auth;

/// <summary>Đăng ký giáo viên, đăng nhập một ô, tài khoản cá nhân, đổi mật khẩu (FR-20, FR-13, FR-21).</summary>
public interface IAuthService
{
    Task<AuthSession> RegisterTeacherAsync(RegisterTeacherCommand command, CancellationToken cancellationToken);

    /// <param name="login">Email (giáo viên, phụ huynh) hoặc tên đăng nhập (học sinh); vai trò do hệ thống xác định (D-34).</param>
    Task<AuthSession> LoginAsync(string login, string password, CancellationToken cancellationToken);

    Task<MeDto> GetMeAsync(CancellationToken cancellationToken);

    Task<MeDto> UpdateMeAsync(string fullName, CancellationToken cancellationToken);

    /// <summary>Đổi mật khẩu (kể cả mật khẩu tạm): xóa cờ <c>MustChangePassword</c>, SecurityStamp mới → mọi phiên khác hết hiệu lực.</summary>
    Task<AuthSession> ChangePasswordAsync(ChangePasswordCommand command, CancellationToken cancellationToken);
}
