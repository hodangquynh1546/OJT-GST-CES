using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Members;

/// <summary>
/// Một thành viên của quỹ (hàng trong M-01). <see cref="Id"/> là mã <c>FundMember</c>, không phải mã người dùng.
/// <see cref="CanManageAccount"/>: được sửa họ tên / cấp lại mật khẩu tạm (AZ-07: tài khoản do thủ quỹ cấp, chỉ ở quỹ này
/// và học sinh CHƯA tự đặt mật khẩu – đã đổi mật khẩu thì tài khoản thuộc về học sinh).
/// </summary>
public sealed record MemberDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    MemberRole Role,
    MemberStatus Status,
    bool MustChangePassword,
    DateTimeOffset JoinedAt,
    DateTimeOffset? RemovedAt,
    bool CanManageAccount);

/// <summary>Một dòng khi thêm nhiều học sinh: email bắt buộc, họ tên tùy chọn (trống thì lấy phần trước @).</summary>
public sealed record BulkMemberInput(string Email, string? FullName);

/// <summary>Bộ lọc danh sách; <see cref="Status"/> mặc định <c>Active</c>.</summary>
public sealed record MemberFilter(string? Search, MemberRole? Role, MemberStatus? Status, int Page, int PageSize);

/// <summary>Kết quả xử lý một email khi thêm vào quỹ.</summary>
public enum AddMemberOutcome
{
    /// <summary>Email chưa có tài khoản: tạo tài khoản mới (Provisioned) kèm mật khẩu tạm.</summary>
    Created,

    /// <summary>Email đã có tài khoản: chỉ thêm vào quỹ.</summary>
    ExistingAdded,

    /// <summary>Đã từng là thành viên và bị ngừng: kích hoạt lại với vai trò học sinh.</summary>
    Reactivated,

    /// <summary>Đã là thành viên đang hoạt động.</summary>
    AlreadyExists,

    /// <summary>Email không hợp lệ.</summary>
    Invalid,
}

/// <summary>Kết quả thêm một email. <see cref="TemporaryPassword"/> chỉ có khi <see cref="AddMemberOutcome.Created"/> và chỉ trả MỘT lần.</summary>
public sealed record AddMemberResult(string Email, AddMemberOutcome Outcome, MemberDto? Member, string? TemporaryPassword, string? Message);

public sealed record BulkAddMembersResult(IReadOnlyList<AddMemberResult> Results);

public sealed record TemporaryPasswordResult(string TemporaryPassword);

/// <summary>Những gì bị ảnh hưởng nếu ngừng thành viên (hiện trong hộp thoại xác nhận, OQ-15).</summary>
public sealed record RemovalPreviewDto(int UnpaidCount, int PendingPaymentCount, long Balance, bool CanRemove, string? BlockedReason);

public sealed record RemoveMemberResult(int CancelledAssignments);
