using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Common.Storage;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using Moq;

namespace ClassFund.UnitTests.Application;

/// <summary>Đồng hồ cố định / tua được cho test.</summary>
public sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>IFileStorage trong bộ nhớ.</summary>
public sealed class MemoryFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Objects { get; } = [];

    public async Task<string> SaveAsync(AttachmentKind kind, Guid? fundId, string fileName, string contentType, Stream content, CancellationToken cancellationToken)
    {
        var key = StorageKeys.Create(kind, fundId, fileName);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Objects[key] = buffer.ToArray();
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(Objects.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        Objects.Remove(storageKey);
        return Task.CompletedTask;
    }
}

/// <summary>Dữ liệu chung: một quỹ có Owner, Treasurer, hai Student; người dùng hiện tại đổi bằng <see cref="ActAs"/>.</summary>
public sealed class ServiceTestContext
{
    public static readonly DateTimeOffset Start = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICurrentUser> _currentUser = new();

    public ServiceTestContext()
    {
        Owner = AddUser("owner@x.com", UserOrigin.SelfRegistered);
        Treasurer = AddUser("treasurer@x.com", UserOrigin.Provisioned);
        StudentA = AddUser("a@x.com", UserOrigin.Provisioned);
        StudentB = AddUser("b@x.com", UserOrigin.Provisioned);
        Outsider = AddUser("out@x.com", UserOrigin.SelfRegistered);

        Fund = new Fund("Lớp 10A1", Owner.Id, "2026-2027", Start);
        Db.Seed(Fund);
        Db.Seed(
            new FundMember(Fund.Id, Owner.Id, MemberRole.Owner, Start),
            new FundMember(Fund.Id, Treasurer.Id, MemberRole.Treasurer, Start),
            new FundMember(Fund.Id, StudentA.Id, MemberRole.Student, Start),
            new FundMember(Fund.Id, StudentB.Id, MemberRole.Student, Start));

        Access = new AccessService(Db);
        Audit = new AuditTrail(Db, new Mock<IRequestInfo>().Object, Clock);
        ActAs(Owner);
    }

    public FakeAppDb Db { get; } = new();

    public TestClock Clock { get; } = new(Start);

    public MemoryFileStorage Storage { get; } = new();

    public ICurrentUser CurrentUser => _currentUser.Object;

    public IAccessService Access { get; }

    public AuditTrail Audit { get; }

    public Fund Fund { get; }

    public User Owner { get; }

    public User Treasurer { get; }

    public User StudentA { get; }

    public User StudentB { get; }

    public User Outsider { get; }

    public IEnumerable<AuditLog> AuditLogs => Db.Query<AuditLog>();

    public void ActAs(User user)
    {
        _currentUser.SetupGet(current => current.UserId).Returns(user.Id);
        _currentUser.SetupGet(current => current.IsAuthenticated).Returns(true);
    }

    private User AddUser(string email, UserOrigin origin)
    {
        var user = new User(email, email.Split('@')[0], "hash", origin, false, Start);
        Db.Seed(user);
        return user;
    }
}
