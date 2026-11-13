using ClassFund.Api.Features.Reports;

namespace ClassFund.UnitTests.Api;

/// <summary>Tham số của S-03 (module-plan.md mục M7: <c>year</c> 1–9999, <c>month</c> 1–12, bắt buộc).</summary>
public class ReportsRequestValidatorTests
{
    [Fact]
    public void Monthly_requires_year_and_month()
    {
        var result = new MonthlyReportRequestValidator().Validate(new MonthlyReportRequest());

        Assert.Equal(["Chọn năm.", "Chọn tháng."], result.Errors.Select(error => error.ErrorMessage));
    }

    [Theory]
    [InlineData(0, 1, "Năm từ 1 đến 9999.")]
    [InlineData(10000, 12, "Năm từ 1 đến 9999.")]
    [InlineData(2026, 0, "Tháng từ 1 đến 12.")]
    [InlineData(2026, 13, "Tháng từ 1 đến 12.")]
    public void Monthly_rejects_out_of_range_values(int year, int month, string message)
    {
        var result = new MonthlyReportRequestValidator().Validate(new MonthlyReportRequest { Year = year, Month = month });

        Assert.Equal([message], result.Errors.Select(error => error.ErrorMessage));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(9999, 12)]
    public void Monthly_accepts_bounds(int year, int month) =>
        Assert.True(new MonthlyReportRequestValidator().Validate(new MonthlyReportRequest { Year = year, Month = month }).IsValid);

    [Theory]
    [InlineData(null, "Chọn năm.")]
    [InlineData(0, "Năm từ 1 đến 9999.")]
    [InlineData(10000, "Năm từ 1 đến 9999.")]
    public void Yearly_requires_a_year_in_range(int? year, string message)
    {
        var result = new YearlyReportRequestValidator().Validate(new YearlyReportRequest { Year = year });

        Assert.Equal([message], result.Errors.Select(error => error.ErrorMessage));
    }
}
