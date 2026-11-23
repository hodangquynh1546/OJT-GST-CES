using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Features.Balance;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Rules;

namespace ClassFund.Api.Features.Balance;

/// <summary>M10 – Số dư cá nhân của tôi (B-01): chỉ xem số dư và lịch sử biến động.</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/me")]
public sealed class MyBalanceController(IBalanceService service) : ApiControllerBase
{
    [HttpGet("balance")]
    [RequirePermission(Permissions.BalanceViewOwn)]
    [ProducesResponseType<MyBalanceDto>(StatusCodes.Status200OK)]
    public Task<MyBalanceDto> Get(Guid fundId, CancellationToken ct) => service.GetMineAsync(fundId, ct);
}

/// <summary>M10 – "Rút số dư" của thủ quỹ (B-02): danh sách thành viên kèm số dư, rút theo số tiền nhập, lịch sử rút.</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund)]
[RequirePermission(Permissions.BalanceHandle)]
public sealed class WithdrawalsController(IWithdrawalsService service) : ApiControllerBase
{
    [HttpGet("balances")]
    [ProducesResponseType<MemberBalancesResult>(StatusCodes.Status200OK)]
    public Task<MemberBalancesResult> Balances(Guid fundId, [FromQuery] BalanceListRequest request, CancellationToken ct) =>
        service.ListBalancesAsync(fundId, request.Search, request.Page, request.PageSize, ct);

    /// <summary>Rút số tiền nhập: 400 sai định dạng (&lt; 10.000đ hoặc không chẵn nghìn), 422 <c>balance.insufficient</c> khi vượt số dư.</summary>
    [HttpPost("balances/{userId:guid}/withdraw")]
    [ProducesResponseType<MemberBalanceDto>(StatusCodes.Status200OK)]
    public Task<MemberBalanceDto> Withdraw(Guid fundId, Guid userId, WithdrawRequest request, CancellationToken ct) =>
        service.WithdrawAsync(fundId, userId, request.Amount, ct);

    [HttpGet("withdrawals")]
    [ProducesResponseType<PagedResult<WithdrawalDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<WithdrawalDto>> History(Guid fundId, [FromQuery] PageRequest request, CancellationToken ct) =>
        service.HistoryAsync(fundId, request.Page, request.PageSize, ct);
}

/// <summary><c>GET /balances?search=&amp;page=&amp;pageSize=</c>.</summary>
public sealed record BalanceListRequest : PageRequest
{
    public string? Search { get; init; }
}

public sealed class BalanceListRequestValidator : AbstractValidator<BalanceListRequest>
{
    public BalanceListRequestValidator() => this.ValidatePaging();
}

public sealed record WithdrawRequest(long Amount);

public sealed class WithdrawRequestValidator : AbstractValidator<WithdrawRequest>
{
    public WithdrawRequestValidator() =>
        RuleFor(request => request.Amount)
            .GreaterThanOrEqualTo(WithdrawalRule.MinAmount).WithMessage("Số tiền rút tối thiểu 10.000đ.")
            .Must(amount => amount % WithdrawalRule.Step == 0).WithMessage("Số tiền rút phải là bội của 1.000đ (dạng xx.000).");
}
