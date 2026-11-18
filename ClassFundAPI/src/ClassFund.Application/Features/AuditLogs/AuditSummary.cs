using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace ClassFund.Application.Features.AuditLogs;

/// <summary>
/// Dựng mô tả ngắn cho cột "Chi tiết" từ Before/After/Note của mọi module, không cần biết từng hành động:
/// lấy các trường dễ đọc (tên, email, số tiền…), trường đổi giá trị hiện "cũ → mới", rồi thêm ghi chú.
/// Module mới chỉ cần đặt tên trường trong After theo các khóa dưới đây là tự hiện.
/// </summary>
public static class AuditSummary
{
    public const int MaxLength = 200;

    /// <summary>Trường mô tả đối tượng, theo thứ tự ưu tiên.</summary>
    private static readonly string[] SubjectKeys =
        ["name", "fullName", "title", "email", "fileName", "eventName", "chargeTitle", "payerName"];

    /// <summary>Trường số tiền (VND).</summary>
    private static readonly string[] MoneyKeys = ["amount", "amountDue", "amountPerPerson", "total", "surplus", "balance"];

    /// <summary>Trường phụ hiển thị kèm nhãn tiếng Việt.</summary>
    private static readonly (string Key, string Label)[] ExtraKeys =
    [
        ("role", "vai trò"),
        ("kind", "loại"),
        ("type", "loại"),
        ("method", "phương thức"),
        ("response", "trả lời"),
        ("count", "số lượng"),
        ("reason", "lý do"),
    ];

    private static readonly Dictionary<string, string> ValueLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Owner"] = "Thủ quỹ chính",
        ["Treasurer"] = "Thủ quỹ",
        ["Student"] = "Học sinh",
        ["Income"] = "Thu",
        ["Expense"] = "Chi",
        ["Receipt"] = "Hóa đơn",
        ["PaymentProof"] = "Minh chứng thanh toán",
        ["Document"] = "Tài liệu",
        ["Avatar"] = "Ảnh đại diện",
        ["Cash"] = "Tiền mặt",
        ["BankTransfer"] = "Chuyển khoản",
        ["PersonalBalance"] = "Số dư cá nhân",
        ["Accepted"] = "Tham gia",
        ["Declined"] = "Không tham gia",
        ["Pending"] = "Chưa trả lời",
    };

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public static string Build(string? beforeJson, string? afterJson, string? note)
    {
        var before = Parse(beforeJson);
        var after = Parse(afterJson);
        var current = after ?? before;
        var parts = new List<string>();

        if (current is not null)
        {
            var subject = SubjectKeys.Select(key => Change(before, after, key)).FirstOrDefault(text => text is not null);
            if (subject is not null)
            {
                parts.Add(subject);
            }

            foreach (var key in MoneyKeys)
            {
                if (Get(current, key) is { ValueKind: JsonValueKind.Number } money && money.TryGetInt64(out var amount))
                {
                    parts.Add(FormatMoney(amount));
                    break;
                }
            }

            if (Change(before, after, "isActive") is not null && Get(after, "isActive") is { } active)
            {
                parts.Add(active.ValueKind == JsonValueKind.True ? "Dùng lại" : "Ngừng dùng");
            }

            foreach (var (key, label) in ExtraKeys)
            {
                if (Change(before, after, key) is { } text)
                {
                    parts.Add($"{label}: {text}");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            parts.Add(note.Trim());
        }

        var summary = string.Join(" · ", parts.Distinct());
        return summary.Length <= MaxLength ? summary : summary[..(MaxLength - 1)] + "…";
    }

    public const string LoopbackLabel = "nội bộ (localhost)";

    /// <summary>
    /// Che hai phần cuối địa chỉ IP (IPv4 "113.190.xx.xx", IPv6 giữ 2 nhóm đầu);
    /// địa chỉ máy nội bộ (127.0.0.1, ::1 – khi chạy thử trên máy) hiện <see cref="LoopbackLabel"/>.
    /// </summary>
    public static string? MaskIp(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return LoopbackLabel;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return $"{bytes[0]}.{bytes[1]}.xx.xx";
        }

        var groups = address.ToString().Split(':', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(':', groups.Take(2)) + ":xxxx:…";
    }

    public static string FormatMoney(long amount) => amount.ToString("#,##0", Vietnamese) + " đ";

    /// <summary>Giá trị của <paramref name="key"/>: đổi thì "cũ → mới", không đổi thì giá trị hiện tại.</summary>
    private static string? Change(JsonElement? before, JsonElement? after, string key)
    {
        var oldText = Text(Get(before, key));
        var newText = Text(Get(after, key));
        if (oldText is not null && newText is not null && oldText != newText)
        {
            return $"{oldText} → {newText}";
        }

        return newText ?? oldText;
    }

    private static string? Text(JsonElement? value) => value?.ValueKind switch
    {
        JsonValueKind.String => Label(value.Value.GetString()),
        JsonValueKind.Number => value.Value.GetRawText(),
        JsonValueKind.True => "có",
        JsonValueKind.False => "không",
        _ => null,
    };

    private static string? Label(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ValueLabels.GetValueOrDefault(value, value);

    private static JsonElement? Get(JsonElement? source, string key)
    {
        if (source is not { ValueKind: JsonValueKind.Object } element)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static JsonElement? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
