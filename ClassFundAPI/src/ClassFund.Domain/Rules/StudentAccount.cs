using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Rules;

/// <summary>Quy tắc duy nhất suy ra trạng thái tài khoản học sinh; M3 (danh sách) và M4 (phân công thủ quỹ) cùng dùng.</summary>
public static class StudentAccount
{
    /// <summary>
    /// <paramref name="account"/> là <c>User</c> của học sinh, null nếu học sinh chưa có tài khoản.
    /// Thứ tự: chưa có → <see cref="StudentAccountStatus.None"/>; bị khóa → <see cref="StudentAccountStatus.Disabled"/>
    /// (kể cả khi còn mật khẩu tạm); còn mật khẩu tạm → <see cref="StudentAccountStatus.PendingActivation"/>; còn lại → <see cref="StudentAccountStatus.Active"/>.
    /// </summary>
    public static StudentAccountStatus StatusOf(User? account)
    {
        if (account is null)
        {
            return StudentAccountStatus.None;
        }

        if (account.Status == AccountStatus.Disabled)
        {
            return StudentAccountStatus.Disabled;
        }

        return account.MustChangePassword ? StudentAccountStatus.PendingActivation : StudentAccountStatus.Active;
    }
}
