using ClassFund.Api.Features.Funds;

namespace ClassFund.UnitTests.Api;

/// <summary>Tên quỹ 1–100 ký tự sau khi cắt khoảng trắng (module-plan.md M2 – mã lỗi <c>validation_failed</c>).</summary>
public class FundsRequestValidatorTests
{
    [Theory]
    [InlineData(null, "Nhập tên quỹ.")]
    [InlineData("", "Nhập tên quỹ.")]
    [InlineData("   ", "Nhập tên quỹ.")]
    public void Empty_name_is_rejected(string? name, string message)
    {
        var result = new CreateFundRequestValidator().Validate(new CreateFundRequest(name));

        Assert.Equal([message], result.Errors.Select(error => error.ErrorMessage));
        Assert.Equal("Name", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Name_longer_than_100_after_trim_is_rejected()
    {
        var result = new UpdateFundRequestValidator().Validate(new UpdateFundRequest(new string('A', 101)));

        Assert.Equal(["Tên quỹ tối đa 100 ký tự."], result.Errors.Select(error => error.ErrorMessage));
    }

    [Theory]
    [InlineData("Quỹ lớp 5A")]
    [InlineData("  Quỹ  ")]
    public void Valid_name_passes(string name)
    {
        Assert.True(new CreateFundRequestValidator().Validate(new CreateFundRequest(name)).IsValid);
        Assert.True(new UpdateFundRequestValidator().Validate(new UpdateFundRequest(name)).IsValid);
    }

    [Fact]
    public void Exactly_100_characters_surrounded_by_spaces_passes()
    {
        var name = $"  {new string('A', 100)}  ";

        Assert.True(new UpdateFundRequestValidator().Validate(new UpdateFundRequest(name)).IsValid);
    }
}
