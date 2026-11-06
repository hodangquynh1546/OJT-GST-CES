using SchoolFund.Domain.Enums;

namespace SchoolFund.Application.Common.Auditing;

/// <summary>Tên hành động trong AuditLog (data-dictionary.md).</summary>
public static class AuditActions
{
    public const string AccountRegisterTeacher = "Account.RegisterTeacher";
    public const string AccountUpdateProfile = "Account.UpdateProfile";
    public const string AuthSignIn = "Auth.SignIn";
    public const string AuthSignInFailed = "Auth.SignInFailed";
    public const string AuthPasswordChanged = "Auth.PasswordChanged";
    public const string FundCreate = "Fund.Create";
    public const string FundRename = "Fund.Rename";
    public const string CategoryCreate = "Category.Create";
    public const string CategoryUpdate = "Category.Update";

    public static string TransactionCreate(TransactionType type) => $"{type}.Create";

    public static string TransactionUpdate(TransactionType type) => $"{type}.Update";

    public static string TransactionCancel(TransactionType type) => $"{type}.Cancel";
}
