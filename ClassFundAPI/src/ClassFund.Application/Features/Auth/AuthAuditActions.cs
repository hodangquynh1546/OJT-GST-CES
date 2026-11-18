namespace ClassFund.Application.Features.Auth;

/// <summary>Mã audit của module Auth (Scope System, không gắn quỹ). Không bao giờ ghi mật khẩu / hash / stamp.</summary>
public static class AuthAuditActions
{
    public const string Register = "auth.register";
    public const string ChangePassword = "auth.change_password";
    public const string UpdateProfile = "auth.update_profile";
}
