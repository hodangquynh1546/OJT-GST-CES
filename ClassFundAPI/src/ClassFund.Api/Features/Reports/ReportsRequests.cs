using FluentValidation;

namespace ClassFund.Api.Features.Reports;

/// <summary>Query của <c>GET /reports/monthly</c>; trường để nullable: thiếu tham số phải báo lỗi, không nhận giá trị mặc định của kiểu.</summary>
public sealed record MonthlyReportRequest
{
    public int? Year { get; init; }

    public int? Month { get; init; }
}

/// <summary>Query của <c>GET /reports/yearly</c>.</summary>
public sealed record YearlyReportRequest
{
    public int? Year { get; init; }
}

public sealed class MonthlyReportRequestValidator : AbstractValidator<MonthlyReportRequest>
{
    public MonthlyReportRequestValidator()
    {
        RuleFor(request => request.Year).ReportYear();
        RuleFor(request => request.Month)
            .NotNull().WithMessage("Chọn tháng.")
            .InclusiveBetween(1, 12).WithMessage("Tháng từ 1 đến 12.");
    }
}

public sealed class YearlyReportRequestValidator : AbstractValidator<YearlyReportRequest>
{
    public YearlyReportRequestValidator() => RuleFor(request => request.Year).ReportYear();
}

internal static class ReportRules
{
    public const int MinYear = 1;
    public const int MaxYear = 9999;

    /// <summary>Giới hạn của <see cref="DateOnly"/>; năm chưa có giao dịch vẫn hợp lệ.</summary>
    public static void ReportYear<T>(this IRuleBuilder<T, int?> rule) => rule
        .NotNull().WithMessage("Chọn năm.")
        .InclusiveBetween(MinYear, MaxYear).WithMessage($"Năm từ {MinYear} đến {MaxYear}.");
}
