namespace ClassFund.Application.Features.Payments;

/// <summary>Mã audit của module Thu tiền. <c>payment.collect</c> của thủ quỹ tích cho chính mình có <c>IsSelfAction</c>.</summary>
public static class PaymentsAuditActions
{
    public const string Collect = "payment.collect";
    public const string Uncollect = "payment.uncollect";

    /// <summary>Thủ quỹ bấm "Nhắc đóng" cho một người (mỗi lần nhắc một dòng).</summary>
    public const string Remind = "payment.remind";
}
