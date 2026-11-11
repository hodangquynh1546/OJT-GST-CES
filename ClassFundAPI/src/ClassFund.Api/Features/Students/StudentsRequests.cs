using FluentValidation;

namespace ClassFund.Api.Features.Students;

/// <summary>Trường để nullable: thiếu trường phải báo lỗi tiếng Việt, không nhận giá trị mặc định của kiểu.</summary>
public sealed record CreateStudentRequest(string? FullName, string? ParentEmail, string? ParentFullName);

public sealed record UpdateStudentRequest(string? FullName);

/// <summary>Bỏ trống <c>username</c> → hệ thống gợi ý dạng <c>ten.ho.NNN</c>. Định dạng sai trả 422 <c>user.username</c> (BR-29).</summary>
public sealed record IssueAccountRequest(string? Username);

public sealed record LinkParentRequest(string? Email, string? FullName);

public sealed class CreateStudentRequestValidator : AbstractValidator<CreateStudentRequest>
{
    public CreateStudentRequestValidator()
    {
        RuleFor(request => request.FullName).StudentName();
        RuleFor(request => request.ParentEmail).ParentEmail().When(request => !string.IsNullOrWhiteSpace(request.ParentEmail));
        RuleFor(request => request.ParentFullName).ParentName();
    }
}

public sealed class UpdateStudentRequestValidator : AbstractValidator<UpdateStudentRequest>
{
    public UpdateStudentRequestValidator() => RuleFor(request => request.FullName).StudentName();
}

public sealed class IssueAccountRequestValidator : AbstractValidator<IssueAccountRequest>
{
    // Định dạng tên đăng nhập kiểm tra ở Application (422 user.username); ở đây chỉ chặn chuỗi quá dài.
    public IssueAccountRequestValidator() => RuleFor(request => request.Username)
        .MaximumLength(200).WithMessage("Tên đăng nhập quá dài.");
}

public sealed class LinkParentRequestValidator : AbstractValidator<LinkParentRequest>
{
    public LinkParentRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().WithMessage("Nhập email phụ huynh.").ParentEmail();
        RuleFor(request => request.FullName).ParentName();
    }
}

internal static class StudentRules
{
    private const int StudentNameMaxLength = 100;
    private const int ParentNameMaxLength = 200;
    private const int EmailMaxLength = 254;

    /// <summary>1–100 ký tự sau khi cắt khoảng trắng (Student.FullName varchar(100)).</summary>
    public static IRuleBuilderOptions<T, string?> StudentName<T>(this IRuleBuilder<T, string?> rule) => rule
        .NotEmpty().WithMessage("Nhập họ tên học sinh.")
        .Must(name => name is null || name.Trim().Length <= StudentNameMaxLength)
        .WithMessage($"Họ tên học sinh tối đa {StudentNameMaxLength} ký tự.");

    public static IRuleBuilderOptions<T, string?> ParentEmail<T>(this IRuleBuilder<T, string?> rule) => rule
        .Must(email => email is null || email.Trim().Length <= EmailMaxLength).WithMessage($"Email tối đa {EmailMaxLength} ký tự.")
        .EmailAddress().WithMessage("Email phụ huynh không hợp lệ.");

    /// <summary>Tùy chọn; bỏ trống → "Phụ huynh {tên học sinh}".</summary>
    public static IRuleBuilderOptions<T, string?> ParentName<T>(this IRuleBuilder<T, string?> rule) => rule
        .Must(name => name is null || name.Trim().Length <= ParentNameMaxLength)
        .WithMessage($"Họ tên phụ huynh tối đa {ParentNameMaxLength} ký tự.");
}
