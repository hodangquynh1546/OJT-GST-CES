using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Application.Features.Categories;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.Categories;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/funds/{fundId:guid}/categories")]
public sealed class CategoriesController(ICategoryService categories) : ControllerBase
{
    /// <summary>GVCN, thủ quỹ thấy số giao dịch; học sinh, phụ huynh chỉ thấy danh mục đang hiện có khoản công khai.</summary>
    [HttpGet]
    [RequirePermission(Permissions.FundView)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> List(
        Guid fundId,
        [FromQuery] TransactionType? type,
        [FromQuery] bool includeHidden,
        CancellationToken cancellationToken) =>
        Ok(await categories.ListAsync(fundId, type, includeHidden, cancellationToken));

    [HttpPost]
    [RequirePermission(Permissions.CategoryManage)]
    [ProducesResponseType<CategoryCreatedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        Guid fundId,
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var created = await categories.CreateAsync(fundId, request.Type!.Value, request.Name!, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Đổi tên hoặc ẩn / hiện; danh mục không bao giờ bị xóa.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CategoryManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        Guid fundId,
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        await categories.UpdateAsync(fundId, id, request.Name!, request.IsHidden!.Value, cancellationToken);
        return NoContent();
    }
}
