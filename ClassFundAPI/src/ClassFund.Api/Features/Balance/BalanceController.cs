using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Balance;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.Balance;

/// <summary>M10 – Số dư cá nhân của tôi (B-01).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/me")]
public sealed class MyBalanceController(IBalanceService service) : ApiControllerBase
{
    [HttpGet("balance")]
    [RequirePermission(Permissions.BalanceViewOwn)]
    [ProducesResponseType<MyBalanceDto>(StatusCodes.Status200OK)]
    public Task<MyBalanceDto> Get(Guid fundId, CancellationToken ct) => service.GetMineAsync(fundId, ct);

    /// <summary>Rút toàn bộ số dư (không có body, OQ-11). 422 <c>withdrawal.nothing_to_withdraw</c> / <c>withdrawal.pending_exists</c>.</summary>
    [HttpPost("withdrawal-requests")]
    [RequirePermission(Permissions.BalanceWithdraw)]
    [ProducesResponseType<MyBalanceDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MyBalanceDto>> RequestWithdrawal(Guid fundId, CancellationToken ct)
    {
        var result = await service.RequestWithdrawalAsync(fundId, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}

/// <summary>M10 – Thủ quỹ xử lý yêu cầu rút (B-02).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/withdrawal-requests")]
[RequirePermission(Permissions.BalanceHandle)]
public sealed class WithdrawalsController(IWithdrawalsService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<WithdrawalListResult>(StatusCodes.Status200OK)]
    public Task<WithdrawalListResult> List(Guid fundId, [FromQuery] WithdrawalListQuery query, CancellationToken ct) =>
        service.ListAsync(fundId, query.Status, ct);

    /// <summary>Đã trả tiền: trừ đúng số tiền yêu cầu; đã xử lý → 409 <c>withdrawal.not_pending</c>.</summary>
    [HttpPost("{requestId:guid}/paid")]
    [ProducesResponseType<WithdrawalRequestDto>(StatusCodes.Status200OK)]
    public Task<WithdrawalRequestDto> Paid(Guid fundId, Guid requestId, CancellationToken ct) => service.MarkPaidAsync(fundId, requestId, ct);

    [HttpPost("{requestId:guid}/reject")]
    [ProducesResponseType<WithdrawalRequestDto>(StatusCodes.Status200OK)]
    public Task<WithdrawalRequestDto> Reject(Guid fundId, Guid requestId, RejectWithdrawalRequest request, CancellationToken ct) =>
        service.RejectAsync(fundId, requestId, request.Reason, ct);
}

/// <summary><c>GET /withdrawal-requests?status=Requested|Paid|Rejected</c>.</summary>
public sealed record WithdrawalListQuery
{
    public WithdrawalStatus? Status { get; init; }
}

public sealed record RejectWithdrawalRequest(string Reason);

public sealed class RejectWithdrawalRequestValidator : AbstractValidator<RejectWithdrawalRequest>
{
    public const int ReasonMaxLength = 500;

    public RejectWithdrawalRequestValidator() =>
        RuleFor(request => request.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithMessage("Vui lòng nhập lý do từ chối.")
            .Must(reason => reason is null || reason.Trim().Length <= ReasonMaxLength).WithMessage($"Lý do tối đa {ReasonMaxLength} ký tự.");
}
