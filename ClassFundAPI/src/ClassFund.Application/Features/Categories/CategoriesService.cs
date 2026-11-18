using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Categories;

/// <summary>Danh mục thu / chi của quỹ: thêm, đổi tên, ngừng dùng – dùng lại; không xóa (FR-14).</summary>
public interface ICategoriesService
{
    /// <param name="type">Lọc theo loại; null = cả thu và chi.</param>
    /// <param name="activeOnly">True: chỉ danh mục đang dùng (ô chọn danh mục khi ghi giao dịch).</param>
    Task<IReadOnlyList<CategoryDto>> ListAsync(Guid fundId, LedgerType? type, bool activeOnly, CancellationToken cancellationToken);

    Task<CategoryDto> CreateAsync(Guid fundId, CreateCategoryInput input, CancellationToken cancellationToken);

    Task<CategoryDto> UpdateAsync(Guid fundId, Guid categoryId, UpdateCategoryInput input, CancellationToken cancellationToken);

    /// <summary>
    /// Cho M7: danh mục thuộc quỹ, đang dùng và đúng loại; không thì 422 <c>category.unusable</c>.
    /// Gọi được trong transaction của module khác (chỉ đọc).
    /// </summary>
    Task EnsureUsableAsync(Guid fundId, Guid categoryId, LedgerType type, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class CategoriesService(IAppDb db, ICurrentUser currentUser, AuditTrail audit) : ICategoriesService
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(Guid fundId, LedgerType? type, bool activeOnly, CancellationToken cancellationToken)
    {
        var categories = db.Query<Category>().Where(category => category.FundId == fundId);
        if (type is { } wanted)
        {
            categories = categories.Where(category => category.Type == wanted);
        }

        if (activeOnly)
        {
            categories = categories.Where(category => category.IsActive);
        }

        var rows = await db.ToListAsync(
            from category in categories
            orderby category.Type, category.IsSystem descending, category.Name
            select new
            {
                category.Id,
                category.Name,
                category.Type,
                category.IsSystem,
                category.IsActive,
                UsageCount = db.Query<LedgerEntry>().Count(entry => entry.FundId == fundId && entry.CategoryId == category.Id),
            },
            cancellationToken);

        return rows.Select(row => new CategoryDto(row.Id, row.Name, row.Type, row.IsSystem, row.IsActive, row.UsageCount)).ToArray();
    }

    public async Task<CategoryDto> CreateAsync(Guid fundId, CreateCategoryInput input, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var name = Normalize(input.Name);

        try
        {
            return await db.InTransactionAsync(
                async token =>
                {
                    var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                    YearEndRule.EnsureFundActive(fund.Status);
                    await EnsureNameFreeAsync(fundId, input.Type, name, exceptId: null, token);

                    var category = new Category(fundId, name, input.Type, isSystem: false);
                    db.Add(category);
                    audit.Record(new AuditEntry(
                        CategoriesAuditActions.Create,
                        fundId,
                        userId,
                        FinancialScope.General,
                        EntityType: nameof(Category),
                        EntityId: category.Id,
                        After: new { category.Name, type = category.Type.ToString() }));
                    await db.SaveChangesAsync(token);
                    return ToDto(category, usageCount: 0);
                },
                cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            throw new ConflictException(CategoriesErrors.DuplicateName, CategoriesErrors.DuplicateNameMessage);
        }
    }

    public async Task<CategoryDto> UpdateAsync(Guid fundId, Guid categoryId, UpdateCategoryInput input, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var name = Normalize(input.Name);

        try
        {
            return await db.InTransactionAsync(
                async token =>
                {
                    var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                    YearEndRule.EnsureFundActive(fund.Status);

                    var category = await db.LockAsync<Category>(categoryId, token);
                    if (category is null || category.FundId != fundId)
                    {
                        throw new NotFoundException();
                    }

                    var usageCount = await db.CountAsync(
                        db.Query<LedgerEntry>().Where(entry => entry.FundId == fundId && entry.CategoryId == categoryId),
                        token);

                    if (category.Name == name && category.IsActive == input.IsActive)
                    {
                        return ToDto(category, usageCount);
                    }

                    if (!string.Equals(category.Name, name, StringComparison.Ordinal))
                    {
                        await EnsureNameFreeAsync(fundId, category.Type, name, category.Id, token);
                    }

                    var before = new { category.Name, category.IsActive };
                    category.Name = name;
                    category.IsActive = input.IsActive;
                    audit.Record(new AuditEntry(
                        CategoriesAuditActions.Update,
                        fundId,
                        userId,
                        FinancialScope.General,
                        EntityType: nameof(Category),
                        EntityId: category.Id,
                        Before: before,
                        After: new { category.Name, category.IsActive }));
                    await db.SaveChangesAsync(token);
                    return ToDto(category, usageCount);
                },
                cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            throw new ConflictException(CategoriesErrors.DuplicateName, CategoriesErrors.DuplicateNameMessage);
        }
    }

    public async Task EnsureUsableAsync(Guid fundId, Guid categoryId, LedgerType type, CancellationToken cancellationToken)
    {
        var usable = await db.AnyAsync(
            db.Query<Category>().Where(category =>
                category.Id == categoryId && category.FundId == fundId && category.Type == type && category.IsActive),
            cancellationToken);
        if (!usable)
        {
            throw new BusinessRuleException(CategoriesErrors.Unusable, CategoriesErrors.UnusableMessage);
        }
    }

    /// <summary>Không trùng tên trong cùng quỹ + loại, không phân biệt hoa thường (unique index trong DB phân biệt nên kiểm thêm ở đây).</summary>
    private async Task EnsureNameFreeAsync(Guid fundId, LedgerType type, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();
        var taken = await db.AnyAsync(
            db.Query<Category>().Where(category =>
                category.FundId == fundId
                && category.Type == type
                && category.Id != exceptId
                && category.Name.ToLower() == lowered),
            cancellationToken);
        if (taken)
        {
            throw new ConflictException(CategoriesErrors.DuplicateName, CategoriesErrors.DuplicateNameMessage);
        }
    }

    /// <summary>Bỏ khoảng trắng thừa ở đầu, cuối và giữa tên.</summary>
    public static string Normalize(string name) =>
        string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static CategoryDto ToDto(Category category, int usageCount) =>
        new(category.Id, category.Name, category.Type, category.IsSystem, category.IsActive, usageCount);

    private Guid CurrentUserId() => currentUser.UserId ?? throw new ForbiddenAccessException();
}
