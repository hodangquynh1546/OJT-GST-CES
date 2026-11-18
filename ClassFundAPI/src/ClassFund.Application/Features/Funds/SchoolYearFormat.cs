using System.Globalization;

namespace ClassFund.Application.Features.Funds;

/// <summary>Năm học dạng "2026-2027": hai năm liên tiếp, cách nhau bằng dấu gạch ngang.</summary>
public static class SchoolYearFormat
{
    public const int MaxLength = 20;
    public const string Message = "Năm học có dạng 2026-2027 (hai năm liên tiếp).";

    /// <summary>Chuẩn hóa: bỏ khoảng trắng, đổi gạch dài (–, —) thành "-".</summary>
    public static string Normalize(string value) =>
        value.Trim().Replace('–', '-').Replace('—', '-').Replace(" ", string.Empty, StringComparison.Ordinal);

    public static bool IsValid(string value)
    {
        var parts = Normalize(value).Split('-');
        return parts.Length == 2
            && parts.All(part => part.Length == 4 && part.All(char.IsAsciiDigit))
            && int.Parse(parts[1], CultureInfo.InvariantCulture) == int.Parse(parts[0], CultureInfo.InvariantCulture) + 1
            && int.Parse(parts[0], CultureInfo.InvariantCulture) is >= 2000 and <= 2100;
    }
}
