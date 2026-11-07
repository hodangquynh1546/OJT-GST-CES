using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.UnitTests.Domain;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

    private static User Student() => new(UserKind.Student, "Học sinh", "hash-tam", null, "Khang.Nguyen", true, Now);

    [Fact]
    public void Change_password_clears_temporary_flag_and_rotates_security_stamp()
    {
        var user = Student();
        var stamp = user.SecurityStamp;

        user.ChangePassword("hash-moi", Now.AddMinutes(1));

        Assert.Equal("hash-moi", user.PasswordHash);
        Assert.False(user.MustChangePassword);
        Assert.NotEqual(stamp, user.SecurityStamp);
        Assert.Equal(Now.AddMinutes(1), user.UpdatedAt);
    }

    [Fact]
    public void Disable_rotates_security_stamp_so_sessions_end()
    {
        var user = Student();
        var stamp = user.SecurityStamp;

        user.Disable(Now);

        Assert.Equal(AccountStatus.Disabled, user.Status);
        Assert.NotEqual(stamp, user.SecurityStamp);
    }

    [Fact]
    public void Upgrading_hash_and_recording_sign_in_keep_the_session()
    {
        var user = Student();
        var stamp = user.SecurityStamp;

        user.UpgradePasswordHash("hash-nang-cap");
        user.RecordSignIn(Now);

        Assert.Equal("hash-nang-cap", user.PasswordHash);
        Assert.Equal(Now, user.LastLoginAt);
        Assert.Equal(stamp, user.SecurityStamp);
        Assert.Equal("khang.nguyen", user.Username);
    }
}
