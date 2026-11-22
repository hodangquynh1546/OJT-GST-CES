using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Domain;

/// <summary>M9-T1: chia tiền thừa theo tỷ lệ số đã đóng, phần dư lớn nhất (OQ-18, BR-22).</summary>
public class SettlementCalculatorTests
{
    private static Contribution C(long amount) => new(Guid.NewGuid(), amount);

    [Fact]
    public void Shares_are_proportional_to_what_each_person_paid()
    {
        var a = C(300_000);
        var b = C(100_000);

        var shares = SettlementCalculator.Allocate(40_000, [a, b]);

        Assert.Equal(30_000, shares.Single(share => share.UserId == a.UserId).Amount);
        Assert.Equal(10_000, shares.Single(share => share.UserId == b.UserId).Amount);
    }

    [Fact]
    public void The_total_always_equals_the_surplus_even_when_it_does_not_divide_evenly()
    {
        var people = new[] { C(100), C(100), C(100) };

        var shares = SettlementCalculator.Allocate(100, people);

        Assert.Equal(100, shares.Sum(share => share.Amount));
        Assert.All(shares, share => Assert.InRange(share.Amount, 33, 34));
    }

    [Fact]
    public void The_extra_coin_goes_to_the_largest_remainder_then_the_larger_contribution()
    {
        var small = C(1);
        var large = C(2);

        // 10 * 1/3 = 3.33, 10 * 2/3 = 6.67 → phần dư lớn hơn thuộc người đóng nhiều
        var shares = SettlementCalculator.Allocate(10, [small, large]);

        Assert.Equal(3, shares.Single(share => share.UserId == small.UserId).Amount);
        Assert.Equal(7, shares.Single(share => share.UserId == large.UserId).Amount);
    }

    [Fact]
    public void One_person_gets_everything()
    {
        var only = C(250_000);

        var shares = SettlementCalculator.Allocate(12_345, [only]);

        Assert.Equal(12_345, Assert.Single(shares).Amount);
    }

    [Fact]
    public void Someone_who_paid_nothing_gets_nothing()
    {
        var paid = C(50_000);
        var nothing = C(0);

        var shares = SettlementCalculator.Allocate(7_001, [paid, nothing]);

        Assert.Equal(7_001, shares.Single(share => share.UserId == paid.UserId).Amount);
        Assert.Equal(0, shares.Single(share => share.UserId == nothing.UserId).Amount);
    }

    [Fact]
    public void No_surplus_gives_zero_shares_and_needs_no_contributors()
    {
        Assert.All(SettlementCalculator.Allocate(0, [C(10), C(20)]), share => Assert.Equal(0, share.Amount));
        Assert.Empty(SettlementCalculator.Allocate(0, []));
    }

    [Fact]
    public void A_surplus_with_nobody_who_paid_cannot_be_split()
    {
        var error = Assert.Throws<BusinessRuleException>(() => SettlementCalculator.Allocate(1_000, [C(0), C(0)]));

        Assert.Equal(SettlementCalculator.NoContributorsCode, error.Code);
        Assert.Throws<BusinessRuleException>(() => SettlementCalculator.Allocate(1_000, []));
    }

    [Fact]
    public void Large_amounts_do_not_overflow()
    {
        var shares = SettlementCalculator.Allocate(9_000_000_000_000, [C(4_000_000_000_000), C(5_000_000_000_000)]);

        Assert.Equal(9_000_000_000_000, shares.Sum(share => share.Amount));
    }

    [Fact]
    public void Negative_surplus_is_a_programming_error()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SettlementCalculator.Allocate(-1, [C(1)]));
    }
}
