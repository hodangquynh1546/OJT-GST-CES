using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Domain;

public class StudentAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 0, 0, 0, TimeSpan.Zero);

    private static User Account(bool mustChangePassword) => new(UserKind.Student, "Học sinh", "hash", null, "hs.khang", mustChangePassword, Now);

    [Fact]
    public void No_account_is_None() => Assert.Equal(StudentAccountStatus.None, StudentAccount.StatusOf(null));

    [Fact]
    public void Temporary_password_is_PendingActivation() =>
        Assert.Equal(StudentAccountStatus.PendingActivation, StudentAccount.StatusOf(Account(mustChangePassword: true)));

    [Fact]
    public void Changed_password_is_Active() =>
        Assert.Equal(StudentAccountStatus.Active, StudentAccount.StatusOf(Account(mustChangePassword: false)));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Disabled_wins_over_temporary_password(bool mustChangePassword)
    {
        var user = Account(mustChangePassword);
        user.Disable(Now);

        Assert.Equal(StudentAccountStatus.Disabled, StudentAccount.StatusOf(user));
    }
}
