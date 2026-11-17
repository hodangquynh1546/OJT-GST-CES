namespace ClassFund.Domain.Common;

/// <summary>Entity có khóa chính UUID v7 sinh ở ứng dụng.</summary>
public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
}
