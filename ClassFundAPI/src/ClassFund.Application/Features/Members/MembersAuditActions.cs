namespace ClassFund.Application.Features.Members;

/// <summary>Mã audit của module Học sinh (Scope General, gắn quỹ). Không bao giờ ghi mật khẩu / hash / stamp.</summary>
public static class MembersAuditActions
{
    public const string Add = "member.add";
    public const string Reactivate = "member.reactivate";
    public const string Update = "member.update";
    public const string ResetPassword = "member.reset_password";
    public const string Remove = "member.remove";
}
