using System.Reflection;
using ClassFund.Api.Authorization;
using ClassFund.Api.Features.Categories;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Enums;

namespace ClassFund.UnitTests.Api;

public class CategoriesApiTests
{
    private static string[] PermissionsOf(string action) => typeof(CategoriesController)
        .GetMethod(action)!
        .GetCustomAttributes<RequirePermissionAttribute>()
        .Select(attribute => attribute.Permission)
        .ToArray();

    [Theory]
    [InlineData(nameof(CategoriesController.List), Permissions.FundView)]
    [InlineData(nameof(CategoriesController.Create), Permissions.CategoryManage)]
    [InlineData(nameof(CategoriesController.Update), Permissions.CategoryManage)]
    public void Endpoint_requires_the_documented_permission(string action, string permission)
    {
        Assert.Equal([permission], PermissionsOf(action));
    }

    [Theory]
    [InlineData(FundRole.Teacher)]
    [InlineData(FundRole.Student)]
    [InlineData(FundRole.Parent)]
    public void Only_treasurer_has_category_manage(FundRole role)
    {
        Assert.DoesNotContain(Permissions.CategoryManage, RolePermissions.For(role));
        Assert.Contains(Permissions.CategoryManage, RolePermissions.For(FundRole.Treasurer));
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("   ", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Create_requires_a_name(string? name, bool valid)
    {
        var result = new CreateCategoryRequestValidator().Validate(new CreateCategoryRequest(TransactionType.Income, name));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(100, 0, true)]
    [InlineData(101, 0, false)]
    [InlineData(100, 4, true)]
    public void Name_length_is_measured_after_trimming(int length, int padding, bool valid)
    {
        var name = new string(' ', padding) + new string('a', length);

        Assert.Equal(valid, new CreateCategoryRequestValidator().Validate(new CreateCategoryRequest(TransactionType.Income, name)).IsValid);
        Assert.Equal(valid, new UpdateCategoryRequestValidator().Validate(new UpdateCategoryRequest(name, false)).IsValid);
    }

    [Fact]
    public void Create_requires_a_type()
    {
        var result = new CreateCategoryRequestValidator().Validate(new CreateCategoryRequest(null, "Photo"));

        Assert.Equal(nameof(CreateCategoryRequest.Type), Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Update_requires_the_hidden_flag()
    {
        var result = new UpdateCategoryRequestValidator().Validate(new UpdateCategoryRequest("Photo", null));

        Assert.Equal(nameof(UpdateCategoryRequest.IsHidden), Assert.Single(result.Errors).PropertyName);
    }
}
