namespace ClassFund.Application.Features.Treasurers;

/// <summary>
/// Một lần phân công thủ quỹ. <c>isActive</c> = chưa thu hồi; thu hồi chỉ đánh dấu nên lịch sử luôn còn (BR-33).
/// Chỉ họ tên người thực hiện, không có email / tên đăng nhập (BR-23).
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

/// <summary>Phân công / thu hồi thủ quỹ – chỉ GVCN (<c>Treasurer.Assign</c>). FR-27, BR-31, BR-32, BR-33, P-15.</summary>
public interface ITreasurerService
{
    /// <summary>Đang hiệu lực trước, rồi lịch sử (mới nhất trước).</summary>
    Task<IReadOnlyList<TreasurerAssignmentResponse>> ListAsync(Guid fundId, CancellationToken cancellationToken);

    /// <summary>Phân công học sinh làm thủ quỹ (BR-31).</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">Học sinh không thuộc quỹ.</exception>
    /// <exception cref="Domain.Exceptions.BusinessRuleException">
    /// <c>treasurer.already_assigned</c>, <c>treasurer.no_account</c>, <c>treasurer.account_disabled</c>, <c>treasurer.account_not_activated</c>.
    /// </exception>
    Task<TreasurerAssignedResponse> AssignAsync(Guid fundId, Guid studentId, CancellationToken cancellationToken);

    /// <summary>
    /// Thu hồi (BR-32): không thu hồi được thủ quỹ cuối cùng. Chạy trong transaction, khóa dòng quỹ
    /// (<c>SELECT … FOR UPDATE</c>) để hai lệnh thu hồi đồng thời không cùng lọt.
    /// </summary>
    /// <exception cref="Common.Exceptions.NotFoundException">Phân công không thuộc quỹ.</exception>
    /// <exception cref="Common.Exceptions.ConflictException"><c>treasurer.already_revoked</c>.</exception>
    /// <exception cref="Domain.Exceptions.BusinessRuleException"><c>treasurer.last</c>.</exception>
    Task RevokeAsync(Guid fundId, Guid assignmentId, CancellationToken cancellationToken);
}
