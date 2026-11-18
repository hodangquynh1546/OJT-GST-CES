namespace ClassFund.Application.Features.Categories;

public static class CategoriesErrors
{
    public const string DuplicateName = "category.duplicate_name";
    public const string DuplicateNameMessage = "Đã có danh mục cùng tên trong loại này.";

    /// <summary>Dùng cho M7 khi ghi chi bằng danh mục đã ngừng dùng (xem <see cref="ICategoriesService.EnsureUsableAsync"/>).</summary>
    public const string Unusable = "category.unusable";
    public const string UnusableMessage = "Danh mục không tồn tại, đã ngừng dùng hoặc không đúng loại thu/chi.";
}
