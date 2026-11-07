namespace ClassFund.Domain.Rules;

/// <summary>Mật khẩu 8–128 ký tự, có cả chữ và số (BR-28).</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;
    public const string ViolationCode = "auth.password_policy";
    public const string ViolationMessage = "Mật khẩu cần 8–128 ký tự, có cả chữ và số.";

    public static bool IsSatisfiedBy(string password) =>
        password.Length is >= MinLength and <= MaxLength
        && password.Any(char.IsLetter)
        && password.Any(char.IsDigit);
}
