using ClassFund.Domain.Common;

namespace ClassFund.Domain.Entities;

/// <summary>Phân công thủ quỹ; thu hồi là đánh dấu để giữ lịch sử.</summary>
public sealed class TreasurerAssignment(Guid fundId, Guid studentId, Guid assignedBy, DateTimeOffset assignedAt) : Entity
{
    public Guid FundId { get; private set; } = fundId;

    public Guid StudentId { get; private set; } = studentId;

    public Guid AssignedBy { get; private set; } = assignedBy;

    public Guid? RevokedBy { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; } = assignedAt;

    public DateTimeOffset? RevokedAt { get; private set; }

    public void Revoke(Guid revokedBy, DateTimeOffset at)
    {
        RevokedBy = revokedBy;
        RevokedAt = at;
    }
}
