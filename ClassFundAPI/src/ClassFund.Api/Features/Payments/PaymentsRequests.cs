using FluentValidation;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Payments;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.Payments;

/// <summary>Query hàng chờ: <c>?status=&amp;page=&amp;pageSize=</c> (mặc định <c>Submitted</c>).</summary>
public sealed record PaymentListRequest : PageRequest
{
    public PaymentStatus? Status { get; init; }
}

public sealed record SubmitPaymentRequest(PaymentMethod? Method, string? TransferCode, Guid? AttachmentId);

public sealed record RejectPaymentRequest(string Reason);

public sealed class PaymentListRequestValidator : AbstractValidator<PaymentListRequest>
{
    public PaymentListRequestValidator() => this.ValidatePaging();
}

public sealed class SubmitPaymentRequestValidator : AbstractValidator<SubmitPaymentRequest>
{
    public SubmitPaymentRequestValidator()
    {
        RuleFor(request => request.Method).NotNull().WithMessage("Vui lòng chọn hình thức đóng.")
            .IsInEnum().WithMessage("Hình thức đóng không hợp lệ.");
        RuleFor(request => request.TransferCode)
            .Must(value => value is null || value.Trim().Length <= PaymentsErrors.TransferCodeMaxLength)
            .WithMessage("Mã giao dịch tối đa 100 ký tự.");
    }
}

public sealed class RejectPaymentRequestValidator : AbstractValidator<RejectPaymentRequest>
{
    public RejectPaymentRequestValidator() =>
        RuleFor(request => request.Reason).NotEmpty().WithMessage(PaymentsErrors.ReasonRequiredMessage)
            .Must(value => value is null || value.Trim().Length <= PaymentsErrors.ReasonMaxLength).WithMessage("Lý do tối đa 500 ký tự.");
}
