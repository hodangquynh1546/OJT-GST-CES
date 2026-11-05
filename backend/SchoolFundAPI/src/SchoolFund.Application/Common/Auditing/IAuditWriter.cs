using SchoolFund.Domain.Enums;

namespace SchoolFund.Application.Common.Auditing;

/// <summary>
/// Ghi AuditLog cho request hiện tại. Bản ghi chỉ được thêm vào unit of work,
/// nên lưu cùng lần SaveChanges với thay đổi nghiệp vụ. Không truyền mật khẩu, token (NFR-10).
/// </summary>
public interface IAuditWriter
{
    void Record(
        AuditCategory category,
        string action,
        string entityType,
        string entityId,
        object? oldValues,
        object? newValues);
}
