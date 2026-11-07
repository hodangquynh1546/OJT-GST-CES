using FluentValidation;
using ClassFund.Application.Features.Transactions;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.Transactions;

/// <summary>Các trường để nullable: thiếu trường phải báo lỗi, không được nhận giá trị mặc định của kiểu.</summary>
public sealed record CreateTransactionRequest(
    TransactionType? Type,
    long? Amount,
    DateOnly? OccurredOn,
    Guid? CategoryId,
    string? Description,
    string? Note,
    bool? IsPublic,
    Guid? IdempotencyKey);

/// <summary>Không có <c>type</c>: khoản thu không đổi được thành khoản chi và ngược lại (BR-05).</summary>
public sealed record UpdateTransactionRequest(
    long? Amount,
    DateOnly? OccurredOn,
    Guid? CategoryId,
    string? Description,
    string? Note,
    bool? IsPublic);

public sealed record CancelTransactionRequest(string? Reason);

public sealed class TransactionListRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 200;

    public TransactionType? Type { get; init; }

    public Guid? CategoryId { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public string? Search { get; init; }

    public TransactionStatusFilter Status { get; init; } = TransactionStatusFilter.Active;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;
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
    private const int MaxReasonLength = 500;

    public CancelTransactionRequestValidator() => RuleFor(request => request.Reason)
        .NotEmpty().WithMessage("Nhập lý do hủy.")
        .Must(reason => reason is null || reason.Trim().Length <= MaxReasonLength)
        .WithMessage($"Lý do hủy tối đa {MaxReasonLength} ký tự.");
}

public sealed class TransactionListRequestValidator : AbstractValidator<TransactionListRequest>
{
    public TransactionListRequestValidator()
    {
        RuleFor(request => request.Page).GreaterThanOrEqualTo(1).WithMessage("Trang bắt đầu từ 1.");
        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, TransactionListRequest.MaxPageSize)
            .WithMessage($"Số dòng mỗi trang từ 1 đến {TransactionListRequest.MaxPageSize}.");
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
    private const int MaxDescriptionLength = 200;
    private const int MaxNoteLength = 1000;

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
