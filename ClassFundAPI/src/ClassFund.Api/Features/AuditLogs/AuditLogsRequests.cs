using FluentValidation;
using ClassFund.Api.Common;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.AuditLogs;

/// <summary><c>GET /audit-logs?from=&amp;to=&amp;actorId=&amp;scope=&amp;eventId=&amp;action=&amp;selfOnly=&amp;page=&amp;pageSize=</c>.</summary>
public sealed record AuditLogListRequest : PageRequest
{
    /// <summary>Từ ngày (giờ VN), dạng <c>yyyy-MM-dd</c>.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Đến ngày (giờ VN), tính trọn ngày.</summary>
    public DateOnly? To { get; init; }

    public Guid? ActorId { get; init; }

    /// <summary><c>General</c> (quỹ chung) / <c>Event</c> (sự kiện) / <c>System</c>.</summary>
    public FinancialScope? Scope { get; init; }

    public Guid? EventId { get; init; }

    /// <summary>Mã hành động, ví dụ <c>fund.update</c>; kết thúc bằng "." để lấy cả nhóm (<c>member.</c>).</summary>
    public string? Action { get; init; }

    /// <summary>Chỉ thao tác thủ quỹ tự ghi nhận cho chính mình (FR-46).</summary>
    public bool SelfOnly { get; init; }
}

public sealed class AuditLogListRequestValidator : AbstractValidator<AuditLogListRequest>
{
    public const int ActionMaxLength = 100;

    public AuditLogListRequestValidator()
    {
        this.ValidatePaging();
        RuleFor(request => request.To)
            .GreaterThanOrEqualTo(request => request.From)
            .When(request => request.From is not null && request.To is not null)
            .WithMessage("Đến ngày phải sau hoặc bằng Từ ngày.");
        RuleFor(request => request.Action)
            .MaximumLength(ActionMaxLength)
            .WithMessage($"Mã hành động tối đa {ActionMaxLength} ký tự.");
        RuleFor(request => request.EventId)
            .Null()
            .When(request => request.Scope is FinancialScope.General or FinancialScope.System)
            .WithMessage("Chỉ lọc theo sự kiện khi phạm vi là Sự kiện.");
    }
}
