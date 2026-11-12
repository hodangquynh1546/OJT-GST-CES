using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Features.Transactions;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Transactions;

/// <summary>M6 – Thu chi (backend-api.md mục Transactions; FR-02 → FR-07). Xem: mọi thành viên; ghi: thủ quỹ.</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/transactions")]
public sealed class TransactionsController(ITransactionsService transactions) : ApiControllerBase
{
    /// <summary>
    /// Lọc <c>type</c>, <c>categoryId</c>, <c>from</c>, <c>to</c>, <c>search</c>, <c>status</c> (mặc định Active), <c>page</c>, <c>pageSize</c>.
    /// Học sinh, phụ huynh: chỉ khoản công khai, không trường nội bộ, tìm chỉ trong nội dung.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<PagedResult<TransactionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    public async Task<ActionResult<PagedResult<TransactionResponse>>> List(
        Guid fundId,
        [FromQuery] TransactionListRequest request,
        CancellationToken cancellationToken) =>
        Ok(await transactions.ListAsync(
            fundId,
            new TransactionFilter(
                request.Type, request.CategoryId, request.From, request.To, request.Search, request.Status, request.Page, request.PageSize),
            cancellationToken));

    /// <summary>Khoản không công khai với người xem công khai → 404.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TransactionResponse>> Get(Guid fundId, Guid id, CancellationToken cancellationToken) =>
        Ok(await transactions.GetAsync(fundId, id, cancellationToken));

    /// <summary>Lịch sử thay đổi từ AuditLog: <c>[{ occurredAt, action, actorName, oldValues, newValues }]</c>.</summary>
    [HttpGet("{id:guid}/history")]
    [RequirePermission(Permissions.LedgerViewFull)]
    [ProducesResponseType<IReadOnlyList<TransactionHistoryItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TransactionHistoryItem>>> History(Guid fundId, Guid id, CancellationToken cancellationToken) =>
        Ok(await transactions.GetHistoryAsync(fundId, id, cancellationToken));

    /// <summary>201; gửi lại cùng <c>idempotencyKey</c> → 200 <c>replayed: true</c>. Kèm <c>isNegativeBalance</c>.</summary>
    [HttpPost]
    [RequirePermission(Permissions.TransactionManage)]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    public async Task<IActionResult> Create(Guid fundId, CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        var saved = await transactions.CreateAsync(
            fundId,
            request.Type!.Value,
            Values(request.Amount, request.OccurredOn, request.CategoryId, request.Description, request.Note, request.IsPublic ?? true),
            request.IdempotencyKey!.Value,
            cancellationToken);
        return saved.Replayed == true ? Ok(saved) : StatusCode(StatusCodes.Status201Created, saved);
    }

    /// <summary>Đủ các trường; không đổi loại; khoản đã hủy → 422 <c>transaction.cancelled</c>.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.TransactionManage)]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    public async Task<ActionResult<TransactionResponse>> Update(
        Guid fundId,
        Guid id,
        UpdateTransactionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await transactions.UpdateAsync(
            fundId,
            id,
            Values(request.Amount, request.OccurredOn, request.CategoryId, request.Description, request.Note, request.IsPublic!.Value),
            cancellationToken));

    /// <summary><c>reason</c> bắt buộc (1–500); chỉ đánh dấu hủy, không xóa.</summary>
    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(Permissions.TransactionManage)]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    public async Task<ActionResult<TransactionResponse>> Cancel(
        Guid fundId,
        Guid id,
        CancelTransactionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await transactions.CancelAsync(fundId, id, request.Reason!, cancellationToken));

    private static TransactionValues Values(long? amount, DateOnly? occurredOn, Guid? categoryId, string? description, string? note, bool isPublic) =>
        new(amount!.Value, occurredOn!.Value, categoryId!.Value, description!, note, isPublic);
}
