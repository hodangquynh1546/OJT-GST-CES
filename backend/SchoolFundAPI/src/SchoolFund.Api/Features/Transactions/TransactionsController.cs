using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SchoolFund.Api.Authorization;
using SchoolFund.Application.Common.Paging;
using SchoolFund.Application.Features.Transactions;
using SchoolFund.Domain.Authorization;

namespace SchoolFund.Api.Features.Transactions;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/funds/{fundId:guid}/transactions")]
public sealed class TransactionsController(TransactionService transactions) : ControllerBase
{
    /// <summary>Học sinh, phụ huynh chỉ thấy khoản công khai; mặc định chỉ lấy khoản đang hiệu lực.</summary>
    [HttpGet]
    [RequirePermission(Permissions.FundView)]
    public async Task<ActionResult<PagedResult<TransactionResponse>>> List(
        Guid fundId,
        [FromQuery] TransactionListRequest request,
        CancellationToken cancellationToken) =>
        Ok(await transactions.ListAsync(
            fundId,
            new TransactionFilter(
                request.Type,
                request.CategoryId,
                request.From,
                request.To,
                request.Search,
                request.Status,
                request.Page,
                request.PageSize),
            cancellationToken));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.FundView)]
    public async Task<ActionResult<TransactionResponse>> Get(Guid fundId, Guid id, CancellationToken cancellationToken) =>
        Ok(await transactions.GetAsync(fundId, id, cancellationToken));

    [HttpGet("{id:guid}/history")]
    [RequirePermission(Permissions.LedgerViewFull)]
    public async Task<ActionResult<IReadOnlyList<TransactionHistoryItem>>> History(
        Guid fundId,
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await transactions.GetHistoryAsync(fundId, id, cancellationToken));

    /// <summary>Trả 201; gửi lại cùng <c>idempotencyKey</c> trả 200 với <c>replayed: true</c>.</summary>
    [HttpPost]
    [RequirePermission(Permissions.TransactionManage)]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        Guid fundId,
        CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var saved = await transactions.CreateAsync(
            fundId,
            request.Type!.Value,
            new TransactionValues(
                request.Amount!.Value,
                request.OccurredOn!.Value,
                request.CategoryId!.Value,
                request.Description!,
                request.Note,
                request.IsPublic ?? true),
            request.IdempotencyKey!.Value,
            cancellationToken);

        return saved.Replayed == true ? Ok(saved) : StatusCode(StatusCodes.Status201Created, saved);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.TransactionManage)]
    public async Task<ActionResult<TransactionResponse>> Update(
        Guid fundId,
        Guid id,
        UpdateTransactionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await transactions.UpdateAsync(
            fundId,
            id,
            new TransactionValues(
                request.Amount!.Value,
                request.OccurredOn!.Value,
                request.CategoryId!.Value,
                request.Description!,
                request.Note,
                request.IsPublic!.Value),
            cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(Permissions.TransactionManage)]
    public async Task<ActionResult<TransactionResponse>> Cancel(
        Guid fundId,
        Guid id,
        CancelTransactionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await transactions.CancelAsync(fundId, id, request.Reason!, cancellationToken));
}
