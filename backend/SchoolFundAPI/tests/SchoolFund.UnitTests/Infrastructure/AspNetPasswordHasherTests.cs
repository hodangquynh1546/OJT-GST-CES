using Microsoft.AspNetCore.Identity;
using SchoolFund.Application.Common.Security;
using SchoolFund.Domain.Entities;
using SchoolFund.Infrastructure.Security;

namespace SchoolFund.UnitTests.Infrastructure;

public class AspNetPasswordHasherTests
{
    private const string Pepper = "unit-test-pepper-0123456789abcdefgh";
    private readonly AspNetPasswordHasher _hasher = new(Pepper);

    [Fact]
    public void Hash_is_salted_and_verifies_with_same_pepper()
    {
        var first = _hasher.Hash("Matkhau123");
        var second = _hasher.Hash("Matkhau123");

        Assert.NotEqual(first, second);
        Assert.DoesNotContain("Matkhau123", first);
        Assert.Equal(PasswordCheck.Success, _hasher.Verify(first, "Matkhau123"));
        Assert.Equal(PasswordCheck.Failed, _hasher.Verify(first, "SaiMatKhau1"));
    }

    [Fact]
    public void Hash_requires_the_pepper()
    {
        var hash = _hasher.Hash("Matkhau123");

        // Có hash (lộ DB) nhưng không có SECRET_KEY: không xác thực được.
        Assert.Equal(PasswordVerificationResult.Failed, new PasswordHasher<User>().VerifyHashedPassword(null!, hash, "Matkhau123"));
        Assert.Equal(PasswordCheck.Failed, new AspNetPasswordHasher(Pepper + "-khac").Verify(hash, "Matkhau123"));
    }

    [Fact]
    public void Hash_created_before_pepper_is_accepted_and_flagged_for_rehash()
    {
        var legacyHash = new PasswordHasher<User>().HashPassword(null!, "Matkhau123");

        Assert.Equal(PasswordCheck.SuccessRehashNeeded, _hasher.Verify(legacyHash, "Matkhau123"));
        Assert.Equal(PasswordCheck.Failed, _hasher.Verify(legacyHash, "SaiMatKhau1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ngan-hon-32-ky-tu")]
    public void Rejects_missing_or_short_pepper(string pepper)
    {
        Assert.Throws<ArgumentException>(() => new AspNetPasswordHasher(pepper));
    }
}
