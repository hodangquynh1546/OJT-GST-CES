using ClassFund.Domain.Exceptions;

namespace ClassFund.Application.Features.Transactions;

/// <summary>
/// Mã lỗi của M6 Thu chi (docs/api/backend-api.md – Transactions). <c>transaction.cancelled</c>,
/// <c>transaction.cancel_reason_required</c> do Domain (<see cref="Domain.Entities.FundTransaction"/>) ném.
/// </summary>
public static class TransactionsErrors
{
    public const string FutureDateCode = "transaction.future_date";
    public const string CategoryInvalidCode = "transaction.category_invalid";

    public static BusinessRuleException FutureDate() =>
        new(FutureDateCode, "Ngày giao dịch không được ở tương lai.");

    public static BusinessRuleException CategoryInvalid() =>
        new(CategoryInvalidCode, "Danh mục không hợp lệ cho khoản này (khác loại, đã ẩn hoặc không thuộc quỹ).");
}
