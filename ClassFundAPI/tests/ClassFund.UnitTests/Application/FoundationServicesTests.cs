using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;
using Moq;

namespace ClassFund.UnitTests.Application;

public class AccessServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly User _owner = new("o@x.com", "Owner", "h", UserOrigin.SelfRegistered, false, Now);
    private readonly User _student = new("s@x.com", "Student", "h", UserOrigin.Provisioned, true, Now);
    private readonly Fund _fund;

    public AccessServiceTests()
    {
        _fund = new Fund("Quỹ A", _owner.Id, "2026-2027", Now);
        _db.Seed(_owner, _student);
        _db.Seed(_fund);
        _db.Seed(new FundMember(_fund.Id, _owner.Id, MemberRole.Owner, Now), new FundMember(_fund.Id, _student.Id, MemberRole.Student, Now));
    }

    [Fact]
    public async Task Member_gets_permissions_of_their_role()
    {
        var access = await new AccessService(_db).GetFundAccessAsync(_owner.Id, _fund.Id, default);
        Assert.Equal(MemberRole.Owner, access.Role);
        Assert.Contains(Permissions.TreasurerManage, access.Permissions);
    }

    [Fact]
    public async Task Non_member_gets_not_found_not_forbidden() =>
        await Assert.ThrowsAsync<NotFoundException>(() => new AccessService(_db).GetFundAccessAsync(Guid.NewGuid(), _fund.Id, default));

    [Fact]
    public async Task Removed_member_loses_access()
    {
        var removed = new FundMember(_fund.Id, Guid.NewGuid(), MemberRole.Student, Now) { Status = MemberStatus.Removed };
        _db.Seed(removed);
        await Assert.ThrowsAsync<NotFoundException>(() => new AccessService(_db).GetFundAccessAsync(removed.UserId, _fund.Id, default));
    }

    [Fact]
    public async Task Memberships_list_only_active_funds()
    {
        var list = await new AccessService(_db).GetMembershipsAsync(_student.Id, default);
        var item = Assert.Single(list);
        Assert.Equal(MemberRole.Student, item.Role);
    }

    [Fact]
    public async Task Provisioned_account_cannot_create_funds() =>
        Assert.Empty(await new AccessService(_db).GetAccountPermissionsAsync(_student.Id, default));
}

public class AuditAndNotifierTests
{
    [Fact]
    public void Audit_entry_carries_scope_actor_and_self_flag()
    {
        var db = new FakeAppDb();
        var request = new Mock<IRequestInfo>();
        request.SetupGet(info => info.CorrelationId).Returns("corr-1");
        var actor = Guid.NewGuid();

        new AuditTrail(db, request.Object, TimeProvider.System).Record(new AuditEntry(
            "payment.confirm", Guid.NewGuid(), actor, FinancialScope.General, EntityType: "Payment", After: new { status = "Confirmed" }, IsSelfAction: true));

        var log = Assert.Single(db.Query<AuditLog>());
        Assert.Equal(actor, log.ActorUserId);
        Assert.Equal(FinancialScope.General, log.Scope);
        Assert.True(log.IsSelfAction);
        Assert.Equal("corr-1", log.CorrelationId);
        Assert.Contains("Confirmed", log.AfterJson);
    }

    [Fact]
    public void Notifier_sends_one_notification_per_distinct_recipient()
    {
        var db = new FakeAppDb();
        var user = Guid.NewGuid();
        new Notifier(db, TimeProvider.System).Send(
            [user, user, Guid.NewGuid()],
            new NotificationMessage(Guid.NewGuid(), FinancialScope.Event, Guid.NewGuid(), NotificationType.EventChargeCreated, "t", "b"));
        Assert.Equal(2, db.Query<Notification>().Count());
    }

    [Theory]
    [InlineData(FinancialScope.General, null, "[Quỹ chung]")]
    [InlineData(FinancialScope.Event, "20/11", "[Sự kiện: 20/11]")]
    public void Scope_prefix_distinguishes_general_and_event(FinancialScope scope, string? name, string expected) =>
        Assert.Equal(expected, Notifier.ScopePrefix(scope, name));
}

public class CredentialsTests
{
    [Fact]
    public void Temporary_password_satisfies_policy_and_is_random()
    {
        var first = Credentials.TemporaryPassword();
        Assert.True(PasswordPolicy.IsSatisfiedBy(first));
        Assert.NotEqual(first, Credentials.TemporaryPassword());
    }
}
