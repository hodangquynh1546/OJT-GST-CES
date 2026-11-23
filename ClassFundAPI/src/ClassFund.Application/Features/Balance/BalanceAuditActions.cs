namespace ClassFund.Application.Features.Balance;

public static class BalanceAuditActions
{
    /// <summary>Thủ quỹ rút số dư của một thành viên (Scope System; <c>IsSelfAction</c> khi rút số dư của chính mình).</summary>
    public const string Withdraw = "balance.withdraw";
}
