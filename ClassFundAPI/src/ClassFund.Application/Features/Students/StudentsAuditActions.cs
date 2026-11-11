namespace ClassFund.Application.Features.Students;

/// <summary>Tên hành động AuditLog của M3 (data-dictionary.md). Không bao giờ ghi mật khẩu tạm / hash (BR-37, NFR-10).</summary>
public static class StudentsAuditActions
{
    public const string Create = "Student.Create";
    public const string Update = "Student.Update";
    public const string IssueAccount = "Student.IssueAccount";
    public const string ResetPassword = "Student.ResetPassword";
    public const string ParentCreateAccount = "Parent.CreateAccount";
    public const string ParentLinkCreate = "ParentLink.Create";
    public const string ParentLinkRemove = "ParentLink.Remove";
}
