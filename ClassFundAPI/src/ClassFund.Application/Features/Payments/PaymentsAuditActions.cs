namespace ClassFund.Application.Features.Payments;

/// <summary>Mã audit của module Thanh toán (Scope General, gắn quỹ). <c>payment.confirm</c> của thủ quỹ tự nộp có <c>IsSelfAction</c>.</summary>
public static class PaymentsAuditActions
{
    public const string Submit = "payment.submit";
    public const string Confirm = "payment.confirm";
    public const string Reject = "payment.reject";
    public const string PayWithBalance = "payment.pay_with_balance";
}
