using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Features.Categories;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Application;

public class CategoriesServiceTests
{
    private readonly ServiceTestContext _ctx = new();
    private readonly Category _food;
    private readonly Category _donation;

    public CategoriesServiceTests()
    {
        _food = new Category(_ctx.Fund.Id, "Bánh kẹo", LedgerType.Expense, isSystem: true);
        _donation = new Category(_ctx.Fund.Id, "Tài trợ", LedgerType.Income, isSystem: false) { IsActive = false };
        _ctx.Db.Seed(_food, _donation);
        _ctx.Db.Seed(new LedgerEntry(_ctx.Fund.Id, null, LedgerType.Expense, LedgerSource.Manual, _food.Id, "Mua bánh", null, 120_000, new DateOnly(2026, 11, 18), _ctx.Owner.Id, ServiceTestContext.Start));
    }

    private CategoriesService Service => new(_ctx.Db, _ctx.CurrentUser, _ctx.Audit);

    [Fact]
    public async Task List_filters_by_type_and_active_and_counts_usage()
    {
        var all = await Service.ListAsync(_ctx.Fund.Id, type: null, activeOnly: false, default);
        Assert.Equal(2, all.Count);
        Assert.Equal(1, all.Single(category => category.Id == _food.Id).UsageCount);

        var expenses = await Service.ListAsync(_ctx.Fund.Id, LedgerType.Expense, activeOnly: false, default);
        Assert.Equal(_food.Id, Assert.Single(expenses).Id);

        var active = await Service.ListAsync(_ctx.Fund.Id, null, activeOnly: true, default);
        Assert.DoesNotContain(active, category => category.Id == _donation.Id);
    }

    [Fact]
    public async Task Create_normalizes_name_and_records_audit()
    {
        var category = await Service.CreateAsync(_ctx.Fund.Id, new CreateCategoryInput("  Văn   nghệ ", LedgerType.Expense), default);

        Assert.Equal("Văn nghệ", category.Name);
        Assert.False(category.IsSystem);
        Assert.True(category.IsActive);
        Assert.Single(_ctx.AuditLogs, entry => entry.Action == CategoriesAuditActions.Create);
    }

    [Fact]
    public async Task Create_duplicate_name_in_same_type_ignoring_case_is_409()
    {
        var error = await Assert.ThrowsAsync<ConflictException>(
            () => Service.CreateAsync(_ctx.Fund.Id, new CreateCategoryInput("BÁNH KẸO", LedgerType.Expense), default));
        Assert.Equal(CategoriesErrors.DuplicateName, error.Code);
    }

    [Fact]
    public async Task Same_name_in_other_type_is_allowed()
    {
        var category = await Service.CreateAsync(_ctx.Fund.Id, new CreateCategoryInput("Bánh kẹo", LedgerType.Income), default);
        Assert.Equal(LedgerType.Income, category.Type);
    }

    [Fact]
    public async Task Update_renames_and_deactivates_used_category_without_deleting_FR14()
    {
        var updated = await Service.UpdateAsync(_ctx.Fund.Id, _food.Id, new UpdateCategoryInput("Bánh kẹo, tiếp khách", IsActive: false), default);

        Assert.Equal("Bánh kẹo, tiếp khách", updated.Name);
        Assert.False(updated.IsActive);
        Assert.Equal(1, updated.UsageCount);
        Assert.Contains(_ctx.Db.Query<Category>(), category => category.Id == _food.Id);
        var log = Assert.Single(_ctx.AuditLogs, entry => entry.Action == CategoriesAuditActions.Update);
        Assert.Contains("\"IsActive\":true", log.BeforeJson);
    }

    [Fact]
    public async Task Reactivate_category()
    {
        var updated = await Service.UpdateAsync(_ctx.Fund.Id, _donation.Id, new UpdateCategoryInput("Tài trợ", IsActive: true), default);
        Assert.True(updated.IsActive);
    }

    [Fact]
    public async Task Category_of_other_fund_is_404()
    {
        var other = new Category(Guid.NewGuid(), "Khác", LedgerType.Expense, false);
        _ctx.Db.Seed(other);
        await Assert.ThrowsAsync<NotFoundException>(
            () => Service.UpdateAsync(_ctx.Fund.Id, other.Id, new UpdateCategoryInput("X", true), default));
    }

    [Fact]
    public async Task Archived_fund_blocks_category_changes_BR37()
    {
        _ctx.Fund.Status = FundStatus.Archived;
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service.CreateAsync(_ctx.Fund.Id, new CreateCategoryInput("Mới", LedgerType.Expense), default));
        Assert.Equal(YearEndRule.FundArchivedCode, error.Code);
    }

    [Fact]
    public async Task Ensure_usable_rejects_inactive_or_wrong_type()
    {
        await Service.EnsureUsableAsync(_ctx.Fund.Id, _food.Id, LedgerType.Expense, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.EnsureUsableAsync(_ctx.Fund.Id, _donation.Id, LedgerType.Income, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.EnsureUsableAsync(_ctx.Fund.Id, _food.Id, LedgerType.Income, default));
    }
}
