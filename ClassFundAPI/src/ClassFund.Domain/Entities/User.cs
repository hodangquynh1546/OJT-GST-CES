using ClassFund.Domain.Common;
using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Entities;

/// <summary>Tài khoản đăng nhập của giáo viên, học sinh, phụ huynh.</summary>
public sealed class User : Entity
{
    public User(
        UserKind kind,
        string fullName,
        string passwordHash,
        string? email,
        string? username,
        bool mustChangePassword,
        DateTimeOffset createdAt)
    {
        Kind = kind;
        FullName = fullName;
        PasswordHash = passwordHash;
        Email = email;
        NormalizedEmail = email?.ToLowerInvariant();
        Username = username?.ToLowerInvariant();
        MustChangePassword = mustChangePassword;
        CreatedAt = createdAt;
        Status = AccountStatus.Active;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public UserKind Kind { get; private set; }

    public string? Email { get; private set; }

    public string? NormalizedEmail { get; private set; }

    public string? Username { get; private set; }

    public string FullName { get; private set; }

    public string PasswordHash { get; private set; }

    public AccountStatus Status { get; private set; }

    public bool MustChangePassword { get; private set; }

    public string SecurityStamp { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Rename(string fullName, DateTimeOffset at)
    {
        FullName = fullName;
        UpdatedAt = at;
    }

    /// <summary>Đổi mật khẩu: xóa cờ mật khẩu tạm và đổi SecurityStamp để mọi phiên cũ hết hiệu lực (BR-17, BR-18).</summary>
    public void ChangePassword(string newPasswordHash, DateTimeOffset at)
    {
        PasswordHash = newPasswordHash;
        MustChangePassword = false;
        SecurityStamp = Guid.NewGuid().ToString("N");
        UpdatedAt = at;
    }

    /// <summary>Băm lại mật khẩu đúng bằng tham số mới; không đổi SecurityStamp nên phiên giữ nguyên.</summary>
    public void UpgradePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    /// <summary>
    /// GVCN cấp lại mật khẩu tạm (BR-35): mật khẩu cũ hết hiệu lực, đổi SecurityStamp để mọi phiên bị đăng xuất,
    /// bắt buộc đổi mật khẩu ở lần đăng nhập sau.
    /// </summary>
    public void ResetTemporaryPassword(string temporaryPasswordHash, DateTimeOffset at)
    {
        PasswordHash = temporaryPasswordHash;
        MustChangePassword = true;
        SecurityStamp = Guid.NewGuid().ToString("N");
        UpdatedAt = at;
    }

    /// <summary>Khóa tài khoản: mọi phiên chấm dứt ngay (BR-18).</summary>
    public void Disable(DateTimeOffset at)
    {
        Status = AccountStatus.Disabled;
        SecurityStamp = Guid.NewGuid().ToString("N");
        UpdatedAt = at;
    }

    public void RecordSignIn(DateTimeOffset at) => LastLoginAt = at;
}
