namespace ClassFund.Application.Common.Security;

/// <summary>Dữ liệu ghi vào cookie phiên khi đăng nhập: chỉ mã người dùng và SecurityStamp, không có quyền / vai trò.</summary>
public sealed record SessionTicket(Guid UserId, string SecurityStamp);
