namespace SchoolFund.Application.Common.Security;

public enum PasswordCheck
{
    Failed,
    Success,

    /// <summary>Đúng mật khẩu nhưng hash dùng tham số cũ, nên băm lại.</summary>
    SuccessRehashNeeded,
}

/// <summary>Băm mật khẩu (PBKDF2). Không bao giờ ghi mật khẩu hay hash ra log.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheck Verify(string passwordHash, string password);
}
