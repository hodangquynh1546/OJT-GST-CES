using FluentValidation;
using ClassFund.Application.Features.Payments;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.Payments;

public sealed record CollectPaymentRequest(PaymentMethod? Method, string? TransferCode);

public sealed record UncollectPaymentRequest(string Reason);

public sealed class CollectPaymentRequestValidator : AbstractValidator<CollectPaymentRequest>
{
    public CollectPaymentRequestValidator()
    {
        RuleFor(request => request.Method).NotNull().WithMessage("Vui lòng chọn hình thức thu.")
            .Must(method => method is PaymentMethod.Cash or PaymentMethod.BankTransfer).WithMessage(PaymentsErrors.MethodInvalidMessage);
        RuleFor(request => request.TransferCode)
            .Must(value => value is null || value.Trim().Length <= PaymentsErrors.TransferCodeMaxLength)
            .WithMessage("Mã giao dịch tối đa 100 ký tự.");
    }
}

public sealed class UncollectPaymentRequestValidator : AbstractValidator<UncollectPaymentRequest>
{
    public UncollectPaymentRequestValidator() =>
        RuleFor(request => request.Reason).NotEmpty().WithMessage(PaymentsErrors.ReasonRequiredMessage)
            .Must(value => value is null || value.Trim().Length <= PaymentsErrors.ReasonMaxLength).WithMessage("Lý do tối đa 500 ký tự.");
}
