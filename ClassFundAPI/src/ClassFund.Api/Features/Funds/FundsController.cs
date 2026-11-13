using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Application.Features.Funds;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Funds;

/// <summary>M2 – Quỹ của tôi / Quỹ của con, tạo quỹ, đổi tên quỹ (backend-api.md mục Funds; FR-22, FR-23, FR-11).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Prefix + "/funds")]
public sealed class FundsController(IFundsService funds) : ApiControllerBase
{
    /// <summary>
    /// Quỹ tôi có vai trò, sắp theo tên. Mọi vai trò: số dư, trạng thái và tên thủ quỹ;
    /// GVCN thêm <c>studentCount</c>; phụ huynh thêm <c>linkedStudents</c>. Cần đăng nhập, không còn mật khẩu tạm.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FundSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FundSummaryResponse>>> ListMine(CancellationToken cancellationToken) =>
        Ok(await funds.ListMineAsync(cancellationToken));

    /// <summary>
    /// S-01 Tổng quan: số dư, tổng thu / chi, tháng hiện tại (giờ Việt Nam), người quản lý, 5 khoản gần nhất.
    /// Số tổng giống nhau ở mọi vai trò; học sinh / phụ huynh chỉ thấy khoản công khai trong danh sách gần đây.
    /// </summary>
    [HttpGet("{fundId:guid}")]
    [RequirePermission(Permissions.FundView)]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<FundOverviewResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FundOverviewResponse>> Get(Guid fundId, CancellationToken cancellationToken) =>
        Ok(await funds.GetAsync(fundId, cancellationToken));

    /// <summary>Chỉ giáo viên. Quỹ mới có 6 danh mục mặc định và trạng thái "Chưa phân công thủ quỹ".</summary>
    [HttpPost]
    [RequirePermission(Permissions.FundCreate)]
    [ProducesResponseType<FundCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    public async Task<IActionResult> Create(CreateFundRequest request, CancellationToken cancellationToken)
    {
        var created = await funds.CreateAsync(request.Name!, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Chỉ GVCN của quỹ. Tên không đổi thì không ghi AuditLog.</summary>
    [HttpPut("{fundId:guid}")]
    [RequirePermission(Permissions.FundManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    public async Task<IActionResult> Rename(Guid fundId, UpdateFundRequest request, CancellationToken cancellationToken)
    {
        await funds.RenameAsync(fundId, request.Name!, cancellationToken);
        return NoContent();
    }
}
