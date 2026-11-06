using SchoolFund.Domain.Entities;
using SchoolFund.Domain.Enums;
using SchoolFund.Domain.Exceptions;
using SchoolFund.Domain.Rules;

namespace SchoolFund.UnitTests.Domain;

public class FundTransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

    private static FundTransaction NewTransaction() => new(
        Guid.NewGuid(),
        TransactionType.Income,
        1000,
        new DateOnly(2026, 11, 5),
        Guid.NewGuid(),
        "Nội dung",
        null,
        true,
        Guid.NewGuid(),
        Guid.NewGuid(),
        Now);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_needs_a_reason(string reason)
    {
        var transaction = NewTransaction();

        var exception = Assert.Throws<BusinessRuleException>(() => transaction.Cancel(reason, Guid.NewGuid(), Now));

        Assert.Equal(FundTransaction.CancelReasonRequiredCode, exception.Code);
        Assert.False(transaction.IsCancelled);
    }

    [Fact]
    public void Cancelled_transaction_cannot_be_updated_or_cancelled_again()
    {
        var transaction = NewTransaction();
        transaction.Cancel("Nhập nhầm", Guid.NewGuid(), Now);

        var update = Assert.Throws<BusinessRuleException>(() =>
            transaction.Update(5, new DateOnly(2026, 11, 1), Guid.NewGuid(), "Sửa", null, false, Guid.NewGuid(), Now));
        var cancel = Assert.Throws<BusinessRuleException>(() => transaction.Cancel("Lần hai", Guid.NewGuid(), Now));

        Assert.Equal(FundTransaction.CancelledCode, update.Code);
        Assert.Equal(FundTransaction.CancelledCode, cancel.Code);
        Assert.Equal(1000, transaction.Amount);
        Assert.Equal("Nhập nhầm", transaction.CancelReason);
    }

    [Theory]
    [InlineData("2026-11-05T16:59:59+00:00", "2026-11-05")]
    [InlineData("2026-11-05T17:00:00+00:00", "2026-11-06")]
    public void Vietnam_date_changes_at_midnight_utc_plus_7(string moment, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), VietnamTime.DateOf(DateTimeOffset.Parse(moment)));
    }
}
