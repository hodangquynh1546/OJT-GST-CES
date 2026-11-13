using ClassFund.Application.Features.Transactions;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Reports;

/// <summary>
/// Một dòng thống kê theo danh mục. Dòng "Khoản khác" của bản công khai có <c>categoryId</c> và <c>count</c> null;
/// hai trường này vẫn có trong JSON (P1, BR-15).
/// </summary>
public sealed record CategoryReportLine(
    TransactionType Type,
    Guid? CategoryId,
    string CategoryName,
    int? Count,
    long Amount);

/// <summary>
/// Báo cáo một tháng (FR-08). Số tổng giống nhau ở mọi vai trò (BR-15); <c>byCategory</c> và <c>transactions</c> theo phạm vi người xem.
/// <c>transactions</c> chỉ gồm khoản đang hiệu lực (FR-05, TD-109).
/// </summary>
public sealed record MonthlyReport(
    int Year,
    int Month,
    long OpeningBalance,
    long TotalIncome,
    long TotalExpense,
    long NetChange,
    long ClosingBalance,
    IReadOnlyList<CategoryReportLine> ByCategory,
    IReadOnlyList<TransactionResponse> Transactions);

/// <summary>Số tổng của một tháng trong báo cáo năm.</summary>
public sealed record MonthFigures(
    int Month,
    long OpeningBalance,
    long TotalIncome,
    long TotalExpense,
    long NetChange,
    long ClosingBalance);

/// <summary>Luôn đủ 12 tháng, chỉ có số tổng: không có danh sách hay số lượng khoản (FR-09, LEAK-12).</summary>
public sealed record YearlyReport(int Year, IReadOnlyList<MonthFigures> Months);
