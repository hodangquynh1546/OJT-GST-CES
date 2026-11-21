namespace ClassFund.Application.Features.Balance;

public static class BalanceErrors
{
    /// <summary>BR-25: số dư không âm.</summary>
    public const string Insufficient = "balance.insufficient";
    public const string InsufficientMessage = "Số dư cá nhân không đủ.";

    /// <summary>Yêu cầu rút đã được xử lý (đã trả hoặc đã từ chối).</summary>
    public const string NotPending = "withdrawal.not_pending";
    public const string NotPendingMessage = "Yêu cầu rút này đã được xử lý.";

    public const string ReasonRequired = "withdrawal.reason_required";
    public const string ReasonRequiredMessage = "Vui lòng nhập lý do từ chối.";
}
