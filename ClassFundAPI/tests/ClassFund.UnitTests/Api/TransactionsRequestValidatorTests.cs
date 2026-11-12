using ClassFund.Api.Features.Transactions;
using ClassFund.Domain.Enums;

namespace ClassFund.UnitTests.Api;

/// <summary>Giới hạn trường của T-01 / S-02 (screen-specifications.md: nội dung 200, ghi chú 1000, lý do hủy 500; số tiền &gt; 0).</summary>
public class TransactionsRequestValidatorTests
{
    private static readonly DateOnly Day = new(2026, 11, 10);

    [Fact]
    public void Create_requires_every_field_and_a_non_empty_key()
    {
        var result = new CreateTransactionRequestValidator().Validate(new CreateTransactionRequest(null, null, null, Guid.Empty, null, null, null, Guid.Empty));

        Assert.Equal(
            ["Type", "Amount", "OccurredOn", "CategoryId", "Description", "IdempotencyKey"],
            result.Errors.Select(error => error.PropertyName));
    }

    [Theory]
    [InlineData(0L, "Số tiền phải lớn hơn 0.")]
    [InlineData(-5L, "Số tiền phải lớn hơn 0.")]
    public void Amount_must_be_positive(long amount, string message)
    {
        var result = new UpdateTransactionRequestValidator().Validate(new UpdateTransactionRequest(amount, Day, Guid.NewGuid(), "Nội dung", null, true));

        Assert.Equal([message], result.Errors.Select(error => error.ErrorMessage));
    }

    [Fact]
    public void Lengths_are_measured_after_trimming()
    {
        var validator = new CreateTransactionRequestValidator();
        var ok = new CreateTransactionRequest(TransactionType.Expense, 1, Day, Guid.NewGuid(), $"  {new string('a', 200)}  ", new string('b', 1000), true, Guid.NewGuid());
        Assert.True(validator.Validate(ok).IsValid);

        var tooLong = ok with { Description = new string('a', 201), Note = new string('b', 1001) };
        Assert.Equal(["Nội dung tối đa 200 ký tự.", "Ghi chú tối đa 1000 ký tự."], validator.Validate(tooLong).Errors.Select(error => error.ErrorMessage));
    }

    [Theory]
    [InlineData(null, "Nhập lý do hủy.")]
    [InlineData("   ", "Nhập lý do hủy.")]
    public void Cancel_requires_reason(string? reason, string message) =>
        Assert.Equal([message], new CancelTransactionRequestValidator().Validate(new CancelTransactionRequest(reason)).Errors.Select(error => error.ErrorMessage));

    [Fact]
    public void Cancel_reason_is_at_most_500_characters()
    {
        var validator = new CancelTransactionRequestValidator();
        Assert.True(validator.Validate(new CancelTransactionRequest(new string('a', 500))).IsValid);
        Assert.Equal(["Lý do hủy tối đa 500 ký tự."], validator.Validate(new CancelTransactionRequest(new string('a', 501))).Errors.Select(error => error.ErrorMessage));
    }

    [Fact]
    public void List_checks_paging_search_and_date_range()
    {
        var validator = new TransactionListRequestValidator();
        Assert.True(validator.Validate(new TransactionListRequest()).IsValid);

        var invalid = new TransactionListRequest { Page = 0, PageSize = 101, Search = new string('a', 201), From = Day, To = Day.AddDays(-1) };

        Assert.Equal(["Page", "PageSize", "Search", "To"], validator.Validate(invalid).Errors.Select(error => error.PropertyName));
    }
}
