namespace ClassFund.Application.Common.Exceptions;

/// <summary>Không tồn tại hoặc không thuộc phạm vi người dùng; API trả 404 <c>not_found</c>.</summary>
public sealed class NotFoundException(string message = "Không tìm thấy dữ liệu.") : Exception(message);
