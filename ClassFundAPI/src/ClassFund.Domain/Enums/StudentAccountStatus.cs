namespace ClassFund.Domain.Enums;

/// <summary>
/// Trạng thái tài khoản của học sinh hiển thị ở API (<c>accountStatus</c>) và dùng để chặn phân công thủ quỹ.
/// Không lưu ở database: suy ra bằng <see cref="Rules.StudentAccount.StatusOf"/>. Khác với <see cref="AccountStatus"/> (cột <c>User.Status</c>).
/// </summary>
public enum StudentAccountStatus
{
    /// <summary>Chưa cấp tài khoản.</summary>
    None = 0,

    /// <summary>Đã cấp mật khẩu tạm, học sinh chưa đổi mật khẩu.</summary>
    PendingActivation = 1,

    /// <summary>Đã đổi mật khẩu, đăng nhập được.</summary>
    Active = 2,

    /// <summary>Tài khoản bị khóa.</summary>
    Disabled = 3,
}
