namespace ClassFund.Application.Features.Members;

/// <summary>Mã lỗi và thông báo của module Học sinh.</summary>
public static class MembersErrors
{
    public const string AlreadyExistsCode = "member.already_exists";
    public const string AlreadyExistsMessage = "Học sinh này đã ở trong quỹ.";

    public const string InvalidEmailCode = "member.invalid_email";
    public const string InvalidEmailMessage = "Email không hợp lệ.";

    public const string InvalidNameCode = "member.invalid_name";
    public const string InvalidNameMessage = "Họ tên không được để trống và tối đa 100 ký tự.";

    public const string TooManyEmailsCode = "member.too_many_emails";
    public const int MaxBulkEmails = 50;
    public const string TooManyEmailsMessage = "Mỗi lần chỉ thêm tối đa 50 email.";

    public const string NoEmailsCode = "member.no_emails";
    public const string NoEmailsMessage = "Vui lòng nhập ít nhất một email.";

    /// <summary>AZ-02: thủ quỹ không tác động lên thủ quỹ chính / thủ quỹ khác.</summary>
    public const string ProtectedRoleMessage = "Chỉ thủ quỹ chính được thao tác trên thủ quỹ.";

    /// <summary>AZ-07: chỉ quản lý tài khoản do thủ quỹ cấp và chưa dùng ở quỹ khác.</summary>
    public const string AccountNotManagedCode = "member.account_not_managed";
    public const string AccountNotManagedMessage =
        "Tài khoản này do chính người dùng quản lý (tự đăng ký hoặc đang ở quỹ khác). Họ tự đổi họ tên và mật khẩu ở trang Tài khoản.";

    public const string CannotActOnSelfCode = "member.cannot_act_on_self";
    public const string CannotActOnSelfMessage = "Bạn không thể thao tác này trên chính mình ở đây. Hãy dùng trang Tài khoản.";

    public const string CannotRemoveOwnerCode = "member.cannot_remove_owner";
    public const string CannotRemoveOwnerMessage = "Không thể ngừng thủ quỹ chính của quỹ.";

    public const string AlreadyRemovedCode = "member.already_removed";
    public const string AlreadyRemovedMessage = "Thành viên này đã bị ngừng.";

    public const string HasBalanceCode = "member.has_balance";
    public const string HasBalanceMessage = "Thành viên còn số dư cá nhân. Số dư phải về 0 (rút tiền) trước khi ngừng.";

    public const string PendingPaymentsCode = "member.pending_payments";
    public const string PendingPaymentsMessage = "Thành viên còn thanh toán đang chờ xác nhận. Hãy xác nhận hoặc từ chối trước khi ngừng.";

    public const int FullNameMaxLength = 100;
}
