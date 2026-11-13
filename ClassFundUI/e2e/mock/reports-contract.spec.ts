import { expect, test } from '@playwright/test';

import { FUND } from '../support/mock-session';
import { REPORT_MONTH, REPORT_YEAR, mockReports, monthlyFull, monthlyPublic, yearlyReport } from '../support/mock-reports';

/** Kiểm tra bộ mock báo cáo (dùng chung M7a / M7b) khớp hợp đồng `reports/monthly`, `reports/yearly` đã khóa. */
const fetchJson = (page: import('@playwright/test').Page, path: string) =>
  page.evaluate(async (url) => (await fetch(url)).json(), `/api/v1/funds/${FUND}/reports/${path}`);

test.beforeEach(async ({ page }) => {
  await page.goto('/login');
});

test('mock yearly: luôn 12 tháng, đủ 6 trường, cuối tháng trước = đầu tháng sau, 3 tháng gần nhất có số liệu', async ({ page }) => {
  const calls = await mockReports(page);

  const report = await fetchJson(page, `yearly?year=${REPORT_YEAR}`);
  expect(Object.keys(report)).toEqual(['year', 'months']);
  expect(report.year).toBe(REPORT_YEAR);
  expect(report.months.map((month: { month: number }) => month.month)).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
  for (const [index, month] of report.months.entries()) {
    expect(Object.keys(month)).toEqual(['month', 'openingBalance', 'totalIncome', 'totalExpense', 'netChange', 'closingBalance']);
    expect(month.netChange).toBe(month.totalIncome - month.totalExpense);
    expect(month.closingBalance).toBe(month.openingBalance + month.netChange);
    if (index > 0) {
      expect(month.openingBalance).toBe(report.months[index - 1].closingBalance);
    }
  }
  expect(report.months[REPORT_MONTH - 1].totalIncome).toBe(3_500_000);
  expect(calls[0].get('year')).toBe(String(REPORT_YEAR));

  // Năm khác: vẫn 12 tháng.
  expect((await fetchJson(page, 'yearly?year=2024')).months).toHaveLength(12);
  expect(yearlyReport(2026, 11).months[10].closingBalance).toBe(4_020_000);
});

test('mock monthly: tổng các dòng = số tổng; bản công khai có "Khoản khác" null, không trường nội bộ', async ({ page }) => {
  for (const report of [monthlyFull(), monthlyPublic()]) {
    for (const type of ['Income', 'Expense'] as const) {
      const sum = report.byCategory.filter((line) => line.type === type).reduce((total, line) => total + line.amount, 0);
      expect(sum).toBe(type === 'Income' ? report.totalIncome : report.totalExpense);
    }
    expect(report.closingBalance).toBe(report.openingBalance + report.totalIncome - report.totalExpense);
  }

  await mockReports(page, { publicView: true });
  const shown = await fetchJson(page, `monthly?year=${REPORT_YEAR}&month=${REPORT_MONTH}`);
  const others = shown.byCategory.filter((line: { categoryId: string | null }) => line.categoryId === null);
  expect(others).toEqual([
    { type: 'Income', categoryId: null, categoryName: 'Khoản khác', count: null, amount: 300_000 },
    { type: 'Expense', categoryId: null, categoryName: 'Khoản khác', count: null, amount: 500_000 },
  ]);
  expect(shown.transactions).toHaveLength(3);
  for (const item of shown.transactions) {
    expect(Object.keys(item).sort()).toEqual(['amount', 'categoryId', 'categoryName', 'description', 'id', 'isCancelled', 'occurredOn', 'type']);
  }
});
