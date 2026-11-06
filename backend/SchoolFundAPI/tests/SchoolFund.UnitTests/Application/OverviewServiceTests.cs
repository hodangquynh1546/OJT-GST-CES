using SchoolFund.Application.Common.Exceptions;
using SchoolFund.Application.Features.Funds;
using SchoolFund.Application.Features.Overview;
using SchoolFund.Domain.Authorization;

namespace SchoolFund.UnitTests.Application;

/// <summary>Tổng quan quỹ và thành viên (FR-01, FR-12, BR-02, BR-15, BR-23).</summary>
public class OverviewServiceTests
{
    private readonly ReportFixture _world = new();

    private Task<IReadOnlyList<FundMemberResponse>> MembersAsync() =>
        _world.Overview.ListMembersAsync(_world.Fund.Id, CancellationToken.None);

    [Fact]
    public async Task Overview_sums_active_transactions_and_uses_the_vietnam_month()
    {
        _world.Add(_world.Dues, 500_000, "2026-11-30");
        _world.Add(_world.Supplies, 120_000, "2026-11-29");
        _world.Add(_world.Dues, 70_000, "2026-12-01");
        _world.Add(_world.Supplies, 20_000, "2026-12-01");
        _world.Cancel(_world.Add(_world.Dues, 999_000, "2026-12-01"));

        var overview = await _world.OverviewAsync();

        Assert.Equal(430_000, overview.CurrentBalance);
        Assert.Equal(570_000, overview.TotalIncome);
        Assert.Equal(140_000, overview.TotalExpense);
        Assert.Equal(70_000, overview.CurrentMonth.Income);
        Assert.Equal(20_000, overview.CurrentMonth.Expense);
        Assert.Equal(50_000, overview.CurrentMonth.Net);
    }

    [Fact]
    public async Task Overview_of_an_empty_fund_returns_zero_figures()
    {
        var overview = await _world.OverviewAsync();

        Assert.Equal(0, overview.CurrentBalance);
        Assert.Equal(0, overview.CurrentMonth.Net);
        Assert.Empty(overview.RecentTransactions);
    }

    [Fact]
    public async Task Balance_can_be_negative()
    {
        _world.Add(_world.Supplies, 10_000, "2026-11-01");

        Assert.Equal(-10_000, (await _world.OverviewAsync()).CurrentBalance);
    }

    [Fact]
    public async Task Figures_are_the_same_for_every_role_but_recent_transactions_follow_the_scope()
    {
        _world.Add(_world.Dues, 100_000, "2026-11-20", description: "Công khai");
        _world.Add(_world.Dues, 40_000, "2026-11-21", isPublic: false, description: "Hoa và phong bì");
        _world.Cancel(_world.Add(_world.Dues, 5_000, "2026-11-22", description: "Đã hủy"));

        _world.SignIn(_world.Teacher);
        var full = await _world.OverviewAsync();
        _world.SignIn(_world.Parent);
        var publicView = await _world.OverviewAsync();

        Assert.Equal(full.CurrentBalance, publicView.CurrentBalance);
        Assert.Equal(full.TotalIncome, publicView.TotalIncome);
        Assert.Equal(full.CurrentMonth, publicView.CurrentMonth);
        Assert.Equal(["Hoa và phong bì", "Công khai"], full.RecentTransactions.Select(item => item.Description));
        var shown = Assert.Single(publicView.RecentTransactions);
        Assert.Equal("Công khai", shown.Description);
        Assert.Null(shown.CreatedByName);
    }

    [Fact]
    public async Task Recent_transactions_are_the_five_latest()
    {
        for (var day = 1; day <= 7; day++)
        {
            _world.Add(_world.Dues, 1_000, $"2026-11-0{day}", description: $"Ngày {day}");
        }

        var overview = await _world.OverviewAsync();

        Assert.Equal(
            ["Ngày 7", "Ngày 6", "Ngày 5", "Ngày 4", "Ngày 3"],
            overview.RecentTransactions.Select(item => item.Description));
    }

    [Fact]
    public async Task Overview_returns_role_permissions_and_managers()
    {
        _world.AddStudent("Học sinh chưa có tài khoản");

        _world.SignIn(_world.Student);
        var overview = await _world.OverviewAsync();

        Assert.Equal(FundRole.Student, overview.Role);
        Assert.Equal([Permissions.FundView, Permissions.LedgerViewPublic], overview.Permissions);
        Assert.Equal(TreasurerStatus.Assigned, overview.TreasurerStatus);
        Assert.Equal(
            [new FundMemberResponse("Cô Hạnh", FundRole.Teacher), new FundMemberResponse("Minh Khang", FundRole.Treasurer)],
            overview.Managers);
    }

    [Fact]
    public async Task Revoked_treasurer_is_no_longer_a_manager()
    {
        _world.Assignment.Revoke(_world.Teacher.Id, ReportFixture.Now);

        _world.SignIn(_world.Teacher);
        var overview = await _world.OverviewAsync();
        var members = await MembersAsync();

        Assert.Equal(TreasurerStatus.Unassigned, overview.TreasurerStatus);
        Assert.Equal(new FundMemberResponse("Cô Hạnh", FundRole.Teacher), Assert.Single(overview.Managers));
        Assert.Contains(new FundMemberResponse("Minh Khang", FundRole.Student), members);
    }

    [Fact]
    public async Task Members_lists_teacher_treasurers_then_students_of_this_fund_only()
    {
        _world.AddStudent("An Nhiên");
        _world.AddStudent("Người lớp khác", fund: _world.AddOtherFund());

        Assert.Equal(
            [
                new FundMemberResponse("Cô Hạnh", FundRole.Teacher),
                new FundMemberResponse("Minh Khang", FundRole.Treasurer),
                new FundMemberResponse("An Nhiên", FundRole.Student),
                new FundMemberResponse("Bảo Anh", FundRole.Student),
            ],
            await MembersAsync());
    }

    [Fact]
    public async Task Public_viewers_cannot_list_members()
    {
        _world.SignIn(_world.Parent);
        await Assert.ThrowsAsync<ForbiddenAccessException>(MembersAsync);

        _world.SignIn(_world.Student);
        await Assert.ThrowsAsync<ForbiddenAccessException>(MembersAsync);
    }

    [Fact]
    public async Task Outsider_gets_not_found()
    {
        _world.SignIn(_world.Outsider);

        await Assert.ThrowsAsync<NotFoundException>(_world.OverviewAsync);
        await Assert.ThrowsAsync<NotFoundException>(MembersAsync);
    }
}
