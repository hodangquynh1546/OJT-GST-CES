using System.Text.Json.Serialization;
using SchoolFund.Domain.Enums;

namespace SchoolFund.Application.Features.Categories;

/// <summary>Bản công khai không có <c>isHidden</c> và <c>transactionCount</c> trong JSON (NFR-08, LEAK-08).</summary>
public sealed record CategoryResponse(
    Guid Id,
    TransactionType Type,
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsHidden,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TransactionCount);

public sealed record CategoryCreatedResponse(Guid Id);

public interface ICategoryService
{
    /// <summary>Phạm vi dữ liệu do quyền của người gọi quyết định; bản công khai bỏ qua <paramref name="includeHidden"/>.</summary>
    Task<IReadOnlyList<CategoryResponse>> ListAsync(
        Guid fundId,
        TransactionType? type,
        bool includeHidden,
        CancellationToken cancellationToken);

    /// <exception cref="Domain.Exceptions.BusinessRuleException"><c>category.name_taken</c>.</exception>
    Task<CategoryCreatedResponse> CreateAsync(
        Guid fundId,
        TransactionType type,
        string name,
        CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException">Danh mục không thuộc quỹ.</exception>
    /// <exception cref="Domain.Exceptions.BusinessRuleException"><c>category.name_taken</c>.</exception>
    Task UpdateAsync(
        Guid fundId,
        Guid id,
        string name,
        bool isHidden,
        CancellationToken cancellationToken);
}
