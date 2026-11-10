using FluentValidation;
using ClassFund.Application.Features.Auth;

namespace ClassFund.Api.Features.Auth;

// Chính sách mật khẩu (8–128 ký tự, chữ + số) kiểm tra ở Application → 422 auth.password_policy, không kiểm ở đây.

/// <summary>Đăng ký giáo viên. Không có trường vai trò: tài khoản luôn là giáo viên (D-24).</summary>
public sealed record RegisterTeacherRequest(string? FullName, string? Email, string? Password, string? ConfirmPassword);

/// <summary>Một ô "Email hoặc tên đăng nhập" (D-34), không có tham số vai trò. <see cref="Email"/> chỉ để tương thích client cũ.</summary>
public sealed record LoginRequest(string? Login, string? Email, string? Password)
{
    internal string EffectiveLogin => string.IsNullOrWhiteSpace(Login) ? Email ?? string.Empty : Login;
}

public sealed record UpdateMeRequest(string? FullName);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>Response đổi phiên (đăng ký, đăng nhập, đổi mật khẩu): token antiforgery mới (gửi lại ở header X-XSRF-TOKEN) và người dùng.</summary>
public sealed record SessionResponse(string AntiforgeryToken, MeDto User);

/// <summary>Độ dài cột theo data-dictionary.md (bảng User).</summary>
internal static class AuthLimits
{
    public const int FullNameMaxLength = 200;
    public const int EmailMaxLength = 254;
}

public sealed class RegisterTeacherRequestValidator : AbstractValidator<RegisterTeacherRequest>
{
    public RegisterTeacherRequestValidator()
    {
        RuleFor(request => request.FullName)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Nhập họ tên.")
            .MaximumLength(AuthLimits.FullNameMaxLength).WithMessage("Họ tên tối đa 200 ký tự.");
        RuleFor(request => request.Email)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Nhập email.")
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
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Nhập email hoặc tên đăng nhập.")
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
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Nhập họ tên.")
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
