using System.Text.Json.Serialization;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Categories;

/// <summary>
/// Bản công khai (học sinh, phụ huynh) không có <c>isHidden</c> và <c>transactionCount</c> trong JSON (NFR-08, BR-25).
/// <c>transactionCount</c> của bản đầy đủ tính cả khoản đã hủy.
/// </summary>
public sealed record CategoryResponse(
    Guid Id,
    TransactionType Type,
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsHidden,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TransactionCount);

public sealed record CategoryCreatedResponse(Guid Id);
