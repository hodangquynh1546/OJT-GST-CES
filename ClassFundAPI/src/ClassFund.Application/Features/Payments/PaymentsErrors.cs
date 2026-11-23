namespace ClassFund.Application.Features.Payments;

/// <summary>Mã lỗi và thông báo của module Thu tiền.</summary>
public static class PaymentsErrors
{
    public const int TransferCodeMaxLength = 100;
    public const int ReasonMaxLength = 500;

    /// <summary>Chỉ Tiền mặt hoặc Chuyển khoản; số dư cá nhân chỉ để lưu tiền thừa và rút.</summary>
    public const string MethodInvalidCode = "payment.method_invalid";
    public const string MethodInvalidMessage = "Chỉ chọn Tiền mặt hoặc Chuyển khoản.";

    public const string NotPayableCode = "payment.not_payable";
    public const string NotPayableMessage = "Khoản này đã được ghi nhận thu.";

    public const string ChargeNotOpenCode = "payment.charge_not_open";
    public const string ChargeNotOpenMessage = "Khoản thu đã đóng, không nhận thêm tiền.";

    public const string NotCollectedCode = "payment.not_collected";
    public const string NotCollectedMessage = "Khoản này chưa được ghi nhận thu.";

    public const string ReasonRequiredCode = "payment.reason_required";
    public const string ReasonRequiredMessage = "Vui lòng nhập lý do hủy ghi nhận thu.";
}
