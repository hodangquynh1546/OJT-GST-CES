using System.Reflection;
using ClassFund.Api.Authorization;
using ClassFund.Api.Features.Overview;
using ClassFund.Api.Features.Reports;
using ClassFund.Application.Features.Overview;
using ClassFund.Domain.Authorization;

namespace ClassFund.UnitTests.Api;

public class ReportsApiTests
{
    private static string[] PermissionsOf(Type controller, string action) => controller
        .GetMethod(action)!
        .GetCustomAttributes<RequirePermissionAttribute>()
        .Select(attribute => attribute.Permission)
        .ToArray();

    [Theory]
    [InlineData(typeof(OverviewController), nameof(OverviewController.Get), Permissions.FundView)]
    [InlineData(typeof(OverviewController), nameof(OverviewController.Members), Permissions.MemberView)]
    [InlineData(typeof(ReportsController), nameof(ReportsController.Monthly), Permissions.FundView)]
    [InlineData(typeof(ReportsController), nameof(ReportsController.Yearly), Permissions.FundView)]
    public void Endpoint_requires_the_documented_permission(Type controller, string action, string permission)
    {
        Assert.Equal([permission], PermissionsOf(controller, action));
    }

    [Theory]
    [InlineData(FundRole.Student)]
    [InlineData(FundRole.Parent)]
    public void Public_viewers_do_not_have_member_view(FundRole role)
    {
        Assert.DoesNotContain(Permissions.MemberView, RolePermissions.For(role));
    }

    [Fact]
    public void Member_response_has_only_full_name_and_role()
    {
        Assert.Equal(
            [nameof(FundMemberResponse.FullName), nameof(FundMemberResponse.Role)],
            typeof(FundMemberResponse)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.Name != "EqualityContract")
                .Select(property => property.Name));
    }

    [Theory]
    [InlineData(2026, 1, true)]
    [InlineData(2026, 12, true)]
    [InlineData(1, 1, true)]
    [InlineData(9999, 12, true)]
    [InlineData(2026, 0, false)]
    [InlineData(2026, 13, false)]
    [InlineData(0, 6, false)]
    [InlineData(10000, 6, false)]
    [InlineData(null, 6, false)]
    [InlineData(2026, null, false)]
    public void Monthly_request_needs_a_valid_year_and_month(int? year, int? month, bool valid)
    {
        var result = new MonthlyReportRequestValidator().Validate(new MonthlyReportRequest { Year = year, Month = month });

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(2026, true)]
    [InlineData(0, false)]
    [InlineData(null, false)]
    public void Yearly_request_needs_a_valid_year(int? year, bool valid)
    {
        var result = new YearlyReportRequestValidator().Validate(new YearlyReportRequest { Year = year });

        Assert.Equal(valid, result.IsValid);
    }
}
