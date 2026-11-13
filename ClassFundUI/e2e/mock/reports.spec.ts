import { Page, expect, test } from '@playwright/test';

import { REPORT_MONTH, REPORT_YEAR, mockReports, monthlyEmpty } from '../support/mock-reports';
import { FUND, PARENT, STUDENT, TEACHER, TREASURER, mockProblem, signInAs, user } from '../support/mock-session';

/** M7b – S-03 Báo cáo tháng / 12 tháng ở 4 vai trò (API giả lập; Canvas board `Reports`). */

const MM = String(REPORT_MONTH).padStart(2, '0');

async function open(page: Page, me: ReturnType<typeof user>, publicView = false, extra: Parameters<typeof mockReports>[1] = {}) {
  await signInAs(page, me);
  await page.route('**/api/v1/funds', (route) =>
    route.fulfill({ json: [{ id: FUND, name: 'Quỹ lớp 5A', currentBalance: 4_020_000, createdAt: `${REPORT_YEAR - 1}-09-01T01:00:00Z` }] }),
  );
  const calls = await mockReports(page, { publicView, ...extra });
  await page.goto(`/funds/${FUND}/reports`);
  return calls;
}

const kpi = (page: Page, label: string) => page.locator('.kpi').filter({ hasText: label }).locator('b');

test('thủ quỹ: báo cáo tháng hiện tại – 5 số liệu, theo danh mục, danh sách 5 khoản', async ({ page }) => {
  const calls = await open(page, TREASURER);

  await expect(page.getByRole('heading', { level: 1, name: 'Báo cáo' })).toBeVisible();
  await expect(page.getByText('Đầy đủ, tự tính từ giao dịch đang hiệu lực')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Báo cáo tháng' })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('kendo-dropdownlist')).toContainText(`Tháng ${MM}/${REPORT_YEAR}`);
  expect(calls[0].get('year')).toBe(String(REPORT_YEAR));
  expect(calls[0].get('month')).toBe(String(REPORT_MONTH));

  await expect(kpi(page, 'Số dư đầu tháng')).toHaveText('2.450.000 đ');
  await expect(kpi(page, 'Tổng thu')).toHaveText('+3.500.000 đ');
  await expect(kpi(page, 'Tổng chi')).toHaveText('−1.930.000 đ');
  await expect(kpi(page, 'Chênh lệch')).toHaveText('+1.570.000 đ');
  await expect(kpi(page, 'Số dư cuối tháng')).toHaveText('4.020.000 đ');
  await expect(page.getByText(`Số dư đầu tháng ${MM}/${REPORT_YEAR} = số dư cuối tháng trước (2.450.000 đ)`)).toBeVisible();

  const expense = page.getByRole('region', { name: 'Chi theo danh mục' });
  await expect(expense.locator('tbody tr')).toHaveText([/Sinh hoạt, liên hoan\s*1\s*−1\.250\.000 đ/, /Khen thưởng\s*1\s*−500\.000 đ/, /Văn phòng phẩm\s*1\s*−180\.000 đ/]);
  await expect(expense.locator('tfoot')).toHaveText(/Tổng chi\s*−1\.930\.000 đ/);
  await expect(page.getByRole('region', { name: 'Thu theo danh mục' }).locator('tfoot')).toContainText('+3.500.000 đ');

  const list = page.getByRole('region', { name: /Giao dịch trong tháng \(5\)/ });
  await expect(list.locator('tbody tr')).toHaveCount(5);
  await expect(list.locator('tbody tr').first()).toContainText('Đóng quỹ tháng 11 – 32 phụ huynh');
  await expect(list.getByText('Không gồm khoản đã hủy')).toBeVisible();
  await expect(page.getByText('Khoản khác')).toHaveCount(0);

  await list.getByRole('link', { name: 'Mở trong Thu chi' }).click();
  await expect(page).toHaveURL(new RegExp(`/funds/${FUND}/transactions$`));
});

test('chọn tháng khác gọi lại API; tháng không có giao dịch hiện số 0 và thông báo trống', async ({ page }) => {
  const calls = await open(page, TREASURER, false, {
    monthly: (year, month) => monthlyEmpty(year, month),
  });
  await expect(page.getByText('Không có giao dịch trong tháng này.')).toBeVisible();
  await expect(kpi(page, 'Tổng thu')).toHaveText('0 đ');
  await expect(page.getByText('Không có khoản thu trong tháng.')).toBeVisible();
  await expect(page.getByText('Không có khoản chi trong tháng.')).toBeVisible();

  await page.locator('kendo-dropdownlist').click();
  const previous = REPORT_MONTH === 1 ? `Tháng 12/${REPORT_YEAR - 1}` : `Tháng ${String(REPORT_MONTH - 1).padStart(2, '0')}/${REPORT_YEAR}`;
  await page.getByRole('option', { name: previous }).click();
  await expect.poll(() => calls.length).toBe(2);
  expect(calls[1].get('month')).toBe(String(REPORT_MONTH === 1 ? 12 : REPORT_MONTH - 1));
});

test('GVCN: bản đầy đủ, chỉ xem', async ({ page }) => {
  await open(page, TEACHER);
  await expect(page.getByText('Đầy đủ · GVCN chỉ xem')).toBeVisible();
  await expect(page.getByRole('note')).toHaveCount(0);
  await expect(page.getByRole('region', { name: /Giao dịch trong tháng \(5\)/ })).toBeVisible();
});

for (const [role, me, subtitle] of [
  ['phụ huynh', PARENT, 'Bản công khai cho phụ huynh · mọi tháng'],
  ['học sinh', STUDENT, 'Bản công khai cho học sinh · mọi tháng'],
] as const) {
  test(`${role}: bản công khai – số tổng giữ nguyên, "Khoản khác" không số lượng, chỉ khoản công khai`, async ({ page }) => {
    await open(page, me, true);

    await expect(page.getByText(subtitle)).toBeVisible();
    await expect(page.getByRole('note')).toContainText('Các khoản không công khai được gộp vào “Khoản khác”');
    await expect(kpi(page, 'Tổng thu')).toHaveText('+3.500.000 đ');
    await expect(kpi(page, 'Tổng chi')).toHaveText('−1.930.000 đ');

    const income = page.getByRole('region', { name: 'Thu theo danh mục' });
    await expect(income.locator('tbody tr')).toHaveText([/Đóng quỹ\s*1\s*\+3\.200\.000 đ/, /Khoản khác \(không công khai chi tiết\)\s*—\s*\+300\.000 đ/]);
    const expense = page.getByRole('region', { name: 'Chi theo danh mục' });
    await expect(expense.locator('tbody tr').last()).toHaveText(/Khoản khác \(không công khai chi tiết\)\s*—\s*−500\.000 đ/);

    const list = page.getByRole('region', { name: /Khoản công khai trong tháng \(3\)/ });
    await expect(list.locator('tbody tr')).toHaveCount(3);
    await expect(list.getByText('Chỉ khoản công khai đang hiệu lực.')).toBeVisible();
    await expect(page.getByText('Khen thưởng')).toHaveCount(0);
    await expect(page.getByText('Quà tặng cô giáo')).toHaveCount(0);
  });
}

test('12 tháng: biểu đồ + bảng đủ 12 dòng, tháng tương lai để trống, chọn năm khác', async ({ page }) => {
  const calls = await open(page, PARENT, true);
  await page.getByRole('button', { name: '12 tháng' }).click();

  await expect(page.getByRole('heading', { name: `Thu chi theo tháng – ${REPORT_YEAR}` })).toBeVisible();
  await expect(page.getByRole('img', { name: /Biểu đồ cột thu chi 12 tháng/ })).toBeVisible();
  const rows = page.getByRole('region', { name: /Bảng 12 tháng/ }).locator('tbody tr');
  await expect(rows).toHaveCount(12);
  await expect(rows.nth(REPORT_MONTH - 1)).toHaveText(new RegExp(`T${REPORT_MONTH}/${REPORT_YEAR}\\s*2\\.450\\.000\\s*\\+3\\.500\\.000\\s*−1\\.930\\.000\\s*\\+1\\.570\\.000\\s*4\\.020\\.000`));
  await expect(page.locator('tr.future')).toHaveCount(12 - REPORT_MONTH);
  expect(calls.at(-1)!.get('year')).toBe(String(REPORT_YEAR));

  await page.locator('kendo-dropdownlist').click();
  await page.getByRole('option', { name: `Năm ${REPORT_YEAR - 1}` }).click();
  await expect(page.getByRole('heading', { name: `Thu chi theo tháng – ${REPORT_YEAR - 1}` })).toBeVisible();
  await expect(page.locator('tr.future')).toHaveCount(0);
  expect(calls.at(-1)!.get('year')).toBe(String(REPORT_YEAR - 1));
});

test('lỗi tải báo cáo: trạng thái lỗi có Thử lại', async ({ page }) => {
  await signInAs(page, TREASURER);
  await page.route('**/api/v1/funds', (route) => route.fulfill({ json: [] }));
  await mockProblem(page, `**/api/v1/funds/${FUND}/reports/monthly?**`, 500, 'server_error');
  await page.goto(`/funds/${FUND}/reports`);
  await expect(page.getByText('Không tải được dữ liệu')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Thử lại' })).toBeVisible();
});
