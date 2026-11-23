using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Funds;

/// <summary>Dữ liệu tạo quỹ (FR-05). Đã qua validator ở Api.</summary>
public sealed record CreateFundInput(string Name, string SchoolYear);

/// <summary>Dữ liệu sửa thông tin quỹ (FR-06).</summary>
public sealed record UpdateFundInput(string Name, string SchoolYear);

/// <summary>
/// Một thẻ quỹ ở F-01 (FR-08).
/// <see cref="GeneralBalance"/> là số dư quỹ chung (không cộng tiền sự kiện, BR-02).
/// </summary>
public sealed record FundSummaryDto(
    Guid Id,
    string Name,
    string SchoolYear,
    FundStatus Status,
    MemberRole MyRole,
    int MemberCount,
    long GeneralBalance,
    long MyDueAmount = 0,
    int MyDueCount = 0,
    long MyBalance = 0);

/// <summary>Thông tin công khai của quỹ (FR-07) kèm vai trò của người xem.</summary>
public sealed record FundDto(
    Guid Id,
    string Name,
    string SchoolYear,
    FundStatus Status,
    DateTimeOffset CreatedAt,
    string OwnerName,
    int MemberCount,
    MemberRole MyRole);
