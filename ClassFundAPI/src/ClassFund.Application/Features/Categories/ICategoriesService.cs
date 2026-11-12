using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Categories;

/// <summary>Danh mục thu / chi của quỹ. Ghi chỉ thủ quỹ (<c>Category.Manage</c>); đọc theo phạm vi quyền. BR-08, BR-25, X-01.</summary>
public interface ICategoriesService
{
    /// <summary>Phạm vi do quyền của người gọi quyết định; bản công khai bỏ qua <paramref name="includeHidden"/>.</summary>
    Task<IReadOnlyList<CategoryResponse>> ListAsync(Guid fundId, TransactionType? type, bool includeHidden, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.ForbiddenAccessException">Không có <c>Category.Manage</c>.</exception>
    /// <exception cref="Domain.Exceptions.BusinessRuleException"><c>category.name_taken</c>.</exception>
    Task<CategoryCreatedResponse> CreateAsync(Guid fundId, TransactionType type, string name, CancellationToken cancellationToken);

    /// <summary>Đổi tên hoặc ẩn / hiện; không đổi gì thì không ghi và không audit.</summary>
    /// <exception cref="Common.Exceptions.ForbiddenAccessException">Không có <c>Category.Manage</c>.</exception>
    /// <exception cref="Common.Exceptions.NotFoundException">Danh mục không thuộc quỹ.</exception>
    /// <exception cref="Domain.Exceptions.BusinessRuleException"><c>category.name_taken</c>.</exception>
    Task UpdateAsync(Guid fundId, Guid id, string name, bool isHidden, CancellationToken cancellationToken);
}
