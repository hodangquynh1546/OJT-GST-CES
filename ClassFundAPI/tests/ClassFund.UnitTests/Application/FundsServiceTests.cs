using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Features.Funds;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Application;

public class FundsServiceTests
{
    private readonly ServiceTestContext _ctx = new();

    private FundsService Service => new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Audit, _ctx.Clock);

    [Fact]
    public async Task Create_makes_creator_owner_and_seeds_default_categories_FR05()
    {
        _ctx.ActAs(_ctx.Outsider);

        var fund = await Service.CreateAsync(new CreateFundInput("  CLB Tiếng Anh ", "2026 – 2027"), default);

        Assert.Equal("CLB Tiếng Anh", fund.Name);
        Assert.Equal("2026-2027", fund.SchoolYear);
        Assert.Equal(MemberRole.Owner, fund.MyRole);
        Assert.Equal(1, fund.MemberCount);
        Assert.Equal(_ctx.Outsider.FullName, fund.OwnerName);
        var categories = _ctx.Db.Query<Category>().Where(category => category.FundId == fund.Id).ToList();
        Assert.Equal(DefaultCategories.All.Count, categories.Count);
        Assert.All(categories, category => Assert.True(category.IsSystem && category.IsActive));
        var log = Assert.Single(_ctx.AuditLogs, entry => entry.Action == FundsAuditActions.Create);
        Assert.Equal(fund.Id, log.FundId);
    }

    [Fact]
    public async Task List_mine_shows_role_members_general_balance_only_and_pending_for_confirmers()
    {
        var eventId = Guid.NewGuid();
        _ctx.Db.Seed(
            Entry(LedgerType.Income, 1_000_000, eventId: null),
            Entry(LedgerType.Expense, 300_000, eventId: null),
            Entry(LedgerType.Income, 5_000_000, eventId)); // tiền sự kiện không cộng vào quỹ chung (BR-02)
        var charge = new Charge(_ctx.Fund.Id, null, "Quỹ tháng 11", null, 50_000, new DateOnly(2026, 11, 30), ChargeKind.Regular, _ctx.Owner.Id, ServiceTestContext.Start);
        var assignment = new ChargeAssignment(charge.Id, _ctx.StudentA.Id, 50_000);
        _ctx.Db.Seed(charge);
        _ctx.Db.Seed(assignment);
        _ctx.Db.Seed(new Payment(assignment.Id, _ctx.StudentA.Id, 50_000, PaymentMethod.BankTransfer, ServiceTestContext.Start));

        var asOwner = Assert.Single(await Service.ListMineAsync(default));
        Assert.Equal(MemberRole.Owner, asOwner.MyRole);
        Assert.Equal(4, asOwner.MemberCount);
        Assert.Equal(700_000, asOwner.GeneralBalance);
        Assert.Equal(1, asOwner.PendingPaymentCount);

        _ctx.ActAs(_ctx.StudentA);
        var asStudent = Assert.Single(await Service.ListMineAsync(default));
        Assert.Equal(MemberRole.Student, asStudent.MyRole);
        Assert.Null(asStudent.PendingPaymentCount);
    }

    [Fact]
    public async Task List_mine_is_empty_for_non_member()
    {
        _ctx.ActAs(_ctx.Outsider);
        Assert.Empty(await Service.ListMineAsync(default));
    }

    [Fact]
    public async Task Outsider_cannot_view_fund_404() =>
        await AssertOutsider(() => Service.GetAsync(_ctx.Fund.Id, default));

    [Fact]
    public async Task Update_changes_name_and_year_with_before_after_audit_FR06()
    {
        var fund = await Service.UpdateAsync(_ctx.Fund.Id, new UpdateFundInput("Lớp 11A1", "2027-2028"), default);

        Assert.Equal("Lớp 11A1", fund.Name);
        Assert.Equal("2027-2028", fund.SchoolYear);
        var log = Assert.Single(_ctx.AuditLogs, entry => entry.Action == FundsAuditActions.Update);
        Assert.Contains("Lớp 10A1", log.BeforeJson);
        Assert.Contains("Lớp 11A1", log.AfterJson);
    }

    [Fact]
    public async Task Update_without_changes_writes_no_audit()
    {
        await Service.UpdateAsync(_ctx.Fund.Id, new UpdateFundInput("Lớp 10A1", "2026-2027"), default);
        Assert.DoesNotContain(_ctx.AuditLogs, entry => entry.Action == FundsAuditActions.Update);
    }

    [Fact]
    public async Task Archived_fund_cannot_be_updated_BR37()
    {
        _ctx.Fund.Status = FundStatus.Archived;
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service.UpdateAsync(_ctx.Fund.Id, new UpdateFundInput("X", "2026-2027"), default));
        Assert.Equal(YearEndRule.FundArchivedCode, error.Code);
    }

    [Theory]
    [InlineData("2026-2027", true)]
    [InlineData("2026–2027", true)]
    [InlineData("2026-2028", false)]
    [InlineData("26-27", false)]
    [InlineData("abcd-efgh", false)]
    public void School_year_format(string value, bool valid) => Assert.Equal(valid, SchoolYearFormat.IsValid(value));

    private LedgerEntry Entry(LedgerType type, long amount, Guid? eventId) =>
        new(_ctx.Fund.Id, eventId, type, LedgerSource.Manual, null, "GD", null, amount, new DateOnly(2026, 11, 18), _ctx.Owner.Id, ServiceTestContext.Start);

    private async Task AssertOutsider(Func<Task> action)
    {
        _ctx.ActAs(_ctx.Outsider);
        await Assert.ThrowsAsync<NotFoundException>(action);
    }
}
