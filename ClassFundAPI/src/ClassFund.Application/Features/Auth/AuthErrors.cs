namespace ClassFund.Application.Features.Auth;

/// <summary>Mã lỗi và thông báo của module Auth (mã chung <c>auth.invalid_credentials</c>… nằm ở AuthenticationFailedException).</summary>
public static class AuthErrors
{
    public const string EmailExistsCode = "auth.email_exists";
    public const string EmailExistsMessage = "Email này đã được đăng ký.";

    public const string InvalidCredentialsMessage = "Email hoặc mật khẩu không đúng.";
    public const string AccountDisabledMessage = "Tài khoản đã bị khóa. Vui lòng liên hệ thủ quỹ.";

    public const string CurrentPasswordIncorrectCode = "auth.current_password_incorrect";
    public const string CurrentPasswordIncorrectMessage = "Mật khẩu hiện tại không đúng.";

    public const string PasswordUnchangedCode = "auth.password_unchanged";
    public const string PasswordUnchangedMessage = "Mật khẩu mới phải khác mật khẩu hiện tại.";

    public const string InvalidEmailCode = "auth.invalid_email";
    public const string InvalidEmailMessage = "Email không hợp lệ.";

    public const string InvalidNameCode = "auth.invalid_name";
    public const string InvalidNameMessage = "Họ tên không được để trống và tối đa 100 ký tự.";

    public const int FullNameMaxLength = 100;
}
