using FluentValidation;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.Categories;

/// <summary><c>GET /categories?type=&amp;activeOnly=</c>.</summary>
public sealed record CategoryListQuery
{
    public LedgerType? Type { get; init; }

    public bool ActiveOnly { get; init; }
}

public sealed record CreateCategoryRequest(string Name, LedgerType? Type);

public sealed record UpdateCategoryRequest(string Name, bool? IsActive);

internal static class CategoryNameRules
{
    public const int MaxLength = 100;

    public static void Apply<T>(IRuleBuilder<T, string> rule) =>
        rule.Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Vui lòng nhập tên danh mục.")
            .Must(name => name is null || name.Trim().Length <= MaxLength)
            .WithMessage($"Tên danh mục tối đa {MaxLength} ký tự.");
}

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        CategoryNameRules.Apply(RuleFor(request => request.Name));
        RuleFor(request => request.Type).NotNull().WithMessage("Chọn loại thu hoặc chi.");
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        CategoryNameRules.Apply(RuleFor(request => request.Name));
        RuleFor(request => request.IsActive).NotNull().WithMessage("Thiếu trạng thái đang dùng.");
    }
}
