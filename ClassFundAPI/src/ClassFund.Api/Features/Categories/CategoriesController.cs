using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Categories;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Categories;

/// <summary>M5 – Danh mục thu chi (FR-14).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/categories")]
public sealed class CategoriesController(ICategoriesService service) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<CategoryDto>> List(Guid fundId, [FromQuery] CategoryListQuery query, CancellationToken ct) =>
        service.ListAsync(fundId, query.Type, query.ActiveOnly, ct);

    [HttpPost]
    [RequirePermission(Permissions.CategoryManage)]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoryDto>> Create(Guid fundId, CreateCategoryRequest request, CancellationToken ct)
    {
        var category = await service.CreateAsync(fundId, new CreateCategoryInput(request.Name, request.Type!.Value), ct);
        return StatusCode(StatusCodes.Status201Created, category);
    }

    /// <summary>Đổi tên và/hoặc ngừng dùng – dùng lại. Không có xóa.</summary>
    [HttpPut("{categoryId:guid}")]
    [RequirePermission(Permissions.CategoryManage)]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    public Task<CategoryDto> Update(Guid fundId, Guid categoryId, UpdateCategoryRequest request, CancellationToken ct) =>
        service.UpdateAsync(fundId, categoryId, new UpdateCategoryInput(request.Name, request.IsActive!.Value), ct);
}
