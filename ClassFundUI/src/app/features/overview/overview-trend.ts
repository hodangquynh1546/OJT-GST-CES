import { YearlyReport } from '../reports/reports.models';
import { TrendPoint } from './overview.models';

export interface YearMonth {
  year: number;
  month: number;
}

/** 3 tháng gần nhất tới tháng hiện tại (cũ → mới). `today` là `yyyy-MM-dd` theo giờ Việt Nam. */
export function lastThreeMonths(today: string, count = 3): YearMonth[] {
  const [year, month] = today.split('-').map(Number);
  const result: YearMonth[] = [];
  for (let back = count - 1; back >= 0; back--) {
    const index = year * 12 + (month - 1) - back;
    result.push({ year: Math.floor(index / 12), month: (index % 12) + 1 });
  }
  return result;
}

/** Các năm cần gọi `reports/yearly`: đầu năm cần thêm năm trước (screen-specifications S-01). */
export function yearsNeeded(months: YearMonth[]): number[] {
  return [...new Set(months.map((item) => item.year))];
}

/** Ghép dữ liệu yearly thành điểm biểu đồ; tháng không có trong báo cáo tính là 0. */
export function buildTrend(months: YearMonth[], reports: YearlyReport[]): TrendPoint[] {
  return months.map(({ year, month }) => {
    const row = reports.find((report) => report.year === year)?.months.find((item) => item.month === month);
    return {
      label: `${String(month).padStart(2, '0')}/${year}`,
      income: row?.totalIncome ?? 0,
      expense: row?.totalExpense ?? 0,
    };
  });
}
