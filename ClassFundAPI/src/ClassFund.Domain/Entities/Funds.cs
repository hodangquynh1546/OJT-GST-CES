using ClassFund.Domain.Common;
using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Entities;

/// <summary>Quỹ lớp. Quỹ chung = giao dịch có EventId null; mỗi sự kiện là một phạm vi tài chính riêng.</summary>
public sealed class Fund(string name, Guid createdByUserId, string schoolYear, DateTimeOffset createdAt) : Entity
{
    public string Name { get; set; } = name;

    public Guid CreatedByUserId { get; init; } = createdByUserId;

    /// <summary>Năm học, ví dụ "2026-2027"; dùng cho tổng kết cuối năm.</summary>
    public string SchoolYear { get; set; } = schoolYear;

    public FundStatus Status { get; set; } = FundStatus.Active;

    public DateTimeOffset CreatedAt { get; init; } = createdAt;
}

/// <summary>Thành viên quỹ. Role quyết định permission (RolePermissions); Owner là thủ quỹ đầu tiên (người tạo quỹ).</summary>
public sealed class FundMember(Guid fundId, Guid userId, MemberRole role, DateTimeOffset joinedAt) : Entity
{
    public Guid FundId { get; init; } = fundId;

    public Guid UserId { get; init; } = userId;

    public MemberRole Role { get; set; } = role;

    public MemberStatus Status { get; set; } = MemberStatus.Active;

    public DateTimeOffset JoinedAt { get; init; } = joinedAt;

    public DateTimeOffset? RemovedAt { get; set; }
}

/// <summary>Danh mục thu/chi của quỹ.</summary>
public sealed class Category(Guid fundId, string name, LedgerType type, bool isSystem) : Entity
{
    public Guid FundId { get; init; } = fundId;

    public string Name { get; set; } = name;

    public LedgerType Type { get; init; } = type;

    public bool IsSystem { get; init; } = isSystem;

    public bool IsActive { get; set; } = true;
}
