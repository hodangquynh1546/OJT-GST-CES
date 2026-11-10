namespace ClassFund.Application.Features.Auth;

/// <summary>Tên hành động AuditLog của M1 (data-dictionary.md). Không bao giờ ghi mật khẩu / hash / cookie (NFR-10).</summary>
public static class AuthAuditActions
{
    public const string RegisterTeacher = "Account.RegisterTeacher";
    public const string UpdateProfile = "Account.UpdateProfile";
    public const string SignIn = "Auth.SignIn";
    public const string SignInFailed = "Auth.SignInFailed";
    public const string PasswordChanged = "Auth.PasswordChanged";
}
