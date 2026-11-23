namespace ClassFund.Application.Features.Settlements;

/// <summary>Mã lỗi và thông báo của module Quyết toán.</summary>
public static class SettlementErrors
{
    public const string NotCompletedCode = "settlement.event_not_completed";
    public const string NotCompletedMessage = "Chỉ quyết toán được sự kiện đã kết thúc.";

    public const string AlreadySettledCode = "settlement.already_settled";
    public const string AlreadySettledMessage = "Sự kiện này đã được quyết toán.";

    public const string MethodRequiredCode = "settlement.method_required";
    public const string MethodRequiredMessage = "Vui lòng chọn cách xử lý tiền thừa: hoàn trực tiếp hoặc cộng vào số dư.";

    public const string YearEndUnpaidCode = "year_end.unpaid_obligations";

    public const string LineProcessedCode = "settlement.line_processed";
    public const string LineProcessedMessage = "Khoản hoàn này đã được xử lý.";
}
