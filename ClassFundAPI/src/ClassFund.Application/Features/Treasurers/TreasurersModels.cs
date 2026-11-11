namespace ClassFund.Application.Features.Treasurers;

/// <summary>
/// Một lần phân công thủ quỹ (backend-api.md – Treasurers). <c>isActive</c> = chưa thu hồi; thu hồi chỉ đánh dấu nên
/// lịch sử luôn còn (BR-33). Chỉ họ tên người thực hiện, không email / tên đăng nhập (BR-23).
/// </summary>
public sealed record TreasurerAssignmentResponse(
    Guid Id,
    Guid StudentId,
    string StudentName,
    bool IsActive,
    DateTimeOffset AssignedAt,
    string AssignedByName,
    DateTimeOffset? RevokedAt,
    string? RevokedByName);

public sealed record TreasurerAssignedResponse(Guid Id);
