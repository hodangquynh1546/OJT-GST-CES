namespace ClassFund.Application.Features.Charges;

/// <summary>Mã audit của module Khoản thu.</summary>
public static class ChargesAuditActions
{
    public const string Create = "charge.create";
    public const string Close = "charge.close";
    public const string Cancel = "charge.cancel";
    public const string Remind = "charge.remind";

    /// <summary>Tạo khoản thu sự kiện (Scope Event).</summary>
    public const string EventCreate = "charge.event_create";

    /// <summary>Đóng khoản thu sự kiện (Scope Event).</summary>
    public const string EventClose = "charge.event_close";

    /// <summary>Hủy khoản thu sự kiện (Scope Event).</summary>
    public const string EventCancel = "charge.event_cancel";

    /// <summary>Nhắc một người tham gia đóng phần còn thiếu của sự kiện (Scope Event).</summary>
    public const string EventRemind = "charge.event_remind";
}
