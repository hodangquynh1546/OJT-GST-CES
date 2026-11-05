namespace SchoolFund.Domain.Rules;

/// <summary>Chuẩn hóa ô "Email hoặc tên đăng nhập" (D-34): bỏ khoảng trắng hai đầu, chữ thường.</summary>
public static class LoginName
{
    public static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
