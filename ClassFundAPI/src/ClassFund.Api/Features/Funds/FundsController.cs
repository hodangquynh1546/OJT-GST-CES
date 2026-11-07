using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Application.Features.Funds;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Funds;

/// <summary>Quỹ của tôi, tạo quỹ, đổi tên quỹ (docs/api/backend-api.md mục Funds; FR-22, FR-23, FR-11).</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/funds")]
public sealed class FundsController(IFundService funds) : ControllerBase
{
    /// <summary>
    /// Quỹ tôi có vai trò. Mọi vai trò: số dư, trạng thái và tên thủ quỹ;
    /// GVCN thêm <c>studentCount</c>; phụ huynh thêm <c>linkedStudents</c>. Cần đăng nhập, không còn mật khẩu tạm.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FundSummaryResponse>>> ListMine(CancellationToken cancellationToken) =>
        Ok(await funds.ListMineAsync(cancellationToken));

    /// <summary>Chỉ giáo viên. Quỹ mới có 6 danh mục mặc định và trạng thái "Chưa phân công thủ quỹ".</summary>
    [HttpPost]
    [RequirePermission(Permissions.FundCreate)]
    [ProducesResponseType<FundCreatedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateFundRequest request, CancellationToken cancellationToken)
    {
        var created = await funds.CreateAsync(request.Name!, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Chỉ GVCN của quỹ.</summary>
    [HttpPut("{fundId:guid}")]
    [RequirePermission(Permissions.FundManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Rename(Guid fundId, UpdateFundRequest request, CancellationToken cancellationToken)
    {
        await funds.RenameAsync(fundId, request.Name!, cancellationToken);
        return NoContent();
    }
}
