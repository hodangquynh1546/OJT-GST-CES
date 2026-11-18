using FluentValidation;
using ClassFund.Application.Features.Auth;
using ClassFund.Domain.Rules;

namespace ClassFund.Api.Features.Auth;

public sealed record RegisterRequest(string FullName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record UpdateProfileRequest(string FullName);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Phản hồi khi đổi phiên (đăng ký, đăng nhập, đổi mật khẩu): token antiforgery mới gắn với phiên + người dùng.</summary>
public sealed record SessionResponse(string AntiforgeryToken, CurrentUserDto User);

internal static class AuthRules
{
    public static IRuleBuilderOptions<T, string> FullName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Vui lòng nhập họ tên.")
            .Must(value => value.Trim().Length <= AuthErrors.FullNameMaxLength)
            .WithMessage("Họ tên tối đa 100 ký tự.");

    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Vui lòng nhập email.")
            .Must(EmailAddress.IsValid).WithMessage(AuthErrors.InvalidEmailMessage);

    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Vui lòng nhập mật khẩu.")
            .Must(PasswordPolicy.IsSatisfiedBy).WithMessage(PasswordPolicy.ViolationMessage);
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.FullName).FullName();
        RuleFor(request => request.Email).Email();
        RuleFor(request => request.Password).NewPassword();
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Đăng nhập chỉ kiểm tra có nhập; không kiểm chính sách mật khẩu để không lộ quy tắc và vẫn đăng nhập được tài khoản cũ.
        RuleFor(request => request.Email).NotEmpty().WithMessage("Vui lòng nhập email.");
        RuleFor(request => request.Password).NotEmpty().WithMessage("Vui lòng nhập mật khẩu.");
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator() => RuleFor(request => request.FullName).FullName();
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại.");
        RuleFor(request => request.NewPassword).NewPassword();
    }
}
