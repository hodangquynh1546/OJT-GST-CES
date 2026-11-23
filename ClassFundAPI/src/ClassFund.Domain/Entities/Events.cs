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

    /// <summary>Hạn đóng danh sách (thủ quỹ nhập khi tạo / sửa); từ hạn này thủ quỹ đóng danh sách, trước hạn có nhắc tự động. Null ở sự kiện tạo trước khi có cột này.</summary>
    public DateOnly? ListDeadline { get; set; }

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

    /// <summary>Thời điểm bị loại khi đóng danh sách; null khi chưa bị loại.</summary>
    public DateTimeOffset? ExcludedAt { get; set; }

    /// <summary>Số tiền đã đóng được hoàn vào số dư cá nhân khi bị loại; null khi chưa bị loại (0 nếu chưa đóng đồng nào).</summary>
    public long? RefundedAmount { get; set; }
}
