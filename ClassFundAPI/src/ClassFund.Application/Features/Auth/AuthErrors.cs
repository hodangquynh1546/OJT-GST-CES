using ClassFund.Application.Common.Exceptions;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Auth;

/// <summary>Mã lỗi của M1 Auth (docs/api/backend-api.md – Auth). Giữ nguyên chính tả: frontend dựa vào mã này.</summary>
public static class AuthErrors
{
    public const string EmailTakenCode = "user.email_taken";
    public const string WrongPasswordCode = "auth.wrong_password";
    public const string PasswordReusedCode = "auth.password_reused";

    public static BusinessRuleException EmailTaken() =>
        new(EmailTakenCode, "Email này đã được dùng cho một tài khoản khác.");

    public static BusinessRuleException WrongPassword() =>
        new(WrongPasswordCode, "Mật khẩu hiện tại không đúng.");

    public static BusinessRuleException PasswordReused() =>
        new(PasswordReusedCode, "Mật khẩu mới phải khác mật khẩu hiện tại.");

    public static BusinessRuleException PasswordPolicyViolated() =>
        new(PasswordPolicy.ViolationCode, PasswordPolicy.ViolationMessage);

    /// <summary>Một thông báo chung cho sai email / tên đăng nhập / mật khẩu: không tiết lộ tài khoản có tồn tại (FR-13).</summary>
    public static AuthenticationFailedException InvalidCredentials() =>
        new(AuthenticationFailedException.InvalidCredentialsCode, "Email / tên đăng nhập hoặc mật khẩu không đúng.");

    public static AuthenticationFailedException AccountDisabled() =>
        new(AuthenticationFailedException.AccountDisabledCode, "Tài khoản đang bị khóa, liên hệ giáo viên chủ nhiệm.");

    public static AuthenticationFailedException NotSignedIn() =>
        new(AuthenticationFailedException.UnauthorizedCode, "Bạn chưa đăng nhập.");
}
