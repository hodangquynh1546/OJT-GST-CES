using System.Reflection;
using SchoolFund.Api.Authorization;
using SchoolFund.Api.Features.Transactions;
using SchoolFund.Domain.Authorization;
using SchoolFund.Domain.Enums;

namespace SchoolFund.UnitTests.Api;

public class TransactionsApiTests
{
    private static readonly DateOnly Day = new(2026, 11, 5);

    private static CreateTransactionRequest Create(
        TransactionType? type = TransactionType.Income,
        long? amount = 1000,
        string? description = "Nội dung",
        string? note = null,
        Guid? key = null) =>
        new(type, amount, Day, Guid.NewGuid(), description, note, null, key ?? Guid.NewGuid());

    private static string[] PermissionsOf(string action) => typeof(TransactionsController)
        .GetMethod(action)!
        .GetCustomAttributes<RequirePermissionAttribute>()
        .Select(attribute => attribute.Permission)
        .ToArray();

    [Theory]
    [InlineData(nameof(TransactionsController.List), Permissions.FundView)]
    [InlineData(nameof(TransactionsController.Get), Permissions.FundView)]
    [InlineData(nameof(TransactionsController.History), Permissions.LedgerViewFull)]
    [InlineData(nameof(TransactionsController.Create), Permissions.TransactionManage)]
    [InlineData(nameof(TransactionsController.Update), Permissions.TransactionManage)]
    [InlineData(nameof(TransactionsController.Cancel), Permissions.TransactionManage)]
    public void Endpoint_requires_the_documented_permission(string action, string permission)
    {
        Assert.Equal([permission], PermissionsOf(action));
    }

    [Theory]
    [InlineData(FundRole.Teacher)]
    [InlineData(FundRole.Student)]
    [InlineData(FundRole.Parent)]
    public void Only_treasurer_has_transaction_manage(FundRole role)
    {
        Assert.DoesNotContain(Permissions.TransactionManage, RolePermissions.For(role));
        Assert.Contains(Permissions.TransactionManage, RolePermissions.For(FundRole.Treasurer));
    }

    [Theory]
    [InlineData(FundRole.Student)]
    [InlineData(FundRole.Parent)]
    public void Public_viewers_cannot_read_history(FundRole role)
    {
        Assert.DoesNotContain(Permissions.LedgerViewFull, RolePermissions.For(role));
    }

    [Fact]
    public void Update_request_has_no_type_field()
    {
        Assert.DoesNotContain(
            typeof(UpdateTransactionRequest).GetProperties(),
            property => property.Name == nameof(CreateTransactionRequest.Type));
    }

    [Theory]
    [InlineData(1L, true)]
    [InlineData(0L, false)]
    [InlineData(-1L, false)]
    [InlineData(null, false)]
    public void Amount_must_be_positive(long? amount, bool valid)
    {
        Assert.Equal(valid, new CreateTransactionRequestValidator().Validate(Create(amount: amount)).IsValid);
        Assert.Equal(valid, new UpdateTransactionRequestValidator().Validate(new UpdateTransactionRequest(amount, Day, Guid.NewGuid(), "A", null, true)).IsValid);
    }

    [Fact]
    public void Create_requires_type_and_a_real_idempotency_key()
    {
        var validator = new CreateTransactionRequestValidator();

        Assert.False(validator.Validate(Create(type: null)).IsValid);
        Assert.False(validator.Validate(Create(key: Guid.Empty)).IsValid);
        Assert.False(validator.Validate(new CreateTransactionRequest(TransactionType.Income, 1, Day, Guid.NewGuid(), "A", null, null, null)).IsValid);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Description_is_limited_after_trimming(int length, bool valid)
    {
        var description = "  " + new string('a', length) + "  ";

        Assert.Equal(valid, new CreateTransactionRequestValidator().Validate(Create(description: description)).IsValid);
    }

    [Fact]
    public void Description_is_required()
    {
        Assert.False(new CreateTransactionRequestValidator().Validate(Create(description: "   ")).IsValid);
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Note_is_optional_and_limited(int length, bool valid)
    {
        var validator = new CreateTransactionRequestValidator();

        Assert.True(validator.Validate(Create(note: null)).IsValid);
        Assert.Equal(valid, validator.Validate(Create(note: new string('a', length))).IsValid);
    }

    [Fact]
    public void Update_requires_the_public_flag()
    {
        var result = new UpdateTransactionRequestValidator().Validate(new UpdateTransactionRequest(1, Day, Guid.NewGuid(), "A", null, null));

        Assert.Equal(nameof(UpdateTransactionRequest.IsPublic), Assert.Single(result.Errors).PropertyName);
    }

    [Theory]
    [InlineData("Nhập nhầm", true)]
    [InlineData("", false)]
    [InlineData("    ", false)]
    [InlineData(null, false)]
    public void Cancel_requires_a_reason(string? reason, bool valid)
    {
        Assert.Equal(valid, new CancelTransactionRequestValidator().Validate(new CancelTransactionRequest(reason)).IsValid);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Cancel_reason_is_limited(int length, bool valid)
    {
        var reason = new string('a', length);

        Assert.Equal(valid, new CancelTransactionRequestValidator().Validate(new CancelTransactionRequest(reason)).IsValid);
    }

    [Theory]
    [InlineData(1, 20, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    public void Paging_is_limited(int page, int pageSize, bool valid)
    {
        var request = new TransactionListRequest { Page = page, PageSize = pageSize };

        Assert.Equal(valid, new TransactionListRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void Date_range_cannot_be_reversed()
    {
        var request = new TransactionListRequest { From = Day, To = Day.AddDays(-1) };

        Assert.False(new TransactionListRequestValidator().Validate(request).IsValid);
    }
}
