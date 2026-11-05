using SchoolFund.Domain.Common;

namespace SchoolFund.Domain.Entities;

/// <summary>Liên kết phụ huynh với học sinh; gỡ là đánh dấu, không xóa.</summary>
public sealed class ParentLink(Guid studentId, Guid parentUserId, Guid createdBy, DateTimeOffset createdAt) : Entity
{
    public Guid StudentId { get; private set; } = studentId;

    public Guid ParentUserId { get; private set; } = parentUserId;

    public bool IsActive { get; private set; } = true;

    public Guid CreatedBy { get; private set; } = createdBy;

    public Guid? RemovedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = createdAt;

    public DateTimeOffset? RemovedAt { get; private set; }

    public void Remove(Guid removedBy, DateTimeOffset at)
    {
        IsActive = false;
        RemovedBy = removedBy;
        RemovedAt = at;
    }
}
