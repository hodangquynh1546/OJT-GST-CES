using FluentValidation;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Charges;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.Charges;

/// <summary>Query danh sách: <c>?status=&amp;page=&amp;pageSize=</c>.</summary>
public sealed record ChargeListRequest : PageRequest
{
    public ChargeStatus? Status { get; init; }
}

/// <summary>Query danh sách đã/chưa đóng: <c>?status=&amp;search=&amp;page=&amp;pageSize=</c>.</summary>
public sealed record AssignmentListRequest : PageRequest
{
    public AssignmentStatus? Status { get; init; }

    public string? Search { get; init; }
}

public sealed record CreateChargeRequest(string Title, string? Purpose, long AmountPerPerson, DateOnly? DueDate, Guid? CategoryId);

public sealed class ChargeListRequestValidator : AbstractValidator<ChargeListRequest>
{
    public ChargeListRequestValidator() => this.ValidatePaging();
}

public sealed class AssignmentListRequestValidator : AbstractValidator<AssignmentListRequest>
{
    public AssignmentListRequestValidator()
    {
        this.ValidatePaging();
        RuleFor(request => request.Search).MaximumLength(100).WithMessage("Từ khóa tối đa 100 ký tự.");
    }
}

public sealed class CreateChargeRequestValidator : AbstractValidator<CreateChargeRequest>
{
    public CreateChargeRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().WithMessage("Vui lòng nhập tên khoản thu.")
            .Must(value => value.Trim().Length <= ChargesErrors.TitleMaxLength).WithMessage("Tên khoản thu tối đa 150 ký tự.");
        RuleFor(request => request.Purpose)
            .Must(value => value is null || value.Trim().Length <= ChargesErrors.PurposeMaxLength)
            .WithMessage("Mục đích tối đa 1000 ký tự.");
        RuleFor(request => request.AmountPerPerson).GreaterThan(0).WithMessage("Số tiền mỗi người phải lớn hơn 0.");
        RuleFor(request => request.CategoryId).NotNull().WithMessage("Vui lòng chọn danh mục thu.");
    }
}
