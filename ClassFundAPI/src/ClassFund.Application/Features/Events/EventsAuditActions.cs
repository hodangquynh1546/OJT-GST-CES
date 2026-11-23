namespace ClassFund.Application.Features.Events;

public static class EventsAuditActions
{
    public const string Create = "event.create";
    public const string Update = "event.update";
    public const string Cancel = "event.cancel";
    /// <summary>Đóng danh sách (Open → Completed): loại người chưa đóng đủ, hoàn tiền đã đóng vào số dư.</summary>
    public const string CloseList = "event.close_list";

    /// <summary>Một người bị loại khi đóng danh sách (kèm số tiền hoàn).</summary>
    public const string Exclude = "event.exclude";
    public const string Respond = "event.respond";

    /// <summary>Thành viên thêm vào quỹ sau được mời vào các sự kiện đang mở (BR-17, TD-10).</summary>
    public const string Invite = "event.invite";
}
