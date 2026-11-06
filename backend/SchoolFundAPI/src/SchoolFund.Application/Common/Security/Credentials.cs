using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SchoolFund.Domain.Rules;

namespace SchoolFund.Application.Common.Security;

/// <summary>
/// Sinh thông tin đăng nhập do GVCN cấp (BR-29, TD-86). Mật khẩu tạm chỉ trả về một lần trong response,
/// không lưu dạng rõ, không ghi log / nhật ký (NFR-10, NFR-11).
/// </summary>
public static class Credentials
{
    public const int TemporaryPasswordLength = 16;

    // Bỏ ký tự dễ nhầm khi đọc / chép tay: I, O, l, o, 0, 1.
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";

    private const int SuggestedBaseMaxLength = LoginName.UsernameMaxLength - 4;

    /// <summary>16 ký tự ngẫu nhiên (<see cref="RandomNumberGenerator"/>) dạng <c>Xxxx-xxxx-0000-xxxx</c> (UQ-05).</summary>
    public static string TemporaryPassword()
    {
        var chars = new char[TemporaryPasswordLength];
        for (var i = 0; i < chars.Length; i++)
        {
            var alphabet = i == 0 ? Upper : i is >= 8 and <= 11 ? Digits : Lower;
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        var text = new string(chars);
        return string.Join('-', text[..4], text[4..8], text[8..12], text[12..]);
    }

    /// <summary>
    /// Gợi ý tên đăng nhập từ họ tên: <c>ten.ho.NNN</c> không dấu, chữ thường (BR-29),
    /// ví dụ "Phạm Đức Huy" → <c>huy.pham.583</c>.
    /// </summary>
    public static string SuggestUsername(string fullName)
    {
        var parts = Fold(fullName)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => new string(part.Where(char.IsAsciiLetterOrDigit).ToArray()))
            .Where(part => part.Length > 0)
            .ToArray();

        var name = parts.Length switch
        {
            0 => "hocsinh",
            1 => parts[0],
            _ => $"{parts[^1]}.{parts[0]}",
        };
        if (name.Length > SuggestedBaseMaxLength)
        {
            name = name[..SuggestedBaseMaxLength].TrimEnd('.');
        }

        return $"{name}.{RandomNumberGenerator.GetInt32(100, 1000)}";
    }

    /// <summary>Bỏ dấu tiếng Việt và chuyển chữ thường (đ → d).</summary>
    private static string Fold(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
