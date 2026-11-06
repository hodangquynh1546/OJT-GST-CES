using SchoolFund.Application.Common.Security;
using SchoolFund.Domain.Rules;

namespace SchoolFund.UnitTests.Application;

public class CredentialsTests
{
    [Fact]
    public void Temporary_password_has_16_unambiguous_characters_in_fixed_groups()
    {
        for (var i = 0; i < 200; i++)
        {
            var password = Credentials.TemporaryPassword();

            Assert.Matches(@"^[A-HJ-NP-Z][a-km-np-z]{3}-[a-km-np-z]{4}-[2-9]{4}-[a-km-np-z]{4}$", password);
            Assert.Equal(16, password.Count(char.IsLetterOrDigit));
            Assert.True(PasswordPolicy.IsSatisfiedBy(password));
        }
    }

    [Fact]
    public void Temporary_passwords_are_random()
    {
        var passwords = Enumerable.Range(0, 200).Select(_ => Credentials.TemporaryPassword()).ToHashSet();

        Assert.Equal(200, passwords.Count);
    }

    [Theory]
    [InlineData("Phạm Đức Huy", "huy.pham")]
    [InlineData("  Nguyễn   Minh Khang ", "khang.nguyen")]
    [InlineData("Đỗ Hà My", "my.do")]
    [InlineData("Ánh", "anh")]
    [InlineData("!!!", "hocsinh")]
    public void Suggested_username_is_given_name_dot_family_name_dot_number_without_accents(string fullName, string prefix)
    {
        var username = Credentials.SuggestUsername(fullName);

        Assert.Matches($@"^{prefix.Replace(".", @"\.")}\.[1-9]\d{{2}}$", username);
        Assert.True(LoginName.IsValidUsername(username));
    }

    [Fact]
    public void Suggested_username_fits_maximum_length()
    {
        var username = Credentials.SuggestUsername(string.Join(' ', Enumerable.Repeat("Nguyễnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn", 4)));

        Assert.True(username.Length <= LoginName.UsernameMaxLength);
        Assert.True(LoginName.IsValidUsername(username));
    }

    [Theory]
    [InlineData("bao.le", true)]
    [InlineData("hs_01-a", true)]
    [InlineData("ab", false)]
    [InlineData("bảo", false)]
    [InlineData("bao le", false)]
    [InlineData("Bao.Le", false)]
    public void Username_rule_accepts_only_lowercase_ascii_letters_digits_and_dot_underscore_dash(string username, bool valid)
    {
        Assert.Equal(valid, LoginName.IsValidUsername(username));
    }
}
