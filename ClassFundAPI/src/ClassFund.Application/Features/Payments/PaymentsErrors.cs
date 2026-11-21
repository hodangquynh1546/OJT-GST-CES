namespace ClassFund.Application.Features.Payments;

/// <summary>Mã lỗi và thông báo của module Thanh toán.</summary>
public static class PaymentsErrors
{
    public const int TransferCodeMaxLength = 100;
    public const int ReasonMaxLength = 500;

    /// <summary>BR-11: chuyển khoản phải kèm ảnh minh chứng.</summary>
    public const string ProofRequiredCode = "payment.proof_required";
    public const string ProofRequiredMessage = "Chuyển khoản cần kèm ảnh minh chứng.";

    public const string NotPayableCode = "payment.not_payable";
    public const string NotPayableMessage = "Khoản này đã nộp hoặc đang chờ thủ quỹ xác nhận.";

    public const string ChargeNotOpenCode = "payment.charge_not_open";
    public const string ChargeNotOpenMessage = "Khoản thu đã đóng, không nhận thêm tiền.";

    /// <summary>BR-10 / hai thủ quỹ xác nhận cùng lúc: chỉ một lần xử lý được.</summary>
    public const string AlreadyProcessedCode = "payment.already_processed";
    public const string AlreadyProcessedMessage = "Thanh toán này đã được xử lý.";

    public const string ReasonRequiredCode = "payment.reason_required";
    public const string ReasonRequiredMessage = "Vui lòng nhập lý do từ chối.";
}
