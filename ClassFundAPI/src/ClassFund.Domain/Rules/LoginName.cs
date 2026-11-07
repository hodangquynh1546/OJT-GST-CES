using System.Text.RegularExpressions;

namespace ClassFund.Domain.Rules;

/// <summary>Chuẩn hóa ô "Email hoặc tên đăng nhập" (D-34): bỏ khoảng trắng hai đầu, chữ thường.</summary>
public static partial class LoginName
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 60;

    public static string Normalize(string value) => value.Trim().ToLowerInvariant();

    /// <summary>Tên đăng nhập học sinh: 3–60 ký tự <c>a-z 0-9 . _ -</c> (BR-29), kiểm tra trên giá trị đã chuẩn hóa.</summary>
    public static bool IsValidUsername(string normalizedUsername) => UsernamePattern().IsMatch(normalizedUsername);

    [GeneratedRegex("^[a-z0-9._-]{3,60}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
