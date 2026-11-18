using FluentValidation;
using ClassFund.Application.Features.Funds;

namespace ClassFund.Api.Features.Funds;

/// <summary>Body tạo quỹ / sửa quỹ: <c>{ name, schoolYear }</c>.</summary>
public sealed record FundRequest(string Name, string SchoolYear)
{
    public const int NameMaxLength = 100;
}

public sealed class FundRequestValidator : AbstractValidator<FundRequest>
{
    public FundRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Vui lòng nhập tên quỹ.")
            .Must(name => name is null || name.Trim().Length <= FundRequest.NameMaxLength)
            .WithMessage($"Tên quỹ tối đa {FundRequest.NameMaxLength} ký tự.");
        RuleFor(request => request.SchoolYear)
            .Must(year => !string.IsNullOrWhiteSpace(year) && SchoolYearFormat.IsValid(year))
            .WithMessage(SchoolYearFormat.Message);
    }
}
