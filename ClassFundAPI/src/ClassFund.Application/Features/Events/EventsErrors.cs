namespace ClassFund.Application.Features.Events;

public static class EventsErrors
{
    /// <summary>Chỉ đổi câu trả lời tham gia khi sự kiện còn mở (BR-17).</summary>
    public const string NotOpen = "event.not_open";
    public const string NotOpenMessage = "Sự kiện không còn mở, không đổi được câu trả lời tham gia.";

    /// <summary>Hủy chỉ khi chưa phát sinh tiền (BR-16).</summary>
    public const string HasMoney = "event.has_money";
    public const string HasMoneyMessage = "Sự kiện đã có thu tiền hoặc ghi chi, không hủy được. Hãy đóng danh sách rồi quyết toán.";

    /// <summary>Đã nộp tiền cho sự kiện thì không chuyển sang "Không tham gia".</summary>
    public const string ResponseLocked = "event.response_locked";
    public const string ResponseLockedMessage = "Bạn đã nộp tiền cho sự kiện này nên không chuyển sang không tham gia được. Liên hệ thủ quỹ.";

    /// <summary>Sự kiện đã quyết toán / hủy thì không sửa thông tin.</summary>
    public const string NotEditable = "event.not_editable";
    public const string NotEditableMessage = "Sự kiện đã quyết toán hoặc đã hủy, không sửa được.";

    public const string InvalidResponse = "event.invalid_response";
    public const string InvalidResponseMessage = "Chỉ chọn Tham gia hoặc Không tham gia.";

    public const string ExclusionReasonNothingPaid = "Chưa đóng khoản nào khi đóng danh sách.";
    public const string ExclusionReasonUnderpaid = "Chưa đóng đủ các khoản thu khi đóng danh sách; số tiền đã đóng được hoàn vào số dư cá nhân.";

    public const string ListDeadlineInPast = "event.list_deadline_in_past";
    public const string ListDeadlineInPastMessage = "Hạn đóng danh sách không được trước ngày hôm nay.";
}
