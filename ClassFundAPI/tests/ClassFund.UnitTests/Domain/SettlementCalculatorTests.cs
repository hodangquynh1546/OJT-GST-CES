using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Domain;

/// <summary>Chia tiền thừa ĐỀU theo đầu người, mỗi phần là bội của 1.000đ (mentor, 2026-11; quyết toán sự kiện và tổng kết cuối năm).</summary>
public class SettlementCalculatorTests
{
    private static Contribution P(long contributed = 0) => new(Guid.NewGuid(), contributed);

    [Fact]
    public void Everyone_gets_the_same_share_regardless_of_what_they_paid()
    {
        var people = new[] { P(300_000), P(100_000), P(0) };

        var shares = SettlementCalculator.AllocateEqually(90_000, people);

        Assert.All(shares, share => Assert.Equal(30_000, share.Amount));
        Assert.Equal(300_000, shares.Single(share => share.UserId == people[0].UserId).Contributed);
    }

    [Fact]
    public void Shares_are_multiples_of_1000_and_the_sub_1000_rest_is_left_unallocated()
    {
        var people = new[] { P(), P(), P() };

        var shares = SettlementCalculator.AllocateEqually(100_500, people); // 100 đơn vị 1.000đ chia 3 → 33, 33, 34

        Assert.All(shares, share => Assert.Equal(0, share.Amount % 1_000));
        Assert.Equal(100_000, shares.Sum(share => share.Amount));
        Assert.Equal([33_000L, 33_000L, 34_000L], shares.Select(share => share.Amount).Order());
    }

    [Fact]
    public void The_extra_units_go_to_the_same_people_every_time()
    {
        var people = Enumerable.Range(0, 5).Select(_ => P()).ToArray();

        var first = SettlementCalculator.AllocateEqually(7_000, people);
        var again = SettlementCalculator.AllocateEqually(7_000, people.Reverse().ToArray());

        Assert.Equal(7_000, first.Sum(share => share.Amount));
        foreach (var share in first)
        {
            Assert.Equal(share.Amount, again.Single(item => item.UserId == share.UserId).Amount);
        }
    }

    [Fact]
    public void One_person_gets_everything_that_can_be_split()
    {
        var shares = SettlementCalculator.AllocateEqually(12_345, [P(250_000)]);

        Assert.Equal(12_000, Assert.Single(shares).Amount);
    }

    [Fact]
    public void A_surplus_below_one_unit_gives_everyone_nothing()
    {
        Assert.All(SettlementCalculator.AllocateEqually(999, [P(), P()]), share => Assert.Equal(0, share.Amount));
    }

    [Fact]
    public void No_surplus_gives_zero_shares_and_needs_no_recipients()
    {
        Assert.All(SettlementCalculator.AllocateEqually(0, [P(10), P(20)]), share => Assert.Equal(0, share.Amount));
        Assert.Empty(SettlementCalculator.AllocateEqually(0, []));
    }

    [Fact]
    public void A_surplus_with_nobody_to_receive_it_cannot_be_split()
    {
        var error = Assert.Throws<BusinessRuleException>(() => SettlementCalculator.AllocateEqually(1_000, []));

        Assert.Equal(SettlementCalculator.NoRecipientsCode, error.Code);
    }

    [Fact]
    public void Large_amounts_do_not_overflow()
    {
        var shares = SettlementCalculator.AllocateEqually(9_000_000_000_000, [P(), P(), P()]);

        Assert.Equal(9_000_000_000_000, shares.Sum(share => share.Amount));
    }

    [Fact]
    public void Negative_surplus_is_a_programming_error() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SettlementCalculator.AllocateEqually(-1, [P()]));
}
