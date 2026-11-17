using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ClassFund.Application.Common.Persistence;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Common.Auditing;

/// <summary>Một dòng nhật ký cần ghi. Before/After chỉ chứa giá trị được phép lưu: không mật khẩu, hash, token.</summary>
public sealed record AuditEntry(
    string Action,
    Guid? FundId,
    Guid? ActorUserId,
    FinancialScope Scope,
    Guid? EventId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    object? Before = null,
    object? After = null,
    bool IsSelfAction = false,
    string? Note = null);

/// <summary>
/// Thêm AuditLog vào CÙNG lần SaveChanges với thay đổi nghiệp vụ (cùng transaction): có thay đổi thì có nhật ký.
/// Mọi thao tác ghi của module phải gọi <see cref="Record"/>.
/// </summary>
public sealed class AuditTrail(IAppDb db, IRequestInfo request, TimeProvider clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    public void Record(AuditEntry entry) =>
        db.Add(new AuditLog(
            clock.GetUtcNow(),
            entry.FundId,
            entry.ActorUserId,
            entry.Scope,
            entry.EventId,
            entry.Action,
            entry.EntityType,
            entry.EntityId?.ToString())
        {
            BeforeJson = Serialize(entry.Before),
            AfterJson = Serialize(entry.After),
            IsSelfAction = entry.IsSelfAction,
            Note = entry.Note,
            CorrelationId = request.CorrelationId,
            IpAddress = request.IpAddress,
        });

    private static string? Serialize(object? values) => values is null ? null : JsonSerializer.Serialize(values, JsonOptions);
}
