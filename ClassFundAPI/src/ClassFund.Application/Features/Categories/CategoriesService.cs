using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.Application.Features.Categories;

/// <inheritdoc />
/// <remarks>
/// Quyền đã được <c>[RequirePermission]</c> kiểm tra ở API; service kiểm tra lại qua <see cref="IAccessService"/>.
/// Tên duy nhất theo (quỹ, loại) không phân biệt hoa thường: kiểm tra trước để báo lỗi rõ, unique index
/// <c>UQ_Category_Fund_Type_Name</c> là chốt chặn cuối cho hai request đồng thời.
/// </remarks>
public sealed class CategoriesService(
    IAppDb db,
    IAccessService accessService,
    ICurrentUser currentUser,
    AuditTrail auditTrail,
    TimeProvider timeProvider) : ICategoriesService
{
    private const string NameUniqueConstraint = "UQ_Category_Fund_Type_Name";
    private const string EntityType = nameof(Category);

    private Guid UserId => currentUser.UserId
        ?? throw new InvalidOperationException("Thao tác cần người dùng đã đăng nhập.");

    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(
        Guid fundId,
        TransactionType? type,
        bool includeHidden,
        CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);

        var categories = db.Query<Category>().Where(category => category.FundId == fundId);
        if (type is { } selectedType)
        {
            categories = categories.Where(category => category.Type == selectedType);
        }

        var transactions = db.Query<FundTransaction>().Where(transaction => transaction.FundId == fundId);

        // Không có Ledger.ViewFull thì luôn là bản công khai, kể cả khi sau này thêm vai trò mới.
        if (!access.Permissions.Contains(Permissions.LedgerViewFull))
        {
            // X-01: xét toàn bộ lịch sử quỹ; khoản công khai đã hủy vẫn được tính.
            var visible = categories
                .Where(category => !category.IsHidden
                    && transactions.Any(transaction => transaction.CategoryId == category.Id && transaction.IsPublic))
                .OrderBy(category => category.Type)
                .ThenBy(category => category.Name)
                .Select(category => new CategoryResponse(category.Id, category.Type, category.Name, null, null));

            return await db.ToListAsync(visible, cancellationToken);
        }

        if (!includeHidden)
        {
            categories = categories.Where(category => !category.IsHidden);
        }

        var full = categories
            .OrderBy(category => category.Type)
            .ThenBy(category => category.Name)
            .Select(category => new CategoryResponse(
                category.Id,
                category.Type,
                category.Name,
                category.IsHidden,
                transactions.Count(transaction => transaction.CategoryId == category.Id)));

        return await db.ToListAsync(full, cancellationToken);
    }

    public async Task<CategoryCreatedResponse> CreateAsync(
        Guid fundId,
        TransactionType type,
        string name,
        CancellationToken cancellationToken)
    {
        await RequireManageAsync(fundId, cancellationToken);

        var trimmedName = name.Trim();
        await EnsureNameIsFreeAsync(fundId, type, trimmedName, exceptId: null, cancellationToken);

        var category = new Category(fundId, type, trimmedName, timeProvider.GetUtcNow());
        db.Add(category);
        auditTrail.Record(
            CategoriesAuditActions.Create,
            AuditCategory.Change,
            UserId,
            EntityType,
            category.Id,
            newValues: new { fundId, type = type.ToString(), name = trimmedName });

        await SaveAsync(cancellationToken);
        return new CategoryCreatedResponse(category.Id);
    }

    public async Task UpdateAsync(Guid fundId, Guid id, string name, bool isHidden, CancellationToken cancellationToken)
    {
        await RequireManageAsync(fundId, cancellationToken);

        var category = await db.FirstOrDefaultAsync(
            db.Query<Category>().Where(item => item.Id == id && item.FundId == fundId),
            cancellationToken) ?? throw new NotFoundException();

        var trimmedName = name.Trim();
        if (category.Name == trimmedName && category.IsHidden == isHidden)
        {
            return;
        }

        await EnsureNameIsFreeAsync(fundId, category.Type, trimmedName, category.Id, cancellationToken);

        var oldValues = new { name = category.Name, isHidden = category.IsHidden };
        category.Update(trimmedName, isHidden, timeProvider.GetUtcNow());
        auditTrail.Record(
            CategoriesAuditActions.Update,
            AuditCategory.Change,
            UserId,
            EntityType,
            category.Id,
            oldValues,
            new { name = trimmedName, isHidden });

        await SaveAsync(cancellationToken);
    }

    private async Task RequireManageAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.CategoryManage))
        {
            throw new ForbiddenAccessException();
        }
    }

    private async Task EnsureNameIsFreeAsync(
        Guid fundId,
        TransactionType type,
        string name,
        Guid? exceptId,
        CancellationToken cancellationToken)
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
            throw CategoriesErrors.NameTaken();
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == NameUniqueConstraint)
        {
            throw CategoriesErrors.NameTaken();
        }
    }
}
