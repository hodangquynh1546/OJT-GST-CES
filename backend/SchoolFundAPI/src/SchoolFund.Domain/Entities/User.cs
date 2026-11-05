using SchoolFund.Domain.Common;
using SchoolFund.Domain.Enums;

namespace SchoolFund.Domain.Entities;

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
}
