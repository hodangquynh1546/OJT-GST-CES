using System.Net;
using Moq;
using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Overview;
using ClassFund.Application.Features.Reports;
using ClassFund.Application.Features.Transactions;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.UnitTests.Application;

/// <summary>Một quỹ đủ 4 vai trò cho test báo cáo và tổng quan; giờ cố định ở đầu tháng 12 theo giờ Việt Nam.</summary>
internal sealed class ReportFixture
{
    // 2026-11-30 18:00 UTC = 2026-12-01 01:00 giờ Việt Nam.
    public static readonly DateTimeOffset Now = new(2026, 11, 30, 18, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class NoRequestInfo : IRequestInfo
    {
        public string? CorrelationId => null;

        public IPAddress? IpAddress => null;
    }

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    public ReportFixture()
    {
        var time = new FixedTime();
        var access = new AccessService(_db);
        var transactions = new TransactionService(
            _db,
            access,
            _currentUser.Object,
            new AuditTrail(_db, new NoRequestInfo(), time),
            time);

        Reports = new ReportService(_db, access, _currentUser.Object, transactions);
        Overview = new OverviewService(_db, access, _currentUser.Object, transactions, time);

        Teacher = AddUser(UserKind.Teacher, "Cô Hạnh");
        Treasurer = AddUser(UserKind.Student, "Minh Khang");
        Student = AddUser(UserKind.Student, "Bảo Anh");
        Parent = AddUser(UserKind.Parent, "Phụ huynh");
        Outsider = AddUser(UserKind.Teacher, "Cô Lan");

        Fund = new Fund(Teacher.Id, "Quỹ lớp", Now);
        _db.Add(Fund);

        Dues = AddCategory("Đóng quỹ", TransactionType.Income);
        Sponsor = AddCategory("Tài trợ", TransactionType.Income);
        Supplies = AddCategory("Văn phòng phẩm", TransactionType.Expense);
        Rewards = AddCategory("Khen thưởng", TransactionType.Expense);

        Assignment = new TreasurerAssignment(Fund.Id, AddStudent("Minh Khang", Treasurer).Id, Teacher.Id, Now);
        _db.Add(Assignment);
        _db.Add(new ParentLink(AddStudent("Bảo Anh", Student).Id, Parent.Id, Teacher.Id, Now));

        SignIn(Treasurer);
    }

    public ReportService Reports { get; }

    public OverviewService Overview { get; }

    public User Teacher { get; }

    public User Treasurer { get; }

    public User Student { get; }

    public User Parent { get; }

    public User Outsider { get; }

    public Fund Fund { get; }

    public Category Dues { get; }

    public Category Sponsor { get; }

    public Category Supplies { get; }

    public Category Rewards { get; }

    public TreasurerAssignment Assignment { get; }

    public void SignIn(User user) => _currentUser.SetupGet(current => current.UserId).Returns(user.Id);

    public Student AddStudent(string name, User? account = null, Fund? fund = null)
    {
        var student = new Student((fund ?? Fund).Id, name, Teacher.Id, Now);
        if (account is not null)
        {
            student.LinkAccount(account.Id, Now);
        }

        _db.Add(student);
        return student;
    }

    public Fund AddOtherFund()
    {
        var fund = new Fund(Outsider.Id, "Quỹ khác", Now);
        _db.Add(fund);
        return fund;
    }

    public FundTransaction Add(
        Category category,
        long amount,
        string day,
        bool isPublic = true,
        string description = "Nội dung",
        Fund? fund = null)
    {
        var transaction = new FundTransaction(
            (fund ?? Fund).Id,
            category.Type,
            amount,
            DateOnly.Parse(day),
            category.Id,
            description,
            null,
            isPublic,
            Treasurer.Id,
            Guid.NewGuid(),
            Now);
        _db.Add(transaction);
        return transaction;
    }

    public void Cancel(FundTransaction transaction) => transaction.Cancel("Nhập nhầm", Treasurer.Id, Now);

    public Task<MonthlyReport> MonthlyAsync(int year, int month) =>
        Reports.GetMonthlyAsync(Fund.Id, year, month, CancellationToken.None);

    public Task<YearlyReport> YearlyAsync(int year) => Reports.GetYearlyAsync(Fund.Id, year, CancellationToken.None);

    public Task<FundOverviewResponse> OverviewAsync() => Overview.GetAsync(Fund.Id, CancellationToken.None);

    private User AddUser(UserKind kind, string name)
    {
        var user = new User(kind, name, "hash", $"{Guid.NewGuid():N}@example.vn", null, false, Now);
        _db.Add(user);
        return user;
    }

    private Category AddCategory(string name, TransactionType type)
    {
        var category = new Category(Fund.Id, type, name, Now);
        _db.Add(category);
        return category;
    }
}
