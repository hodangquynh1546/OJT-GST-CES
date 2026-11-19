namespace ClassFund.Application.Features.Charges;

/// <summary>Mã lỗi và thông báo của module Khoản thu.</summary>
public static class ChargesErrors
{
    public const int TitleMaxLength = 150;
    public const int PurposeMaxLength = 1000;

    public const string DueDateInPastCode = "charge.due_date_in_past";
    public const string DueDateInPastMessage = "Hạn đóng không được trước ngày hôm nay.";

    public const string InvalidStatusCode = "charge.invalid_status";
    public const string InvalidStatusMessage = "Khoản thu không còn ở trạng thái phù hợp để thực hiện thao tác này.";

    public const string HasPaymentsCode = "charge.has_payments";
    public const string HasPaymentsMessage = "Khoản thu đã có người nộp tiền nên không thể hủy. Hãy đóng khoản thu thay vì hủy.";

    public const string NothingToRemindCode = "charge.nothing_to_remind";
    public const string NothingToRemindMessage = "Không còn ai chưa đóng để nhắc.";

    public const string RemindTooSoonCode = "charge.remind_too_soon";
    public const string RemindTooSoonMessage = "Vừa nhắc xong. Vui lòng chờ ít nhất 1 giờ rồi nhắc lại.";

    /// <summary>Khoảng cách tối thiểu giữa hai lần nhắc của cùng một khoản thu.</summary>
    public static readonly TimeSpan RemindCooldown = TimeSpan.FromHours(1);
}
