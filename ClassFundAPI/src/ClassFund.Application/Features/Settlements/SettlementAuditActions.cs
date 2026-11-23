namespace ClassFund.Application.Features.Settlements;

/// <summary>Mã audit của module Quyết toán (Scope Event).</summary>
public static class SettlementAuditActions
{
    public const string Execute = "settlement.execute";
    public const string Refund = "settlement.refund";

    /// <summary>Tổng kết cuối năm quỹ chung (Scope General).</summary>
    public const string YearEndSettle = "year_end.settle";

    public const string YearEndRefund = "year_end.refund";
}
