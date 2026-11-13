using Moq;
using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Funds;
using ClassFund.Application.Features.Transactions;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Application;

/// <summary>Quy tắc của module Quỹ (M2): FR-22, FR-23, FR-11, BR-02, BR-36, BR-37, Q-09.</summary>
public class FundsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private FundsService Service()
    {
        var request = new Mock<IRequestInfo>();
        var clock = new FixedClock(Now);
        var access = new AccessService(_db);
        var audit = new AuditTrail(_db, request.Object, clock);
        var transactions = new TransactionsService(_db, access, _currentUser.Object, audit, clock);
        return new FundsService(_db, access, _currentUser.Object, audit, transactions, clock);
    }

    private User SignedIn(UserKind kind)
    {
        var user = new User(kind, "Người dùng", "hash", $"{Guid.NewGuid():N}@example.vn", null, false, Now);
        _db.Add(user);
        _currentUser.SetupGet(current => current.UserId).Returns(user.Id);
        return user;
    }

    private Fund AddFund(Guid ownerId, string name = "Quỹ lớp 5A")
    {
        var fund = new Fund(ownerId, name, Now.AddDays(-1));
        _db.Add(fund);
        return fund;
    }

    [Fact]
    public async Task Create_trims_name_adds_default_categories_and_audit()
    {
        var teacher = SignedIn(UserKind.Teacher);

        var created = await Service().CreateAsync("  Quỹ lớp 5A  ", CancellationToken.None);

        var fund = Assert.Single(_db.Query<Fund>());
        Assert.Equal(created.Id, fund.Id);
        Assert.Equal("Quỹ lớp 5A", fund.Name);
        Assert.Equal(teacher.Id, fund.OwnerUserId);
        Assert.Equal(Now, fund.CreatedAt);
        Assert.Equal(
            DefaultCategories.All,
            _db.Query<Category>().Where(category => category.FundId == fund.Id).Select(category => ValueTuple.Create(category.Type, category.Name)));
        var audit = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(FundsAuditActions.Create, audit.Action);
        Assert.Equal(teacher.Id, audit.ActorUserId);
        Assert.Contains("Đóng quỹ", audit.NewValues);
    }

    [Theory]
    [InlineData(UserKind.Student)]
    [InlineData(UserKind.Parent)]
    public async Task Only_teacher_can_create_fund(UserKind kind)
    {
        SignedIn(kind);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service().CreateAsync("Quỹ", CancellationToken.None));
        Assert.Empty(_db.Query<Fund>());
    }

    [Fact]
    public async Task Rename_changes_name_and_records_old_and_new_values()
    {
        var teacher = SignedIn(UserKind.Teacher);
        var fund = AddFund(teacher.Id);

        await Service().RenameAsync(fund.Id, " Quỹ lớp 5A – 2026 ", CancellationToken.None);

        Assert.Equal("Quỹ lớp 5A – 2026", fund.Name);
        Assert.Equal(Now, fund.UpdatedAt);
        var audit = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(FundsAuditActions.Rename, audit.Action);
        Assert.Contains("Quỹ lớp 5A", audit.OldValues);
        Assert.Contains("Quỹ lớp 5A – 2026", audit.NewValues);
    }

    [Fact]
    public async Task Rename_to_same_name_writes_no_audit()
    {
        var teacher = SignedIn(UserKind.Teacher);
        var fund = AddFund(teacher.Id);

        await Service().RenameAsync(fund.Id, "Quỹ lớp 5A ", CancellationToken.None);

        Assert.Null(fund.UpdatedAt);
        Assert.Empty(_db.Query<AuditLog>());
    }

    [Fact]
    public async Task Rename_fund_of_another_teacher_is_not_found()
    {
        var other = AddFund(Guid.NewGuid());
        SignedIn(UserKind.Teacher);

        await Assert.ThrowsAsync<NotFoundException>(() => Service().RenameAsync(other.Id, "Đổi", CancellationToken.None));
        Assert.Equal("Quỹ lớp 5A", other.Name);
    }

    [Fact]
    public async Task Treasurer_cannot_rename()
    {
        var fund = AddFund(Guid.NewGuid());
        var user = SignedIn(UserKind.Student);
        var student = new Student(fund.Id, "Phạm Thu Trang", fund.OwnerUserId, Now);
        student.LinkAccount(user.Id, Now);
        _db.Add(student);
        _db.Add(new TreasurerAssignment(fund.Id, student.Id, fund.OwnerUserId, Now));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service().RenameAsync(fund.Id, "Đổi", CancellationToken.None));
    }

    [Fact]
    public async Task List_for_teacher_has_balance_without_cancelled_student_count_and_treasurer()
    {
        var teacher = SignedIn(UserKind.Teacher);
        var fund = AddFund(teacher.Id);
        var trang = new Student(fund.Id, "Phạm Thu Trang", teacher.Id, Now);
        _db.Add(trang);
        _db.Add(new Student(fund.Id, "Lê Gia Bảo", teacher.Id, Now));
        _db.Add(new TreasurerAssignment(fund.Id, trang.Id, teacher.Id, Now));
        var revoked = new TreasurerAssignment(fund.Id, Guid.NewGuid(), teacher.Id, Now);
        revoked.Revoke(teacher.Id, Now);
        _db.Add(revoked);
        AddTransaction(fund, TransactionType.Income, 500_000);
        AddTransaction(fund, TransactionType.Expense, 120_000);
        AddTransaction(fund, TransactionType.Expense, 999_000, cancelled: true);

        var summary = Assert.Single(await Service().ListMineAsync(CancellationToken.None));

        Assert.Equal(FundRole.Teacher, summary.Role);
        Assert.Equal(380_000, summary.CurrentBalance);
        Assert.Equal(2, summary.StudentCount);
        Assert.Null(summary.LinkedStudents);
        Assert.Equal(TreasurerStatus.Assigned, summary.TreasurerStatus);
        Assert.Equal(["Phạm Thu Trang"], summary.TreasurerNames);
        Assert.Equal(fund.CreatedAt, summary.CreatedAt);
        Assert.Equal(summary.Permissions.Order(StringComparer.Ordinal), summary.Permissions);
    }

    [Fact]
    public async Task List_for_parent_has_children_and_no_student_count()
    {
        var fund = AddFund(Guid.NewGuid());
        var parent = SignedIn(UserKind.Parent);
        var child = new Student(fund.Id, "Phạm Gia Huy", fund.OwnerUserId, Now);
        _db.Add(child);
        _db.Add(new ParentLink(child.Id, parent.Id, fund.OwnerUserId, Now));

        var summary = Assert.Single(await Service().ListMineAsync(CancellationToken.None));

        Assert.Equal(FundRole.Parent, summary.Role);
        Assert.Null(summary.StudentCount);
        Assert.Equal(["Phạm Gia Huy"], summary.LinkedStudents);
        Assert.Equal(TreasurerStatus.Unassigned, summary.TreasurerStatus);
        Assert.Empty(summary.TreasurerNames);
        Assert.Equal(0, summary.CurrentBalance);
    }

    private void AddTransaction(Fund fund, TransactionType type, long amount, bool cancelled = false)
    {
        var transaction = new FundTransaction(
            fund.Id, type, amount, new DateOnly(2026, 11, 5), Guid.NewGuid(), "Giao dịch", null, true, fund.OwnerUserId, Guid.NewGuid(), Now);
        if (cancelled)
        {
            transaction.Cancel("Nhập nhầm", fund.OwnerUserId, Now);
        }

        _db.Add(transaction);
    }

    // ---------- GetAsync (S-01) ----------

    private User AddStudentMember(Fund fund, string fullName, bool treasurer = false, bool revoked = false)
    {
        var user = new User(UserKind.Student, fullName, "hash", null, $"hs.{Guid.NewGuid():N}"[..12], false, Now);
        _db.Add(user);
        var student = new Student(fund.Id, fullName, fund.OwnerUserId, Now);
        student.LinkAccount(user.Id, Now);
        _db.Add(student);
        if (treasurer)
        {
            var assignment = new TreasurerAssignment(fund.Id, student.Id, fund.OwnerUserId, Now);
            if (revoked)
            {
                assignment.Revoke(fund.OwnerUserId, Now);
            }

            _db.Add(assignment);
        }

        return user;
    }

    private FundTransaction AddTx(
        Fund fund, TransactionType type, long amount, DateOnly on, bool isPublic = true, bool cancelled = false, string description = "Khoản")
    {
        var transaction = new FundTransaction(
            fund.Id, type, amount, on, Guid.NewGuid(), description, null, isPublic, fund.OwnerUserId, Guid.NewGuid(), Now);
        if (cancelled)
        {
            transaction.Cancel("Nhập nhầm", fund.OwnerUserId, Now);
        }

        _db.Add(transaction);
        return transaction;
    }

    [Fact]
    public async Task Get_empty_fund_has_zero_totals_unassigned_status_and_only_the_teacher_as_manager()
    {
        var teacher = SignedIn(UserKind.Teacher);
        var fund = AddFund(teacher.Id);

        var overview = await Service().GetAsync(fund.Id, CancellationToken.None);

        Assert.Equal(FundRole.Teacher, overview.Role);
        Assert.Equal((0L, 0L, 0L), (overview.CurrentBalance, overview.TotalIncome, overview.TotalExpense));
        Assert.Equal(new MonthTotals(0, 0, 0), overview.CurrentMonth);
        Assert.Equal(TreasurerStatus.Unassigned, overview.TreasurerStatus);
        Assert.Equal([("Người dùng", FundRole.Teacher)], overview.Managers.Select(manager => (manager.FullName, manager.Role)));
        Assert.Empty(overview.RecentTransactions);
    }

    [Fact]
    public async Task Get_totals_skip_cancelled_and_current_month_uses_vietnam_time()
    {
        var teacher = SignedIn(UserKind.Teacher);
        var fund = AddFund(teacher.Id);
        AddTx(fund, TransactionType.Income, 1_000_000, new DateOnly(2026, 10, 31));
        AddTx(fund, TransactionType.Income, 500_000, new DateOnly(2026, 11, 1));
        AddTx(fund, TransactionType.Expense, 200_000, new DateOnly(2026, 11, 10));
        AddTx(fund, TransactionType.Expense, 300_000, new DateOnly(2026, 10, 15));
        AddTx(fund, TransactionType.Income, 9_000_000, new DateOnly(2026, 11, 5), cancelled: true);
        AddTx(fund, TransactionType.Expense, 9_000_000, new DateOnly(2026, 11, 6), cancelled: true);

        var overview = await Service().GetAsync(fund.Id, CancellationToken.None);

        Assert.Equal(1_500_000, overview.TotalIncome);
        Assert.Equal(500_000, overview.TotalExpense);
        Assert.Equal(1_000_000, overview.CurrentBalance);
        Assert.Equal(new MonthTotals(500_000, 200_000, 300_000), overview.CurrentMonth);
    }

    [Fact]
    public async Task Get_negative_balance_is_reported_as_is()
    {
        var teacher = SignedIn(UserKind.Teacher);
        var fund = AddFund(teacher.Id);
        AddTx(fund, TransactionType.Income, 100_000, new DateOnly(2026, 11, 2));
        AddTx(fund, TransactionType.Expense, 250_000, new DateOnly(2026, 11, 3));

        var overview = await Service().GetAsync(fund.Id, CancellationToken.None);

        Assert.Equal(-150_000, overview.CurrentBalance);
        Assert.Equal(-150_000, overview.CurrentMonth.Net);
    }

    [Fact]
    public async Task Get_managers_are_teacher_then_active_treasurers_by_name_without_revoked_or_students()
    {
        var teacher = SignedIn(UserKind.Teacher);
        var fund = AddFund(teacher.Id);
        AddStudentMember(fund, "Võ Gia Huy");
        AddStudentMember(fund, "Phạm Thu Trang", treasurer: true);
        AddStudentMember(fund, "Lê Gia Bảo", treasurer: true, revoked: true);

        var overview = await Service().GetAsync(fund.Id, CancellationToken.None);

        Assert.Equal(TreasurerStatus.Assigned, overview.TreasurerStatus);
        Assert.Equal(
            [("Người dùng", FundRole.Teacher), ("Phạm Thu Trang", FundRole.Treasurer)],
            overview.Managers.Select(manager => (manager.FullName, manager.Role)));
    }

    [Fact]
    public async Task Get_recent_transactions_are_the_five_latest_active_ones_newest_first()
    {
        var teacher = SignedIn(UserKind.Teacher);
        var fund = AddFund(teacher.Id);
        for (var day = 1; day <= 7; day++)
        {
            AddTx(fund, TransactionType.Income, 1_000, new DateOnly(2026, 11, day), description: $"Ngày {day}");
        }

        AddTx(fund, TransactionType.Income, 1_000, new DateOnly(2026, 11, 9), cancelled: true, description: "Đã hủy");

        var overview = await Service().GetAsync(fund.Id, CancellationToken.None);

        Assert.Equal(
            ["Ngày 7", "Ngày 6", "Ngày 5", "Ngày 4", "Ngày 3"],
            overview.RecentTransactions.Select(item => item.Description));
    }

    [Fact]
    public async Task Get_public_viewer_has_same_totals_but_recent_list_only_has_public_transactions()
    {
        var teacher = new User(UserKind.Teacher, "Cô Hạnh", "hash", "hanh@example.vn", null, false, Now);
        _db.Add(teacher);
        var fund = AddFund(teacher.Id);
        var student = AddStudentMember(fund, "Võ Gia Huy");
        AddTx(fund, TransactionType.Income, 700_000, new DateOnly(2026, 11, 2), isPublic: true, description: "Công khai");
        AddTx(fund, TransactionType.Expense, 200_000, new DateOnly(2026, 11, 3), isPublic: false, description: "Nội bộ");
        _currentUser.SetupGet(current => current.UserId).Returns(student.Id);

        var overview = await Service().GetAsync(fund.Id, CancellationToken.None);

        Assert.Equal(FundRole.Student, overview.Role);
        Assert.Equal(500_000, overview.CurrentBalance);
        Assert.Equal(new MonthTotals(700_000, 200_000, 500_000), overview.CurrentMonth);
        var item = Assert.Single(overview.RecentTransactions);
        Assert.Equal("Công khai", item.Description);
        Assert.Null(item.IsPublic);
        Assert.Null(item.Note);
    }

    [Fact]
    public async Task Get_hides_a_fund_the_user_does_not_belong_to()
    {
        var owner = new User(UserKind.Teacher, "Cô Hạnh", "hash", "hanh@example.vn", null, false, Now);
        _db.Add(owner);
        var fund = AddFund(owner.Id);
        SignedIn(UserKind.Teacher);

        await Assert.ThrowsAsync<NotFoundException>(() => Service().GetAsync(fund.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => Service().GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
