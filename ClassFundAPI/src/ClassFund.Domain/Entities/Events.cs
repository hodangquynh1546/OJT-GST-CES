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

    /// <summary>Thời điểm thủ quỹ chốt danh sách tham gia thực tế.</summary>
    public DateTimeOffset? ListLockedAt { get; set; }

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

    /// <summary>True nếu thuộc danh sách tham gia thực tế sau khi chốt (đã hoàn thành nghĩa vụ đóng tiền).</summary>
    public bool IsFinal { get; set; }

    public string? ExclusionReason { get; set; }
}
