using SchoolFund.Domain.Rules;

namespace SchoolFund.UnitTests.Domain;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Matkhau1")]
    [InlineData("12345abc")]
    [InlineData("Mật khẩu 2026")]
    public void Accepts_8_to_128_characters_with_letters_and_digits(string password)
    {
        Assert.True(PasswordPolicy.IsSatisfiedBy(password));
    }

    [Theory]
    [InlineData("abc1234")]
    [InlineData("chicochuthoi")]
    [InlineData("1234567890")]
    [InlineData("")]
    public void Rejects_short_or_missing_letter_or_digit(string password)
    {
        Assert.False(PasswordPolicy.IsSatisfiedBy(password));
    }

    [Fact]
    public void Enforces_maximum_length_of_128()
    {
        Assert.True(PasswordPolicy.IsSatisfiedBy(new string('a', 127) + "1"));
        Assert.False(PasswordPolicy.IsSatisfiedBy(new string('a', 128) + "1"));
    }
}
