using ClassFund.Domain.Common;
using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Entities;

/// <summary>Sự kiện: phạm vi tài chính độc lập với quỹ chung, quyết toán riêng.</summary>
public sealed class Event(Guid fundId, string name, string? description, DateOnly? eventDate, Guid createdByUserId, DateTimeOffset createdAt) : Entity
{
    public Guid FundId { get; init; } = fundId;

    public string Name { get; set; } = name;

    public string? Description { get; set; } = description;

    public DateOnly? EventDate { get; set; } = eventDate;

    public EventStatus Status { get; set; } = EventStatus.Open;

    /// <summary>Thời điểm thủ quỹ kết thúc sự kiện (chuyển sang Completed); null khi chưa kết thúc.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public Guid CreatedByUserId { get; init; } = createdByUserId;

    public DateTimeOffset CreatedAt { get; init; } = createdAt;
}

/// <summary>Người được mời vào sự kiện (mọi học sinh/thủ quỹ đang hoạt động) và câu trả lời của họ.</summary>
public sealed class EventParticipant(Guid eventId, Guid userId) : Entity
{
    public Guid EventId { get; init; } = eventId;

    public Guid UserId { get; init; } = userId;

    public ParticipationResponse Response { get; set; } = ParticipationResponse.Pending;

    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>
    /// True nếu còn tham gia sau khi sự kiện kết thúc: người Accepted đã đóng ít nhất một khoản
    /// hoặc có thanh toán đang chờ (BR-19). Người chưa đóng khoản nào bị loại, ghi ExclusionReason.
    /// </summary>
    public bool IsFinal { get; set; }

    public string? ExclusionReason { get; set; }
}
