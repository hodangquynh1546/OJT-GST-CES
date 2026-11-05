namespace SchoolFund.Application.Common.Exceptions;

/// <summary>Không xác thực được; API trả 401 kèm <see cref="Code"/> (<c>auth.invalid_credentials</c>, <c>auth.account_disabled</c>…).</summary>
public sealed class AuthenticationFailedException(string code, string message) : Exception(message)
{
    public const string InvalidCredentialsCode = "auth.invalid_credentials";
    public const string AccountDisabledCode = "auth.account_disabled";
    public const string UnauthorizedCode = "unauthorized";

    public string Code { get; } = code;
}
