import { buildTrend, lastThreeMonths, yearsNeeded } from './overview-trend';
import { MonthFigures, YearlyReport } from '../reports/reports.models';

const month = (n: number, income: number, expense: number): MonthFigures => ({
  month: n,
  openingBalance: 0,
  totalIncome: income,
  totalExpense: expense,
  netChange: income - expense,
  closingBalance: income - expense,
});

describe('overview-trend', () => {
  it('lastThreeMonths returns the current month and the two before it, oldest first', () => {
    expect(lastThreeMonths('2026-11-12')).toEqual([
      { year: 2026, month: 9 },
      { year: 2026, month: 10 },
      { year: 2026, month: 11 },
    ]);
  });

  it('lastThreeMonths crosses into the previous year in January and February', () => {
    expect(lastThreeMonths('2026-01-05')).toEqual([
      { year: 2025, month: 11 },
      { year: 2025, month: 12 },
      { year: 2026, month: 1 },
    ]);
    expect(lastThreeMonths('2026-02-28').map((item) => `${item.month}/${item.year}`)).toEqual(['12/2025', '1/2026', '2/2026']);
  });

  it('yearsNeeded asks for one year mid-year and two years at the start of the year', () => {
    expect(yearsNeeded(lastThreeMonths('2026-11-12'))).toEqual([2026]);
    expect(yearsNeeded(lastThreeMonths('2026-01-05'))).toEqual([2025, 2026]);
  });

  it('buildTrend picks income / expense per month across years and fills missing months with 0', () => {
    const reports: YearlyReport[] = [
      { year: 2025, months: [month(11, 100, 40), month(12, 200, 50)] },
      { year: 2026, months: [month(1, 300, 70)] },
    ];

    expect(buildTrend(lastThreeMonths('2026-01-05'), reports)).toEqual([
      { label: '11/2025', income: 100, expense: 40 },
      { label: '12/2025', income: 200, expense: 50 },
      { label: '01/2026', income: 300, expense: 70 },
    ]);
    expect(buildTrend(lastThreeMonths('2026-03-01'), reports).map((point) => point.income)).toEqual([300, 0, 0]);
  });
});
