using ClassFund.Application.Common.Exceptions;
using ClassFund.Domain.Exceptions;

namespace ClassFund.Application.Features.Treasurers;

/// <summary>Mã lỗi của M4 Phân công thủ quỹ (docs/api/backend-api.md – Treasurers). Giữ nguyên chính tả: frontend dựa vào mã này.</summary>
public static class TreasurersErrors
{
    public const string NoAccountCode = "treasurer.no_account";
    public const string AccountDisabledCode = "treasurer.account_disabled";
    public const string AccountNotActivatedCode = "treasurer.account_not_activated";
    public const string AlreadyAssignedCode = "treasurer.already_assigned";
    public const string LastCode = "treasurer.last";
    public const string AlreadyRevokedCode = "treasurer.already_revoked";

    public static BusinessRuleException NoAccount() =>
        new(NoAccountCode, "Học sinh chưa có tài khoản. Hãy cấp tài khoản ở trang Học sinh trước.");

    public static BusinessRuleException AccountDisabled() =>
        new(AccountDisabledCode, "Tài khoản của học sinh đang bị khóa.");

    public static BusinessRuleException AccountNotActivated() =>
        new(AccountNotActivatedCode, "Học sinh chưa kích hoạt tài khoản (chưa đổi mật khẩu tạm).");

    public static BusinessRuleException AlreadyAssigned() =>
        new(AlreadyAssignedCode, "Học sinh này đang là thủ quỹ.");

    public static BusinessRuleException Last() =>
        new(LastCode, "Không thể thu hồi thủ quỹ cuối cùng. Hãy phân công người mới trước rồi mới thu hồi người cũ.");

    public static ConflictException AlreadyRevoked() =>
        new(AlreadyRevokedCode, "Phân công này đã được thu hồi trước đó.");
}
