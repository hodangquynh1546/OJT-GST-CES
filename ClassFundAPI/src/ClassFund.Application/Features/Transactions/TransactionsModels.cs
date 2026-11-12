using System.Text.Json;
using System.Text.Json.Serialization;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Transactions;

/// <summary>Lọc theo trạng thái hủy; mặc định chỉ khoản đang hiệu lực (backend-api.md – Transactions).</summary>
public enum TransactionStatusFilter
{
    Active = 1,
    Cancelled = 2,
    All = 3,
}

public sealed record TransactionFilter(
    TransactionType? Type,
    Guid? CategoryId,
    DateOnly? From,
    DateOnly? To,
    string? Search,
    TransactionStatusFilter Status,
    int Page,
    int PageSize);

/// <summary>Giá trị thủ quỹ nhập và sửa được; loại thu / chi không nằm ở đây vì không đổi được (BR-05).</summary>
public sealed record TransactionValues(
    long Amount,
    DateOnly OccurredOn,
    Guid CategoryId,
    string Description,
    string? Note,
    bool IsPublic);

/// <summary>
/// Một khoản thu / chi. Bản công khai (học sinh, phụ huynh) chỉ có <c>id</c>, <c>type</c>, <c>amount</c>, <c>occurredOn</c>,
/// <c>categoryId</c>, <c>categoryName</c>, <c>description</c>, <c>isCancelled</c>, <c>cancelledOn</c> (BR-14, NFR-08);
/// các trường nội bộ null nên không có trong JSON.
/// </summary>
public sealed record TransactionResponse(
    Guid Id,
    TransactionType Type,
    long Amount,
    DateOnly OccurredOn,
    Guid CategoryId,
    string? CategoryName,
    string Description,
    bool IsCancelled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? CancelledOn,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsPublic,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CancelReason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? CancelledAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? CreatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? UpdatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CreatedByName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UpdatedByName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CancelledByName)
{
    /// <summary>Chỉ có ở kết quả thêm: <c>true</c> khi trả lại khoản đã tạo bằng cùng khóa chống lưu trùng (BR-12).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Replayed { get; init; }

    /// <summary>Chỉ có ở kết quả ghi (thêm / sửa / hủy): số dư quỹ đang âm, khoản vẫn được lưu (BR-04).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsNegativeBalance { get; init; }
}

/// <summary>Một dòng lịch sử thay đổi (AuditLog của khoản): người, thời điểm, giá trị trước / sau (BR-11).</summary>
public sealed record TransactionHistoryItem(
    DateTimeOffset OccurredAt,
    string Action,
    string? ActorName,
    JsonElement? OldValues,
    JsonElement? NewValues);
