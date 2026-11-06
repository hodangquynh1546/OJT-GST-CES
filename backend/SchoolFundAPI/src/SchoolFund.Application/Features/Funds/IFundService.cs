using System.Text.Json.Serialization;
using SchoolFund.Domain.Authorization;

namespace SchoolFund.Application.Features.Funds;

public enum TreasurerStatus
{
    /// <summary>Chưa có phân công thủ quỹ đang hiệu lực: chưa ai ghi thu chi được (BR-36).</summary>
    Unassigned,
    Assigned,
}

/// <summary>
/// Một thẻ "Quỹ của tôi" / "Quỹ của con" (FR-22). Trường chỉ dành cho một vai trò là <c>null</c>
/// với vai trò khác và không xuất hiện trong JSON: <c>studentCount</c> (GVCN), <c>linkedStudents</c> (phụ huynh).
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

public interface IFundService
{
    /// <summary>Mọi quỹ người dùng hiện tại có vai trò (GVCN, thủ quỹ, học sinh, phụ huynh), sắp theo tên.</summary>
    Task<IReadOnlyList<FundSummaryResponse>> ListMineAsync(CancellationToken cancellationToken);

    /// <summary>Giáo viên tạo quỹ: người tạo là GVCN, kèm 6 danh mục mặc định, chưa có thủ quỹ (FR-23).</summary>
    /// <exception cref="Common.Exceptions.ForbiddenAccessException">Không phải giáo viên.</exception>
    Task<FundCreatedResponse> CreateAsync(string name, CancellationToken cancellationToken);

    /// <summary>GVCN đổi tên quỹ (FR-11).</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">Không thuộc quỹ.</exception>
    /// <exception cref="Common.Exceptions.ForbiddenAccessException">Không phải GVCN của quỹ.</exception>
    Task RenameAsync(Guid fundId, string name, CancellationToken cancellationToken);
}
