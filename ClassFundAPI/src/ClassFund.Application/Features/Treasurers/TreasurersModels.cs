using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Treasurers;

/// <summary>
/// Một người trong đội thủ quỹ (T-01). <see cref="MemberId"/> là mã FundMember. <see cref="GrantedAt"/> lấy từ nhật ký
/// phân quyền gần nhất; null với thủ quỹ chính (là người tạo quỹ) hoặc khi chưa có nhật ký.
/// </summary>
public sealed record TreasurerDto(
    Guid MemberId,
    Guid UserId,
    string FullName,
    string Email,
    MemberRole Role,
    DateTimeOffset JoinedAt,
    DateTimeOffset? GrantedAt);
