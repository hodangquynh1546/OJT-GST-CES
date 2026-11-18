namespace ClassFund.Application.Features.Treasurers;

/// <summary>Mã lỗi và thông báo của module Thủ quỹ.</summary>
public static class TreasurersErrors
{
    public const string AlreadyTreasurerCode = "treasurer.already_treasurer";
    public const string AlreadyTreasurerMessage = "Người này đã là thủ quỹ của quỹ.";

    public const string NotTreasurerCode = "treasurer.not_treasurer";
    public const string NotTreasurerMessage = "Người này không phải thủ quỹ được phân quyền.";

    /// <summary>AZ-03: thủ quỹ chính không thể bị thu hồi.</summary>
    public const string CannotRevokeOwnerCode = "treasurer.cannot_revoke_owner";
    public const string CannotRevokeOwnerMessage = "Không thể thu hồi quyền của thủ quỹ chính.";

    public const string MemberNotActiveCode = "treasurer.member_not_active";
    public const string MemberNotActiveMessage = "Thành viên này đã bị ngừng. Hãy thêm lại vào quỹ trước khi phân quyền.";
}
