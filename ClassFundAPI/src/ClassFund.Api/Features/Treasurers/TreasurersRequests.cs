using FluentValidation;

namespace ClassFund.Api.Features.Treasurers;

public sealed record GrantTreasurerRequest(Guid MemberId);

public sealed class GrantTreasurerRequestValidator : AbstractValidator<GrantTreasurerRequest>
{
    public GrantTreasurerRequestValidator() =>
        RuleFor(request => request.MemberId).NotEmpty().WithMessage("Vui lòng chọn học sinh.");
}
