namespace ClassFund.Application.Features.Treasurers;

/// <summary>Phân công / thu hồi thủ quỹ – chỉ GVCN của quỹ (<c>Treasurer.Assign</c>). FR-27, BR-31, BR-32, BR-33, P-15.</summary>
public interface ITreasurersService
{
    /// <summary>Đang hiệu lực trước, rồi lịch sử; trong mỗi nhóm mới phân công trước.</summary>
    Task<IReadOnlyList<TreasurerAssignmentResponse>> ListAsync(Guid fundId, CancellationToken cancellationToken);

    /// <summary>Phân công học sinh của quỹ có tài khoản đã kích hoạt, không bị khóa, chưa là thủ quỹ (BR-31).</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">Học sinh không thuộc quỹ.</exception>
    /// <exception cref="Domain.Exceptions.BusinessRuleException">Mã trong <see cref="TreasurersErrors"/>.</exception>
    Task<TreasurerAssignedResponse> AssignAsync(Guid fundId, Guid studentId, CancellationToken cancellationToken);

    /// <summary>
    /// Thu hồi (BR-32): không thu hồi được thủ quỹ cuối cùng. Chạy trong transaction và khóa dòng quỹ
    /// (<c>SELECT … FOR UPDATE</c>) để hai lệnh thu hồi đồng thời không cùng lọt (NFR-09).
    /// </summary>
    /// <exception cref="Common.Exceptions.NotFoundException">Phân công không thuộc quỹ.</exception>
    /// <exception cref="Common.Exceptions.ConflictException"><c>treasurer.already_revoked</c>.</exception>
    /// <exception cref="Domain.Exceptions.BusinessRuleException"><c>treasurer.last</c>.</exception>
    Task RevokeAsync(Guid fundId, Guid assignmentId, CancellationToken cancellationToken);
}
