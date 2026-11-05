using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using SchoolFund.Application.Common.Security;
using SchoolFund.Domain.Entities;

namespace SchoolFund.Infrastructure.Security;

/// <summary>
/// Băm mật khẩu bằng <see cref="PasswordHasher{TUser}"/> (PBKDF2, salt riêng từng tài khoản – D-06) kèm pepper:
/// trước khi băm, mật khẩu được biến đổi bằng HMAC-SHA256 với khóa bí mật <c>SECRET_KEY</c> nằm ngoài DB,
/// nên lộ riêng DB không đủ để dò mật khẩu. Hash một chiều – không giải ngược được.
/// </summary>
/// <remarks>
/// Hash tạo trước khi có pepper vẫn được chấp nhận và trả <see cref="PasswordCheck.SuccessRehashNeeded"/>
/// để lần đăng nhập đúng kế tiếp băm lại kèm pepper.
/// </remarks>
public sealed class AspNetPasswordHasher : Application.Common.Security.IPasswordHasher
{
    public const int MinimumPepperLength = 32;

    private readonly PasswordHasher<User> _hasher = new();
    private readonly byte[] _pepper;

    public AspNetPasswordHasher(string pepper)
    {
        if (string.IsNullOrWhiteSpace(pepper) || pepper.Length < MinimumPepperLength)
        {
            throw new ArgumentException($"Pepper cần tối thiểu {MinimumPepperLength} ký tự.", nameof(pepper));
        }

        _pepper = Encoding.UTF8.GetBytes(pepper);
    }

    public string Hash(string password) => _hasher.HashPassword(user: null!, Pepper(password));

    public PasswordCheck Verify(string passwordHash, string password)
    {
        var peppered = Map(_hasher.VerifyHashedPassword(user: null!, passwordHash, Pepper(password)));
        if (peppered != PasswordCheck.Failed)
        {
            return peppered;
        }

        // Hash cũ (trước khi có pepper): đúng mật khẩu → yêu cầu băm lại kèm pepper.
        return Map(_hasher.VerifyHashedPassword(user: null!, passwordHash, password)) == PasswordCheck.Failed
            ? PasswordCheck.Failed
            : PasswordCheck.SuccessRehashNeeded;
    }

    private string Pepper(string password) =>
        Convert.ToBase64String(HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(password)));

    private static PasswordCheck Map(PasswordVerificationResult result) => result switch
    {
        PasswordVerificationResult.Success => PasswordCheck.Success,
        PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
        _ => PasswordCheck.Failed,
    };
}
