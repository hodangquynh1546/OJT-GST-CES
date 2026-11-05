using FluentValidation;
using SchoolFund.Domain.Entities;

namespace SchoolFund.Api.Features.Auth;

// Chính sách mật khẩu (8–128, chữ + số) kiểm tra ở Application → 422 auth.password_policy, không ở đây.

public sealed class RegisterTeacherRequestValidator : AbstractValidator<RegisterTeacherRequest>
{
    public RegisterTeacherRequestValidator()
    {
        RuleFor(request => request.FullName)
            .NotEmpty().WithMessage("Nhập họ tên.")
            .MaximumLength(AuthLimits.FullNameMaxLength).WithMessage("Họ tên tối đa 200 ký tự.");
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Nhập email.")
            .MaximumLength(AuthLimits.EmailMaxLength).WithMessage("Email tối đa 254 ký tự.")
            .EmailAddress().WithMessage("Email không hợp lệ.");
        RuleFor(request => request.Password).NotEmpty().WithMessage("Nhập mật khẩu.");
        RuleFor(request => request.ConfirmPassword)
            .NotEmpty().WithMessage("Nhập lại mật khẩu.")
            .Equal(request => request.Password).WithMessage("Mật khẩu nhập lại không khớp.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.EffectiveLogin)
            .NotEmpty().WithMessage("Nhập email hoặc tên đăng nhập.")
            .MaximumLength(AuthLimits.EmailMaxLength).WithMessage("Email hoặc tên đăng nhập tối đa 254 ký tự.")
            .OverridePropertyName(nameof(LoginRequest.Login));
        RuleFor(request => request.Password).NotEmpty().WithMessage("Nhập mật khẩu.");
    }
}

public sealed class UpdateMeRequestValidator : AbstractValidator<UpdateMeRequest>
{
    public UpdateMeRequestValidator()
    {
        RuleFor(request => request.FullName)
            .NotEmpty().WithMessage("Nhập họ tên.")
            .MaximumLength(AuthLimits.FullNameMaxLength).WithMessage("Họ tên tối đa 200 ký tự.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().WithMessage("Nhập mật khẩu hiện tại.");
        RuleFor(request => request.NewPassword).NotEmpty().WithMessage("Nhập mật khẩu mới.");
    }
}

/// <summary>Độ dài cột theo data-dictionary.md (bảng <see cref="User"/>).</summary>
internal static class AuthLimits
{
    public const int FullNameMaxLength = 200;
    public const int EmailMaxLength = 254;
}
