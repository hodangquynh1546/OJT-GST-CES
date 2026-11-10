using System.Text.Json.Serialization;
using ClassFund.Domain.Authorization;

namespace ClassFund.Application.Features.Funds;

/// <summary>Trạng thái phân công thủ quỹ của một quỹ (BR-36; api-conventions.md mục 7).</summary>
public enum TreasurerStatus
{
    /// <summary>Chưa có phân công thủ quỹ đang hiệu lực: chưa ai ghi thu chi được.</summary>
    Unassigned,
    Assigned,
}

/// <summary>
/// Một thẻ "Quỹ của tôi" / "Quỹ của con" (FR-22, backend-api.md mục Funds). Trường chỉ dành cho một vai trò là <c>null</c>
/// với vai trò khác và không có trong JSON: <c>studentCount</c> (GVCN), <c>linkedStudents</c> (phụ huynh).
/// </summary>
public sealed record FundSummaryResponse(
    Guid Id,
    string Name,
    FundRole Role,
    IReadOnlyList<string> Permissions,
    long CurrentBalance,
    DateTimeOffset CreatedAt,
    TreasurerStatus TreasurerStatus,
    IReadOnlyList<string> TreasurerNames,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? StudentCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? LinkedStudents);

public sealed record FundCreatedResponse(Guid Id);
