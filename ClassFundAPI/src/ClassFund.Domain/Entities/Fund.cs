using ClassFund.Domain.Common;

namespace ClassFund.Domain.Entities;

/// <summary>Quỹ lớp; <see cref="OwnerUserId"/> là GVCN phụ trách.</summary>
public sealed class Fund(Guid ownerUserId, string name, DateTimeOffset createdAt) : Entity
{
    public Guid OwnerUserId { get; private set; } = ownerUserId;

    public string Name { get; private set; } = name;

    public DateTimeOffset CreatedAt { get; private set; } = createdAt;

    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>Đổi tên quỹ (FR-11); chỉ GVCN được gọi – kiểm tra ở Application.</summary>
    public void Rename(string name, DateTimeOffset at)
    {
        Name = name;
        UpdatedAt = at;
    }
}
