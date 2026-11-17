using ClassFund.Domain.Common;
using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Entities;

/// <summary>Tài khoản đăng nhập bằng email + mật khẩu. Thủ quỹ đầu tiên tự đăng ký; học sinh do thủ quỹ cấp (Provisioned).</summary>
public sealed class User(string email, string fullName, string passwordHash, UserOrigin origin, bool mustChangePassword, DateTimeOffset createdAt) : Entity
{
    /// <summary>Email đã chuẩn hóa chữ thường; duy nhất toàn hệ thống.</summary>
    public string Email { get; set; } = email;

    public string FullName { get; set; } = fullName;

    public string PasswordHash { get; set; } = passwordHash;

    public UserOrigin Origin { get; init; } = origin;

    /// <summary>True với mật khẩu tạm: chỉ được đổi mật khẩu cho tới khi đổi xong.</summary>
    public bool MustChangePassword { get; set; } = mustChangePassword;

    public bool IsActive { get; set; } = true;

    /// <summary>Đổi khi đổi mật khẩu / khóa tài khoản để vô hiệu các phiên cũ.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Ảnh đại diện (Attachment Kind = Avatar).</summary>
    public Guid? AvatarAttachmentId { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = createdAt;
}
