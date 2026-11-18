using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Funds;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Funds;

/// <summary>M2 – Quỹ (FR-05 → FR-08).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Prefix + "/funds")]
public sealed class FundsController(IFundsService service) : ApiControllerBase
{
    /// <summary>Quỹ của tôi (mọi người dùng đã đăng nhập; chỉ quỹ mình là thành viên đang hoạt động).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FundSummaryDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<FundSummaryDto>> ListMine(CancellationToken ct) => service.ListMineAsync(ct);

    /// <summary>Tạo quỹ – quyền cấp tài khoản <c>Fund.Create</c> (chỉ tài khoản tự đăng ký).</summary>
    [HttpPost]
    [RequirePermission(Permissions.FundCreate)]
    [ProducesResponseType<FundDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<FundDto>> Create(FundRequest request, CancellationToken ct)
    {
        var fund = await service.CreateAsync(new CreateFundInput(request.Name, request.SchoolYear), ct);
        return CreatedAtAction(nameof(Get), new { fundId = fund.Id, version = "1" }, fund);
    }

    [HttpGet("{fundId:guid}")]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<FundDto>(StatusCodes.Status200OK)]
    public Task<FundDto> Get(Guid fundId, CancellationToken ct) => service.GetAsync(fundId, ct);

    [HttpPut("{fundId:guid}")]
    [RequirePermission(Permissions.FundManage)]
    [ProducesResponseType<FundDto>(StatusCodes.Status200OK)]
    public Task<FundDto> Update(Guid fundId, FundRequest request, CancellationToken ct) =>
        service.UpdateAsync(fundId, new UpdateFundInput(request.Name, request.SchoolYear), ct);
}
