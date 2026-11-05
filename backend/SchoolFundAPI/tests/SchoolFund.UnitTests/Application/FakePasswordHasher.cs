using SchoolFund.Application.Common.Security;

namespace SchoolFund.UnitTests.Application;

/// <summary>Hash giả xác định được: "hash:" + mật khẩu; tiền tố "legacy:" mô phỏng hash cần băm lại.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hash:" + password;

    public PasswordCheck Verify(string passwordHash, string password) =>
        passwordHash == "hash:" + password ? PasswordCheck.Success
        : passwordHash == "legacy:" + password ? PasswordCheck.SuccessRehashNeeded
        : PasswordCheck.Failed;
}
