using System.Net;
using ClassFund.Application.Features.AuditLogs;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.UnitTests.Application;

public class AuditLogsServiceTests
{
    private readonly ServiceTestContext _ctx = new();
    private readonly Event _picnic;

    public AuditLogsServiceTests()
    {
        _picnic = new Event(_ctx.Fund.Id, "Dã ngoại Ba Vì", null, null, _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(_picnic);
    }

    private AuditLogsService Service => new(_ctx.Db);

    private static AuditLogFilter Filter(
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? actorId = null,
        FinancialScope? scope = null,
        Guid? eventId = null,
        string? action = null,
        bool selfOnly = false,
        int page = 1,
        int pageSize = 20) => new(from, to, actorId, scope, eventId, action, selfOnly, page, pageSize);

    private AuditLog Log(
        DateTimeOffset at,
        string action,
        User? actor,
        FinancialScope scope = FinancialScope.General,
        Guid? eventId = null,
        Guid? fundId = null,
        bool self = false,
        string? before = null,
        string? after = null,
        string? note = null,
        IPAddress? ip = null)
    {
        var log = new AuditLog(at, fundId ?? _ctx.Fund.Id, actor?.Id, scope, eventId, action, "X", null)
        {
            IsSelfAction = self,
            BeforeJson = before,
            AfterJson = after,
            Note = note,
            IpAddress = ip,
        };
        _ctx.Db.Seed(log);
        return log;
    }

    [Fact]
    public async Task Lists_only_this_fund_newest_first_with_actor_and_event_names()
    {
        var older = Log(ServiceTestContext.Start, "fund.update", _ctx.Owner);
        var newer = Log(ServiceTestContext.Start.AddHours(2), "payment.confirm", _ctx.Treasurer, FinancialScope.Event, _picnic.Id);
        Log(ServiceTestContext.Start.AddHours(3), "fund.update", _ctx.Owner, fundId: Guid.NewGuid()); // quỹ khác

        var result = await Service.ListAsync(_ctx.Fund.Id, Filter(), default);

        Assert.Equal(2, result.Total);
        Assert.Equal([newer.Id, older.Id], result.Items.Select(item => item.Id));
        Assert.Equal(_ctx.Treasurer.FullName, result.Items[0].ActorName);
        Assert.Equal("Dã ngoại Ba Vì", result.Items[0].EventName);
        Assert.Null(result.Items[1].EventName);
    }

    [Fact]
    public async Task System_actions_show_system_actor()
    {
        Log(ServiceTestContext.Start, "attachment.cleanup", actor: null);
        var item = Assert.Single((await Service.ListAsync(_ctx.Fund.Id, Filter(), default)).Items);
        Assert.Equal(AuditLogsService.SystemActorName, item.ActorName);
    }

    [Fact]
    public async Task Date_filter_uses_vietnam_calendar_days_inclusive_NFR07()
    {
        // 16/11 23:30 giờ VN = 16/11 16:30 UTC; 17/11 00:30 giờ VN = 16/11 17:30 UTC.
        var lateOn16 = Log(new DateTimeOffset(2026, 11, 16, 16, 30, 0, TimeSpan.Zero), "fund.update", _ctx.Owner);
        var earlyOn17 = Log(new DateTimeOffset(2026, 11, 16, 17, 30, 0, TimeSpan.Zero), "fund.update", _ctx.Owner);

        var only17 = await Service.ListAsync(_ctx.Fund.Id, Filter(from: new DateOnly(2026, 11, 17), to: new DateOnly(2026, 11, 17)), default);
        Assert.Equal(earlyOn17.Id, Assert.Single(only17.Items).Id);

        var only16 = await Service.ListAsync(_ctx.Fund.Id, Filter(to: new DateOnly(2026, 11, 16)), default);
        Assert.Equal(lateOn16.Id, Assert.Single(only16.Items).Id);
    }

    [Fact]
    public async Task Filters_by_actor_scope_event_action_and_self_FR47()
    {
        var general = Log(ServiceTestContext.Start, "member.add", _ctx.Owner);
        var eventLog = Log(ServiceTestContext.Start.AddMinutes(1), "payment.confirm", _ctx.Treasurer, FinancialScope.Event, _picnic.Id);
        var selfLog = Log(ServiceTestContext.Start.AddMinutes(2), "payment.confirm", _ctx.Owner, self: true);

        Assert.Equal([selfLog.Id, general.Id], (await Service.ListAsync(_ctx.Fund.Id, Filter(actorId: _ctx.Owner.Id), default)).Items.Select(item => item.Id));
        Assert.Equal(eventLog.Id, Assert.Single((await Service.ListAsync(_ctx.Fund.Id, Filter(scope: FinancialScope.Event), default)).Items).Id);
        Assert.Equal(eventLog.Id, Assert.Single((await Service.ListAsync(_ctx.Fund.Id, Filter(eventId: _picnic.Id), default)).Items).Id);
        Assert.Equal(2, (await Service.ListAsync(_ctx.Fund.Id, Filter(action: "payment.confirm"), default)).Total);
        Assert.Equal(2, (await Service.ListAsync(_ctx.Fund.Id, Filter(action: "payment."), default)).Total);
        Assert.Equal(0, (await Service.ListAsync(_ctx.Fund.Id, Filter(action: "payment"), default)).Total);
        Assert.Equal(selfLog.Id, Assert.Single((await Service.ListAsync(_ctx.Fund.Id, Filter(selfOnly: true), default)).Items).Id);
    }

    [Fact]
    public void Vietnam_day_boundary_is_utc_for_postgres()
    {
        var start = AuditLogsService.StartOfVietnamDay(new DateOnly(2026, 11, 17));
        Assert.Equal(TimeSpan.Zero, start.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 17, 0, 0, TimeSpan.Zero), start);
    }

    [Fact]
    public async Task Pages_with_total_count()
    {
        for (var i = 0; i < 5; i++)
        {
            Log(ServiceTestContext.Start.AddMinutes(i), "fund.update", _ctx.Owner);
        }

        var page2 = await Service.ListAsync(_ctx.Fund.Id, Filter(page: 2, pageSize: 2), default);

        Assert.Equal(5, page2.Total);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(2, page2.Page);
    }

    [Fact]
    public async Task Returns_summary_raw_before_after_and_masked_ip()
    {
        Log(
            ServiceTestContext.Start,
            "fund.update",
            _ctx.Owner,
            before: """{"Name":"Lớp 10A1","SchoolYear":"2026-2027"}""",
            after: """{"Name":"Lớp 11A1","SchoolYear":"2026-2027"}""",
            ip: IPAddress.Parse("113.190.25.7"));

        var item = Assert.Single((await Service.ListAsync(_ctx.Fund.Id, Filter(), default)).Items);

        Assert.Equal("Lớp 10A1 → Lớp 11A1", item.Summary);
        Assert.Contains("Lớp 10A1", item.Before);
        Assert.Equal("113.190.xx.xx", item.IpAddress);
    }

    [Fact]
    public async Task Actors_are_distinct_people_with_entries_in_this_fund()
    {
        Log(ServiceTestContext.Start, "fund.update", _ctx.Owner);
        Log(ServiceTestContext.Start, "member.add", _ctx.Owner);
        Log(ServiceTestContext.Start, "treasurer.grant", _ctx.Treasurer);
        Log(ServiceTestContext.Start, "fund.update", _ctx.StudentA, fundId: Guid.NewGuid());
        Log(ServiceTestContext.Start, "attachment.cleanup", actor: null);

        var actors = await Service.ListActorsAsync(_ctx.Fund.Id, default);

        Assert.Equal(
            new[] { _ctx.Owner.Id, _ctx.Treasurer.Id }.Order(),
            actors.Select(actor => actor.UserId).Order());
    }
}

public class AuditSummaryTests
{
    [Fact]
    public void Shows_subject_money_extras_and_note()
    {
        var summary = AuditSummary.Build(
            null,
            """{"title":"Mua hoa 20/11","amount":650000,"method":"Cash"}""",
            "ghi nhầm số tiền");

        Assert.Equal("Mua hoa 20/11 · 650.000 đ · phương thức: Tiền mặt · ghi nhầm số tiền", summary);
    }

    [Fact]
    public void Shows_changes_as_old_to_new_and_translates_enum_values()
    {
        Assert.Equal(
            "Trần Quang Huy · vai trò: Học sinh → Thủ quỹ",
            AuditSummary.Build("""{"fullName":"Trần Quang Huy","role":"Student"}""", """{"fullName":"Trần Quang Huy","role":"Treasurer"}""", null));
        Assert.Equal(
            "Văn nghệ · Ngừng dùng",
            AuditSummary.Build("""{"Name":"Văn nghệ","IsActive":true}""", """{"Name":"Văn nghệ","IsActive":false}""", null));
    }

    [Fact]
    public void Uses_before_when_there_is_no_after_and_tolerates_bad_json()
    {
        Assert.Equal("ha.le@truong.edu.vn", AuditSummary.Build("""{"email":"ha.le@truong.edu.vn"}""", null, null));
        Assert.Equal(string.Empty, AuditSummary.Build("not json", "{", null));
        Assert.Equal(string.Empty, AuditSummary.Build(null, null, " "));
    }

    [Fact]
    public void Long_summaries_are_cut()
    {
        var summary = AuditSummary.Build(null, null, new string('a', 500));
        Assert.Equal(AuditSummary.MaxLength, summary.Length);
        Assert.EndsWith("…", summary);
    }

    [Theory]
    [InlineData("113.190.25.7", "113.190.xx.xx")]
    [InlineData("::ffff:10.1.2.3", "10.1.xx.xx")]
    [InlineData("2001:db8:85a3::8a2e:370:7334", "2001:db8:xxxx:…")]
    [InlineData("::1", AuditSummary.LoopbackLabel)]
    [InlineData("127.0.0.1", AuditSummary.LoopbackLabel)]
    [InlineData("::ffff:127.0.0.1", AuditSummary.LoopbackLabel)]
    public void Masks_ip_addresses(string ip, string expected) => Assert.Equal(expected, AuditSummary.MaskIp(IPAddress.Parse(ip)));

    [Fact]
    public void Missing_ip_is_null() => Assert.Null(AuditSummary.MaskIp(null));
}
