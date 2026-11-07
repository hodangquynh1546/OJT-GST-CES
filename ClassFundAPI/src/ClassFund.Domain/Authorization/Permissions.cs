namespace ClassFund.Domain.Authorization;

/// <summary>Danh sách permission theo docs/business-analysis/roles-and-permissions.md mục 2.</summary>
public static class Permissions
{
    /// <summary>Quyền cấp tài khoản (mọi giáo viên), không gắn với quỹ.</summary>
    public const string FundCreate = "Fund.Create";

    public const string FundView = "Fund.View";
    public const string LedgerViewFull = "Ledger.ViewFull";
    public const string LedgerViewPublic = "Ledger.ViewPublic";
    public const string FundManage = "Fund.Manage";
    public const string StudentManage = "Student.Manage";
    public const string TreasurerAssign = "Treasurer.Assign";
    public const string MemberView = "Member.View";
    public const string TransactionManage = "Transaction.Manage";
    public const string CategoryManage = "Category.Manage";
}
