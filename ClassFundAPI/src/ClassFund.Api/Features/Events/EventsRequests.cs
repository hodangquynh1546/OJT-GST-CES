using FluentValidation;
using ClassFund.Application.Features.Events;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.Events;

/// <summary>Tạo / sửa sự kiện (E-02). Ngày diễn ra và hạn đóng danh sách bắt buộc.</summary>
public sealed record SaveEventRequest(string Name, string? Description, DateOnly? EventDate, DateOnly? ListDeadline);

public sealed class SaveEventRequestValidator : AbstractValidator<SaveEventRequest>
{
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 1000;

    public SaveEventRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Vui lòng nhập tên sự kiện.")
            .Must(name => name is null || name.Trim().Length <= NameMaxLength).WithMessage($"Tên sự kiện tối đa {NameMaxLength} ký tự.");
        RuleFor(request => request.Description).MaximumLength(DescriptionMaxLength).WithMessage($"Mô tả tối đa {DescriptionMaxLength} ký tự.");
        RuleFor(request => request.EventDate).NotNull().WithMessage("Vui lòng chọn ngày diễn ra.");
        RuleFor(request => request.ListDeadline).NotNull().WithMessage("Vui lòng chọn hạn đóng danh sách.");
    }
}

/// <summary><c>POST /events/{id}/close-list</c> – <c>confirmEarly</c> bắt buộc khi đóng sớm hơn hạn đóng danh sách.</summary>
public sealed record CloseListRequest(bool ConfirmEarly = false);

public sealed record CancelEventRequest(string Reason);

public sealed class CancelEventRequestValidator : AbstractValidator<CancelEventRequest>
{
    public const int ReasonMaxLength = 500;

    public CancelEventRequestValidator() =>
        RuleFor(request => request.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithMessage("Vui lòng nhập lý do hủy.")
            .Must(reason => reason is null || reason.Trim().Length <= ReasonMaxLength).WithMessage($"Lý do tối đa {ReasonMaxLength} ký tự.");
}

/// <summary><c>PUT /events/{id}/me/response</c> – <c>Accepted</c> / <c>Declined</c>.</summary>
public sealed record RespondEventRequest(ParticipationResponse? Response);

public sealed class RespondEventRequestValidator : AbstractValidator<RespondEventRequest>
{
    public RespondEventRequestValidator() =>
        RuleFor(request => request.Response)
            .Must(response => response is ParticipationResponse.Accepted or ParticipationResponse.Declined)
            .WithMessage("Chỉ chọn Tham gia hoặc Không tham gia.");
}

/// <summary><c>GET /events/{id}/participants?paymentStatus=</c> – <c>Paid</c> / <c>Pending</c> / <c>Unpaid</c> / <c>NoCharge</c>.</summary>
public sealed record EventParticipantsQuery
{
    public ParticipantPaymentStatus? PaymentStatus { get; init; }
}
