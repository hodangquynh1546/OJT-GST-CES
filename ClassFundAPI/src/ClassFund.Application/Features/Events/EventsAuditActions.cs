namespace ClassFund.Application.Features.Events;

public static class EventsAuditActions
{
    public const string Create = "event.create";
    public const string Update = "event.update";
    public const string Cancel = "event.cancel";
    public const string Complete = "event.complete";
    public const string Respond = "event.respond";

    /// <summary>Thành viên thêm vào quỹ sau được mời vào các sự kiện đang mở (BR-17, TD-10).</summary>
    public const string Invite = "event.invite";
}
