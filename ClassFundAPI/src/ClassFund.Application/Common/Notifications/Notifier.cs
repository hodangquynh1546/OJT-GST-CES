using ClassFund.Application.Common.Persistence;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Common.Notifications;

/// <summary>Nội dung một thông báo gửi cho nhiều người nhận.</summary>
public sealed record NotificationMessage(
    Guid FundId,
    FinancialScope Scope,
    Guid? EventId,
    NotificationType Type,
    string Title,
    string Body,
    string? ReferenceType = null,
    Guid? ReferenceId = null);

/// <summary>
/// Tạo thông báo nội bộ trong cùng SaveChanges với nghiệp vụ. Tiêu đề luôn có tiền tố phạm vi
/// ("[Quỹ chung]" / "[Sự kiện: tên]") để không nhầm giữa quỹ chung và sự kiện.
/// Người nhận do module quyết định (khoản thu sự kiện: CHỈ người tham gia thực tế).
/// </summary>
public sealed class Notifier(IAppDb db, TimeProvider clock)
{
    public const string GeneralPrefix = "[Quỹ chung]";

    public static string ScopePrefix(FinancialScope scope, string? eventName) => scope switch
    {
        FinancialScope.General => GeneralPrefix,
        FinancialScope.Event => $"[Sự kiện: {eventName}]",
        _ => string.Empty,
    };

    public void Send(IEnumerable<Guid> recipientUserIds, NotificationMessage message)
    {
        var now = clock.GetUtcNow();
        foreach (var userId in recipientUserIds.Distinct())
        {
            db.Add(new Notification(userId, message.FundId, message.Scope, message.EventId, message.Type, message.Title, message.Body, now)
            {
                ReferenceType = message.ReferenceType,
                ReferenceId = message.ReferenceId,
            });
        }
    }
}
