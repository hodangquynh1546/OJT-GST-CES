using ClassFund.Domain.Authorization;

namespace ClassFund.UnitTests.Domain;

/// <summary>Ma trận trong docs/business-analysis/roles-and-permissions.md mục 2 (test case G-01).</summary>
public class RolePermissionsTests
{
    [Theory]
    [InlineData(Permissions.FundView, true, true, true, true)]
    [InlineData(Permissions.LedgerViewFull, true, true, false, false)]
    [InlineData(Permissions.LedgerViewPublic, false, false, true, true)]
    [InlineData(Permissions.FundManage, true, false, false, false)]
    [InlineData(Permissions.StudentManage, true, false, false, false)]
    [InlineData(Permissions.TreasurerAssign, true, false, false, false)]
    [InlineData(Permissions.MemberView, true, true, false, false)]
    [InlineData(Permissions.TransactionManage, false, true, false, false)]
    [InlineData(Permissions.CategoryManage, false, true, false, false)]
    [InlineData(Permissions.FundCreate, false, false, false, false)]
    public void Matrix_matches_the_specification(string permission, bool teacher, bool treasurer, bool student, bool parent)
    {
        Assert.Equal(teacher, RolePermissions.For(FundRole.Teacher).Contains(permission));
        Assert.Equal(treasurer, RolePermissions.For(FundRole.Treasurer).Contains(permission));
        Assert.Equal(student, RolePermissions.For(FundRole.Student).Contains(permission));
        Assert.Equal(parent, RolePermissions.For(FundRole.Parent).Contains(permission));
    }

    [Fact]
    public void Unknown_role_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RolePermissions.For((FundRole)99));
    }
}
