using ClassFund.Api.Common;

namespace ClassFund.UnitTests.Api;

public class PageRequestValidatorTests
{
    private readonly PageRequestValidator _validator = new();

    [Fact]
    public void Defaults_are_page_1_size_20()
    {
        var request = new PageRequest();
        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.True(_validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData(0, 20, "Page")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 101, "PageSize")]
    public void Rejects_out_of_range(int page, int pageSize, string property)
    {
        var result = _validator.Validate(new PageRequest { Page = page, PageSize = pageSize });
        Assert.Contains(result.Errors, error => error.PropertyName == property);
    }

    private sealed record FilteredRequest : PageRequest
    {
        public string? Search { get; init; }
    }

    private sealed class FilteredValidator : FluentValidation.AbstractValidator<FilteredRequest>
    {
        public FilteredValidator() => this.ValidatePaging();
    }

    [Fact]
    public void Derived_requests_reuse_paging_rules()
    {
        Assert.False(new FilteredValidator().Validate(new FilteredRequest { PageSize = 500 }).IsValid);
    }
}
