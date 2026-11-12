using FluentValidation;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Transactions;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.Transactions;

/// <summary>Trường để nullable: thiếu trường phải báo lỗi tiếng Việt, không nhận giá trị mặc định của kiểu.</summary>
public sealed record CreateTransactionRequest(
    TransactionType? Type,
    long? Amount,
    DateOnly? OccurredOn,
    Guid? CategoryId,
    string? Description,
    string? Note,
    bool? IsPublic,
    Guid? IdempotencyKey);

/// <summary>Đủ các trường; không có <c>type</c> nên thu không đổi được thành chi (BR-05). Bỏ <c>note</c> là xóa ghi chú.</summary>
public sealed record UpdateTransactionRequest(
    long? Amount,
    DateOnly? OccurredOn,
    Guid? CategoryId,
    string? Description,
    string? Note,
    bool? IsPublic);

public sealed record CancelTransactionRequest(string? Reason);

/// <summary>Query của <c>GET /transactions</c>; phân trang theo <see cref="PageRequest"/> (20 / trang, tối đa 100).</summary>
public sealed record TransactionListRequest : PageRequest
{
    public const int MaxSearchLength = 200;

    public TransactionType? Type { get; init; }

    public Guid? CategoryId { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    /// <summary>Tìm trong nội dung (bản đầy đủ: cả ghi chú), không phân biệt hoa thường.</summary>
    public string? Search { get; init; }

    public TransactionStatusFilter Status { get; init; } = TransactionStatusFilter.Active;
}

public sealed class CreateTransactionRequestValidator : AbstractValidator<CreateTransactionRequest>
{
    public CreateTransactionRequestValidator()
    {
        RuleFor(request => request.Type).NotNull().WithMessage("Chọn loại thu hoặc chi.");
        RuleFor(request => request.Amount).TransactionAmount();
        RuleFor(request => request.OccurredOn).NotNull().WithMessage("Chọn ngày giao dịch.");
        RuleFor(request => request.CategoryId).RequiredId("Chọn danh mục.");
        RuleFor(request => request.Description).TransactionDescription();
        RuleFor(request => request.Note).TransactionNote();
        RuleFor(request => request.IdempotencyKey).RequiredId("Thiếu khóa chống lưu trùng.");
    }
}

public sealed class UpdateTransactionRequestValidator : AbstractValidator<UpdateTransactionRequest>
{
    public UpdateTransactionRequestValidator()
    {
        RuleFor(request => request.Amount).TransactionAmount();
        RuleFor(request => request.OccurredOn).NotNull().WithMessage("Chọn ngày giao dịch.");
        RuleFor(request => request.CategoryId).RequiredId("Chọn danh mục.");
        RuleFor(request => request.Description).TransactionDescription();
        RuleFor(request => request.Note).TransactionNote();
        RuleFor(request => request.IsPublic).NotNull().WithMessage("Chọn công khai hoặc không công khai.");
    }
}

public sealed class CancelTransactionRequestValidator : AbstractValidator<CancelTransactionRequest>
{
    public const int MaxReasonLength = 500;

    public CancelTransactionRequestValidator() => RuleFor(request => request.Reason)
        .NotEmpty().WithMessage("Nhập lý do hủy.")
        .Must(reason => reason is null || reason.Trim().Length <= MaxReasonLength)
        .WithMessage($"Lý do hủy tối đa {MaxReasonLength} ký tự.");
}

public sealed class TransactionListRequestValidator : AbstractValidator<TransactionListRequest>
{
    public TransactionListRequestValidator()
    {
        this.ValidatePaging();
        RuleFor(request => request.Search)
            .MaximumLength(TransactionListRequest.MaxSearchLength)
            .WithMessage($"Từ khóa tối đa {TransactionListRequest.MaxSearchLength} ký tự.");
        RuleFor(request => request.To)
            .Must((request, to) => request.From is null || to is null || to >= request.From)
            .WithMessage("Ngày kết thúc phải từ ngày bắt đầu trở đi.");
    }
}

internal static class TransactionRules
{
    public const int MaxDescriptionLength = 200;
    public const int MaxNoteLength = 1000;

    /// <summary><c>NotEmpty</c> không chặn được Guid toàn số 0 trên kiểu nullable.</summary>
    public static void RequiredId<T>(this IRuleBuilder<T, Guid?> rule, string message) => rule
        .Must(id => id is { } value && value != Guid.Empty).WithMessage(message);

    public static void TransactionAmount<T>(this IRuleBuilder<T, long?> rule) => rule
        .NotNull().WithMessage("Nhập số tiền.")
        .GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0.");

    /// <summary>Độ dài tính sau khi cắt khoảng trắng, đúng với giá trị được lưu.</summary>
    public static void TransactionDescription<T>(this IRuleBuilder<T, string?> rule) => rule
        .NotEmpty().WithMessage("Nhập nội dung.")
        .Must(text => text is null || text.Trim().Length <= MaxDescriptionLength)
        .WithMessage($"Nội dung tối đa {MaxDescriptionLength} ký tự.");

    public static void TransactionNote<T>(this IRuleBuilder<T, string?> rule) => rule
        .Must(text => text is null || text.Trim().Length <= MaxNoteLength)
        .WithMessage($"Ghi chú tối đa {MaxNoteLength} ký tự.");
}
