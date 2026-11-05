using SchoolFund.Domain.Common;

namespace SchoolFund.Domain.Entities;

/// <summary>Học sinh của đúng một quỹ; <see cref="UserId"/> null nghĩa là chưa cấp tài khoản.</summary>
public sealed class Student(Guid fundId, string fullName, Guid createdBy, DateTimeOffset createdAt) : Entity
{
    public Guid FundId { get; private set; } = fundId;

    public string FullName { get; private set; } = fullName;

    public Guid? UserId { get; private set; }

    public Guid CreatedBy { get; private set; } = createdBy;

    public DateTimeOffset CreatedAt { get; private set; } = createdAt;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public void LinkAccount(Guid userId, DateTimeOffset at)
    {
        UserId = userId;
        UpdatedAt = at;
    }
}
