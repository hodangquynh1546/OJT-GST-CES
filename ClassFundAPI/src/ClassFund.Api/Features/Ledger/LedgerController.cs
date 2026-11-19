using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Ledger;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Ledger;

/// <summary>M7 – Sổ thu chi theo phạm vi (FR-26 → FR-29). Học sinh chỉ đọc (OQ-14).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/ledger-entries")]
public sealed class LedgerController(ILedgerService service) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<LedgerListResult>(StatusCodes.Status200OK)]
    public Task<LedgerListResult> List(Guid fundId, [FromQuery] LedgerListRequest request, CancellationToken ct) =>
        service.ListAsync(
            fundId,
            new LedgerFilter(request.EventId, request.Type, request.CategoryId, request.From, request.To, request.Page, request.PageSize),
            ct);

    [HttpGet("{entryId:guid}")]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<LedgerEntryDetailDto>(StatusCodes.Status200OK)]
    public Task<LedgerEntryDetailDto> Get(Guid fundId, Guid entryId, CancellationToken ct) => service.GetAsync(fundId, entryId, ct);

    /// <summary>Chỉ ghi chi (OQ-18); 422 <c>ledger.exceeds_balance</c> khi vượt số dư phạm vi, <c>evidence.required</c> khi &gt; 500.000đ thiếu chứng từ.</summary>
    [HttpPost]
    [RequirePermission(Permissions.LedgerManage)]
    [ProducesResponseType<LedgerEntryDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<LedgerEntryDetailDto>> Create(Guid fundId, CreateExpenseRequest request, CancellationToken ct)
    {
        var entry = await service.CreateExpenseAsync(
            fundId,
            new CreateExpenseInput(
                request.EventId,
                request.CategoryId!.Value,
                request.Title,
                request.Purpose,
                request.Amount!.Value,
                request.TransactionDate!.Value,
                request.Note,
                request.AttachmentIds ?? []),
            ct);
        return StatusCode(StatusCodes.Status201Created, entry);
    }

    /// <summary>Bút toán đảo (FR-29): trả về dòng đảo vừa tạo; dòng gốc giữ nguyên.</summary>
    [HttpPost("{entryId:guid}/reverse")]
    [RequirePermission(Permissions.LedgerManage)]
    [ProducesResponseType<LedgerEntryDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<LedgerEntryDetailDto>> Reverse(Guid fundId, Guid entryId, ReverseLedgerEntryRequest request, CancellationToken ct)
    {
        var reversal = await service.ReverseAsync(fundId, entryId, request.Reason, ct);
        return StatusCode(StatusCodes.Status201Created, reversal);
    }
}
