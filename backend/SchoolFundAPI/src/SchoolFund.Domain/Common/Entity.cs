namespace SchoolFund.Domain.Common;

/// <summary>Lớp gốc của entity với kiểu khóa chính tùy chọn.</summary>
public abstract class Entity<TId>
    where TId : struct
{
    public TId Id { get; protected init; }
}

/// <summary>Entity có khóa chính UUID v7 sinh ở ứng dụng.</summary>
public abstract class Entity : Entity<Guid>
{
    protected Entity() => Id = Guid.CreateVersion7();
}
