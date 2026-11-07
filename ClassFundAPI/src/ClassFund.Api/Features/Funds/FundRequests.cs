using FluentValidation;

namespace ClassFund.Api.Features.Funds;

/// <summary>Trường để nullable: thiếu trường phải báo lỗi tiếng Việt, không nhận giá trị mặc định của kiểu.</summary>
public sealed record CreateFundRequest(string? Name);

public sealed record UpdateFundRequest(string? Name);

public sealed class CreateFundRequestValidator : AbstractValidator<CreateFundRequest>
{
    public CreateFundRequestValidator() => RuleFor(request => request.Name).FundName();
}

public sealed class UpdateFundRequestValidator : AbstractValidator<UpdateFundRequest>
{
    public UpdateFundRequestValidator() => RuleFor(request => request.Name).FundName();
}

internal static class FundNameRule
{
    private const int MaxLength = 100;

    /// <summary>1–100 ký tự, tính sau khi cắt khoảng trắng – đúng với giá trị được lưu.</summary>
    public static void FundName<T>(this IRuleBuilder<T, string?> rule) => rule
        .NotEmpty().WithMessage("Nhập tên quỹ.")
        .Must(name => name is null || name.Trim().Length <= MaxLength)
        .WithMessage($"Tên quỹ tối đa {MaxLength} ký tự.");
}
