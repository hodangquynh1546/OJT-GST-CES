using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Authorization;

/// <summary>Permission cấp quỹ. Backend kiểm tra bằng <c>[RequirePermission(Permissions.X)]</c>, không kiểm tra theo role.</summary>
public static class Permissions
{
    /// <summary>Cấp tài khoản (không gắn quỹ): tạo quỹ. Chỉ tài khoản tự đăng ký.</summary>
    public const string FundCreate = "Fund.Create";

    public const string FundView = "Fund.View";
    public const string FundManage = "Fund.Manage";
    public const string MemberManage = "Member.Manage";
    public const string TreasurerManage = "Treasurer.Manage";
    public const string CategoryManage = "Category.Manage";
    public const string EventManage = "Event.Manage";
    public const string EventParticipate = "Event.Participate";
    public const string ChargeManage = "Charge.Manage";
    public const string LedgerManage = "Ledger.Manage";
    public const string PaymentSubmit = "Payment.Submit";
    public const string PaymentConfirm = "Payment.Confirm";
    public const string SettlementExecute = "Settlement.Execute";
    public const string BalanceViewOwn = "Balance.ViewOwn";
    public const string BalanceWithdraw = "Balance.Withdraw";
    public const string BalanceHandle = "Balance.Handle";
    public const string ReportView = "Report.View";
    public const string AuditView = "Audit.View";
}

/// <summary>Ma trận role → permission (nguồn sự thật duy nhất, khớp docs/business-analysis/roles-and-permissions.md).</summary>
public static class RolePermissions
{
    private static readonly string[] StudentSet =
    [
        Permissions.FundView,
        Permissions.EventParticipate,
        Permissions.PaymentSubmit,
        Permissions.BalanceViewOwn,
        Permissions.BalanceWithdraw,
    ];

    private static readonly string[] TreasurerExtra =
    [
        Permissions.FundManage,
        Permissions.MemberManage,
        Permissions.CategoryManage,
        Permissions.EventManage,
        Permissions.ChargeManage,
        Permissions.LedgerManage,
        Permissions.PaymentConfirm,
        Permissions.SettlementExecute,
        Permissions.BalanceHandle,
        Permissions.ReportView,
        Permissions.AuditView,
    ];

    private static readonly IReadOnlySet<string> Student = new HashSet<string>(StudentSet);

    /// <summary>Thủ quỹ được phân quyền: mọi nghiệp vụ quản lý quỹ + quyền học sinh, KHÔNG có <see cref="Permissions.TreasurerManage"/>.</summary>
    private static readonly IReadOnlySet<string> Treasurer = new HashSet<string>([.. StudentSet, .. TreasurerExtra]);

    /// <summary>Thủ quỹ đầu tiên: toàn quyền, gồm quản lý thủ quỹ khác.</summary>
    private static readonly IReadOnlySet<string> Owner = new HashSet<string>([.. Treasurer, Permissions.TreasurerManage]);

    public static IReadOnlySet<string> For(MemberRole role) => role switch
    {
        MemberRole.Owner => Owner,
        MemberRole.Treasurer => Treasurer,
        MemberRole.Student => Student,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    /// <summary>Quyền cấp tài khoản: chỉ tài khoản tự đăng ký được tạo quỹ.</summary>
    public static IReadOnlySet<string> ForAccount(UserOrigin origin) =>
        origin == UserOrigin.SelfRegistered ? new HashSet<string> { Permissions.FundCreate } : new HashSet<string>();
}
