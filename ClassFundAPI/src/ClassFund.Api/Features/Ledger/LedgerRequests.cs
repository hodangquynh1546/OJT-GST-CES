using FluentValidation;
using ClassFund.Api.Common;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;

namespace ClassFund.Api.Features.Ledger;

/// <summary><c>GET /ledger-entries?eventId=&amp;type=&amp;categoryId=&amp;from=&amp;to=&amp;page=&amp;pageSize=</c>. Không có eventId = quỹ chung.</summary>
public sealed record LedgerListRequest : PageRequest
{
    /// <summary>Sự kiện cần xem; bỏ trống = sổ quỹ chung (không gồm giao dịch sự kiện, BR-01).</summary>
    public Guid? EventId { get; init; }

    /// <summary><c>Income</c> / <c>Expense</c>.</summary>
    public LedgerType? Type { get; init; }

    public Guid? CategoryId { get; init; }

    /// <summary>Từ ngày giao dịch, <c>yyyy-MM-dd</c>.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Đến ngày giao dịch (tính trọn ngày).</summary>
    public DateOnly? To { get; init; }
}

public sealed class LedgerListRequestValidator : AbstractValidator<LedgerListRequest>
{
    public LedgerListRequestValidator()
    {
        this.ValidatePaging();
        RuleFor(request => request.To)
            .GreaterThanOrEqualTo(request => request.From)
            .When(request => request.From is not null && request.To is not null)
            .WithMessage("Đến ngày phải sau hoặc bằng Từ ngày.");
    }
}

/// <summary>Ghi khoản chi (chỉ chi – thu đi qua khoản thu, BR-39). <c>eventId</c> null = quỹ chung.</summary>
public sealed record CreateExpenseRequest(
    Guid? EventId,
    Guid? CategoryId,
    string Title,
    string? Purpose,
    long? Amount,
    DateOnly? TransactionDate,
    string? Note,
    IReadOnlyList<Guid>? AttachmentIds);

public sealed class CreateExpenseRequestValidator : AbstractValidator<CreateExpenseRequest>
{
    public const int TitleMaxLength = 150;
    public const int TextMaxLength = 1000;
    public const int MaxAttachments = 10;

    public CreateExpenseRequestValidator()
    {
        RuleFor(request => request.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title)).WithMessage("Vui lòng nhập tên khoản chi.")
            .Must(title => title is null || title.Trim().Length <= TitleMaxLength).WithMessage($"Tên khoản chi tối đa {TitleMaxLength} ký tự.");
        RuleFor(request => request.CategoryId).NotEmpty().WithMessage("Vui lòng chọn danh mục.");
        RuleFor(request => request.Amount)
            .NotNull().WithMessage("Vui lòng nhập số tiền.")
            .InclusiveBetween(1, MoneyRule.MaxAmount).When(request => request.Amount is not null).WithMessage("Số tiền phải lớn hơn 0.");
        RuleFor(request => request.TransactionDate).NotNull().WithMessage("Vui lòng chọn ngày chi.");
        RuleFor(request => request.Purpose).MaximumLength(TextMaxLength).WithMessage($"Mục đích tối đa {TextMaxLength} ký tự.");
        RuleFor(request => request.Note).MaximumLength(TextMaxLength).WithMessage($"Ghi chú tối đa {TextMaxLength} ký tự.");
        RuleFor(request => request.EventId).NotEqual(Guid.Empty).WithMessage("Sự kiện không hợp lệ.");
        RuleFor(request => request.AttachmentIds)
            .Must(ids => ids is null || ids.Count <= MaxAttachments).WithMessage($"Tối đa {MaxAttachments} chứng từ.");
    }
}

public sealed record ReverseLedgerEntryRequest(string Reason);

public sealed class ReverseLedgerEntryRequestValidator : AbstractValidator<ReverseLedgerEntryRequest>
{
    public const int ReasonMaxLength = 500;

    public ReverseLedgerEntryRequestValidator() =>
        RuleFor(request => request.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithMessage("Vui lòng nhập lý do đảo.")
            .Must(reason => reason is null || reason.Trim().Length <= ReasonMaxLength).WithMessage($"Lý do tối đa {ReasonMaxLength} ký tự.");
}
