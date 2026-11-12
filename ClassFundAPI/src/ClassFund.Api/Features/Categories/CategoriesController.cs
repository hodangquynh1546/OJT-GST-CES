using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Application.Features.Categories;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.Categories;

/// <summary>M5 – Danh mục thu / chi (backend-api.md mục Categories; BR-08, BR-25, X-01).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/categories")]
public sealed class CategoriesController(ICategoriesService categories) : ApiControllerBase
{
    /// <summary>
    /// GVCN, thủ quỹ thấy cả <c>isHidden</c>, <c>transactionCount</c>; học sinh, phụ huynh chỉ thấy danh mục đang hiện có
    /// khoản công khai, và chỉ <c>id</c>, <c>type</c>, <c>name</c>.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permissions.FundView)]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> List(
        Guid fundId,
        [FromQuery] TransactionType? type,
        [FromQuery] bool includeHidden,
        CancellationToken cancellationToken) =>
        Ok(await categories.ListAsync(fundId, type, includeHidden, cancellationToken));

    /// <summary>Tên trùng trong cùng loại (không phân biệt hoa thường) → 422 <c>category.name_taken</c>.</summary>
    [HttpPost]
    [RequirePermission(Permissions.CategoryManage)]
    [ProducesResponseType<CategoryCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    public async Task<IActionResult> Create(Guid fundId, CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var created = await categories.CreateAsync(fundId, request.Type!.Value, request.Name!, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Đổi tên hoặc ẩn / hiện; danh mục không bao giờ bị xóa.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CategoryManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    public async Task<IActionResult> Update(Guid fundId, Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        await categories.UpdateAsync(fundId, id, request.Name!, request.IsHidden!.Value, cancellationToken);
        return NoContent();
    }
}
