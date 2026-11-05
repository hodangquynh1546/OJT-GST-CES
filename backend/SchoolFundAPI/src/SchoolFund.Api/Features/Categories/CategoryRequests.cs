using FluentValidation;
using SchoolFund.Domain.Enums;

namespace SchoolFund.Api.Features.Categories;

/// <summary>Các trường để nullable: thiếu trường phải báo lỗi, không được nhận giá trị mặc định của kiểu.</summary>
public sealed record CreateCategoryRequest(TransactionType? Type, string? Name);

public sealed record UpdateCategoryRequest(string? Name, bool? IsHidden);

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(request => request.Type).NotNull().WithMessage("Chọn loại danh mục.");
        RuleFor(request => request.Name).CategoryName();
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(request => request.Name).CategoryName();
        RuleFor(request => request.IsHidden).NotNull().WithMessage("Chọn trạng thái ẩn hoặc hiện.");
    }
}

internal static class CategoryNameRule
{
    private const int MaxLength = 100;

    /// <summary>Độ dài tính sau khi cắt khoảng trắng, đúng với giá trị được lưu.</summary>
    public static void CategoryName<T>(this IRuleBuilder<T, string?> rule) => rule
        .NotEmpty().WithMessage("Nhập tên danh mục.")
        .Must(name => name is null || name.Trim().Length <= MaxLength)
        .WithMessage($"Tên danh mục tối đa {MaxLength} ký tự.");
}
