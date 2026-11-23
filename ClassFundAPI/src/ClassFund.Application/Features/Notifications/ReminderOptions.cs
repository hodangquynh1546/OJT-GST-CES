namespace ClassFund.Application.Features.Notifications;

/// <summary>Cấu hình nhắc đóng tiền (<c>appsettings</c>, mục <c>Reminders</c>).</summary>
public sealed class ReminderOptions
{
    public const string SectionName = "Reminders";
    public const int MinDays = 1;
    public const int MaxDays = 14;

    /// <summary>
    /// Số ngày trước hạn đóng danh sách của sự kiện bắt đầu nhắc tự động (mặc định 3: nhắc ở D-3, D-2, D-1, mỗi ngày một lần
    /// cho người tham gia chưa đóng đủ).
    /// </summary>
    public int ListDeadlineDays { get; init; } = 3;

    public int EffectiveListDeadlineDays => Math.Clamp(ListDeadlineDays, MinDays, MaxDays);
}
