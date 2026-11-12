using ClassFund.Api.Features.Categories;
using ClassFund.Domain.Enums;

namespace ClassFund.UnitTests.Api;

/// <summary>Kiểm tra body danh mục (module-plan.md M5): thiếu loại / tên, tên dài tối đa 100 ký tự sau khi cắt khoảng trắng.</summary>
public class CategoriesRequestValidatorTests
{
    [Theory]
    [InlineData(null, "Quà", "Type", "Chọn loại danh mục.")]
    [InlineData(TransactionType.Expense, null, "Name", "Nhập tên danh mục.")]
    [InlineData(TransactionType.Expense, "", "Name", "Nhập tên danh mục.")]
    [InlineData(TransactionType.Expense, "   ", "Name", "Nhập tên danh mục.")]
    public void Create_rejects_missing_type_or_name(TransactionType? type, string? name, string property, string message)
    {
        var result = new CreateCategoryRequestValidator().Validate(new CreateCategoryRequest(type, name));

        var error = Assert.Single(result.Errors);
        Assert.Equal((property, message), (error.PropertyName, error.ErrorMessage));
    }

    [Fact]
    public void Name_length_is_measured_after_trim()
    {
        var validator = new CreateCategoryRequestValidator();

        Assert.True(validator.Validate(new CreateCategoryRequest(TransactionType.Income, " " + new string('a', 100) + " ")).IsValid);
        var error = Assert.Single(validator.Validate(new CreateCategoryRequest(TransactionType.Income, new string('a', 101))).Errors);
        Assert.Equal("Tên danh mục tối đa 100 ký tự.", error.ErrorMessage);
    }

    [Fact]
    public void Update_requires_name_and_hidden_flag()
    {
        var validator = new UpdateCategoryRequestValidator();

        Assert.True(validator.Validate(new UpdateCategoryRequest("Quà", false)).IsValid);
        var errors = validator.Validate(new UpdateCategoryRequest(null, null)).Errors;
        Assert.Equal(["IsHidden", "Name"], errors.Select(error => error.PropertyName).Order());
        Assert.Contains(errors, error => error.ErrorMessage == "Chọn trạng thái ẩn hoặc hiện.");
    }
}
