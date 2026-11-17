using System.Security.Cryptography;

namespace ClassFund.Application.Common.Security;

/// <summary>
/// Sinh mật khẩu tạm do thủ quỹ cấp cho học sinh. Chỉ trả về MỘT lần trong response, không lưu dạng rõ,
/// không ghi log / audit.
/// </summary>
public static class Credentials
{
    public const int TemporaryPasswordLength = 16;

    // Bỏ ký tự dễ nhầm khi đọc / chép tay: I, O, l, o, 0, 1.
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";

    /// <summary>16 ký tự ngẫu nhiên (CSPRNG) dạng <c>Xxxx-xxxx-0000-xxxx</c>: thỏa PasswordPolicy (có chữ và số).</summary>
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
}
