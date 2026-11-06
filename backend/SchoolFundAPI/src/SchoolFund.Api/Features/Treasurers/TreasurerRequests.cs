using FluentValidation;

namespace SchoolFund.Api.Features.Treasurers;

/// <summary>Trường để nullable: thiếu <c>studentId</c> phải báo lỗi tiếng Việt, không nhận <c>Guid.Empty</c>.</summary>
public sealed record AssignTreasurerRequest(Guid? StudentId);

public sealed class AssignTreasurerRequestValidator : AbstractValidator<AssignTreasurerRequest>
{
    public AssignTreasurerRequestValidator() => RuleFor(request => request.StudentId)
        .NotNull().WithMessage("Chọn học sinh.")
        .NotEqual(Guid.Empty).WithMessage("Chọn học sinh.");
}
