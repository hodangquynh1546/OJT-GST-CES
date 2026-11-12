using ClassFund.Domain.Exceptions;

namespace ClassFund.Application.Features.Categories;

/// <summary>Mã lỗi của M5 Danh mục (docs/api/backend-api.md – Categories). Frontend dựa vào mã này.</summary>
public static class CategoriesErrors
{
    public const string NameTakenCode = "category.name_taken";

    public static BusinessRuleException NameTaken() =>
        new(NameTakenCode, "Tên danh mục đã tồn tại trong loại này.");
}
