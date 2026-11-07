namespace ClassFund.Api.Authorization;

/// <summary>
/// Endpoint vẫn dùng được khi tài khoản còn mật khẩu tạm: <c>GET/PUT /auth/me</c>, <c>change-password</c>, <c>logout</c>.
/// Mọi endpoint khác trả 403 <c>password.change_required</c> (BR-17).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowTemporaryPasswordAttribute : Attribute;
