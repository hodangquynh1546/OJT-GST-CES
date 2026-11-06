using FluentValidation;

namespace SchoolFund.Api.Features.Reports;

/// <summary>Các trường để nullable: thiếu tham số phải báo lỗi, không được nhận giá trị mặc định của kiểu.</summary>
public sealed class MonthlyReportRequest
{
    public int? Year { get; init; }

    public int? Month { get; init; }
}

public sealed class YearlyReportRequest
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
    public YearlyReportRequestValidator()
    {
        RuleFor(request => request.Year).ReportYear();
    }
}

internal static class ReportYearRule
{
    private const int MinYear = 1;
    private const int MaxYear = 9999;

    /// <summary>Giới hạn của <see cref="DateOnly"/>; năm chưa có giao dịch vẫn hợp lệ.</summary>
    public static void ReportYear<T>(this IRuleBuilder<T, int?> rule) => rule
        .NotNull().WithMessage("Chọn năm.")
        .InclusiveBetween(MinYear, MaxYear).WithMessage($"Năm từ {MinYear} đến {MaxYear}.");
}
