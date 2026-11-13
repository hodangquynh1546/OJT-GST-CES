namespace ClassFund.Application.Features.Reports;

/// <summary>
/// Báo cáo tháng và thống kê 12 tháng (M7b). FR-08, FR-09; BR-02, BR-09, BR-10, BR-15, P1; NFR-08.
/// Mọi số liệu tính động từ các khoản chưa hủy (không có bảng số dư). Người không phải thành viên quỹ → 404.
/// </summary>
public interface IReportsService
{
    /// <summary>
    /// Số dư đầu tháng = tổng có dấu của mọi khoản chưa hủy trước ngày 1 (bằng số dư cuối tháng trước, BR-10).
    /// Học sinh, phụ huynh: <c>byCategory</c> chỉ có danh mục của khoản công khai + một dòng "Khoản khác" mỗi loại có khoản không
    /// công khai (không <c>categoryId</c>, không <c>count</c>); <c>transactions</c> chỉ khoản công khai đang hiệu lực.
    /// </summary>
    Task<MonthlyReport> GetMonthlyAsync(Guid fundId, int year, int month, CancellationToken cancellationToken);

    /// <summary>Luôn đủ 12 tháng (tháng 1 → 12), cuối tháng trước = đầu tháng sau; giống nhau ở mọi vai trò.</summary>
    Task<YearlyReport> GetYearlyAsync(Guid fundId, int year, CancellationToken cancellationToken);
}
