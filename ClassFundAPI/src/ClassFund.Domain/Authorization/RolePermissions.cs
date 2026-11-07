using System.Collections.Frozen;
using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Authorization;

/// <summary>Bảng vai trò → permission trong một quỹ. Là hằng số, không lưu trong DB (DD-08).</summary>
public static class RolePermissions
{
    private static readonly FrozenSet<string> Teacher = FrozenSet.Create(
        Permissions.FundView,
        Permissions.LedgerViewFull,
        Permissions.FundManage,
        Permissions.StudentManage,
        Permissions.TreasurerAssign,
        Permissions.MemberView);

    private static readonly FrozenSet<string> Treasurer = FrozenSet.Create(
        Permissions.FundView,
        Permissions.LedgerViewFull,
        Permissions.MemberView,
        Permissions.TransactionManage,
        Permissions.CategoryManage);

    private static readonly FrozenSet<string> PublicViewer = FrozenSet.Create(
        Permissions.FundView,
        Permissions.LedgerViewPublic);

    private static readonly FrozenSet<string> TeacherAccount = FrozenSet.Create(Permissions.FundCreate);

    public static IReadOnlySet<string> For(FundRole role) => role switch
    {
        FundRole.Teacher => Teacher,
        FundRole.Treasurer => Treasurer,
        FundRole.Student or FundRole.Parent => PublicViewer,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Vai trò không hợp lệ."),
    };

    /// <summary>Quyền cấp tài khoản, không gắn với quỹ.</summary>
    public static IReadOnlySet<string> ForAccount(UserKind kind) =>
        kind == UserKind.Teacher ? TeacherAccount : FrozenSet<string>.Empty;
}
