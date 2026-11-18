using FluentValidation;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Members;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;

namespace ClassFund.Api.Features.Members;

/// <summary>Query danh sách: <c>?search=&amp;role=&amp;status=&amp;page=&amp;pageSize=</c>.</summary>
public sealed record MemberListRequest : PageRequest
{
    public string? Search { get; init; }

    public MemberRole? Role { get; init; }

    public MemberStatus? Status { get; init; }
}

public sealed record AddMemberRequest(string Email, string FullName);

public sealed record AddMembersRequest(IReadOnlyList<string> Emails);

public sealed record UpdateMemberRequest(string FullName);

public sealed class MemberListRequestValidator : AbstractValidator<MemberListRequest>
{
    public MemberListRequestValidator()
    {
        this.ValidatePaging();
        RuleFor(request => request.Search).MaximumLength(100).WithMessage("Từ khóa tối đa 100 ký tự.");
    }
}

public sealed class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().WithMessage("Vui lòng nhập email.")
            .Must(EmailAddress.IsValid).WithMessage(MembersErrors.InvalidEmailMessage);
        RuleFor(request => request.FullName).NotEmpty().WithMessage("Vui lòng nhập họ tên.")
            .Must(value => value.Trim().Length <= MembersErrors.FullNameMaxLength).WithMessage("Họ tên tối đa 100 ký tự.");
    }
}

public sealed class AddMembersRequestValidator : AbstractValidator<AddMembersRequest>
{
    public AddMembersRequestValidator()
    {
        // Email sai định dạng không làm hỏng cả yêu cầu: được báo riêng từng dòng (Invalid) trong kết quả.
        RuleFor(request => request.Emails).NotNull().WithMessage(MembersErrors.NoEmailsMessage)
            .Must(emails => emails is { Count: > 0 }).WithMessage(MembersErrors.NoEmailsMessage)
            .Must(emails => emails is null || emails.Count <= MembersErrors.MaxBulkEmails).WithMessage(MembersErrors.TooManyEmailsMessage);
    }
}

public sealed class UpdateMemberRequestValidator : AbstractValidator<UpdateMemberRequest>
{
    public UpdateMemberRequestValidator()
    {
        RuleFor(request => request.FullName).NotEmpty().WithMessage("Vui lòng nhập họ tên.")
            .Must(value => value.Trim().Length <= MembersErrors.FullNameMaxLength).WithMessage("Họ tên tối đa 100 ký tự.");
    }
}
