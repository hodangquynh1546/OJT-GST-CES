using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ClassFund.Application.Common.Persistence;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Common.Auditing;

/// <summary>
/// Thêm một dòng AuditLog vào cùng lần SaveChanges với thay đổi nghiệp vụ.
/// Người gọi chỉ truyền giá trị được phép lưu: tuyệt đối không truyền mật khẩu, hash, cookie, token (NFR-10).
/// </summary>
public sealed class AuditTrail(IAppDb db, IRequestInfo request, TimeProvider clock)
{
    public void Record(
        string action,
        AuditCategory category,
        Guid? actorUserId,
        string? entityType = null,
        Guid? entityId = null,
        object? oldValues = null,
        object? newValues = null)
    {
        db.Add(new AuditLog(
            clock.GetUtcNow(),
            actorUserId,
            category,
            action,
            entityType,
            entityId?.ToString(),
            Serialize(oldValues),
            Serialize(newValues),
            request.CorrelationId,
            request.IpAddress));
    }

    /// <summary>Giữ nguyên chữ tiếng Việt thay vì escape <c>\uXXXX</c>.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    private static string? Serialize(object? values) => values is null ? null : JsonSerializer.Serialize(values, JsonOptions);
}
