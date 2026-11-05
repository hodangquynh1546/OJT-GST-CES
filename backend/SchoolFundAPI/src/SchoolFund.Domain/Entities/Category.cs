using SchoolFund.Domain.Common;
using SchoolFund.Domain.Enums;

namespace SchoolFund.Domain.Entities;

/// <summary>Danh mục thu hoặc chi của một quỹ; ẩn thay cho xóa.</summary>
public sealed class Category(Guid fundId, TransactionType type, string name, DateTimeOffset createdAt) : Entity
{
    public Guid FundId { get; private set; } = fundId;

    public TransactionType Type { get; private set; } = type;

    public string Name { get; private set; } = name;

    public bool IsHidden { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = createdAt;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Update(string name, bool isHidden, DateTimeOffset updatedAt)
    {
        Name = name;
        IsHidden = isHidden;
        UpdatedAt = updatedAt;
    }
}
