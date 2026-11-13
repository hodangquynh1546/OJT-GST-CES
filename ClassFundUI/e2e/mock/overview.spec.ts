import { Page, expect, test } from '@playwright/test';

import { mockCategories } from '../support/mock-categories';
import {
  MockOverview,
  OVERVIEW_EMPTY,
  OVERVIEW_NEGATIVE_NO_TREASURER,
  OVERVIEW_PUBLIC,
  OVERVIEW_TEACHER,
  OVERVIEW_TREASURER,
  mockOverview,
} from '../support/mock-overview';
import { mockReports, yearlyReport } from '../support/mock-reports';
import { FUND, PARENT, STUDENT, TEACHER, TREASURER, signInAs } from '../support/mock-session';

/** M7a – S-01 Tổng quan (API giả lập; Canvas board `Overview`; FR-08, BR-02, BR-04, BR-15, BR-36). */

const vnd = (value: number): string => `${value < 0 ? '−' : ''}${new Intl.NumberFormat('vi-VN').format(Math.abs(value))} đ`;
/** Mở S-01; trả về danh sách query đã gọi `reports/yearly` (M7b, `mockReports()` dùng chung) – mảng được điền dần. */
const open = async (page: Page, me: object, overview: MockOverview, yearly = (year: number) => yearlyReport(year)) => {
  await signInAs(page, me);
  await mockOverview(page, overview);
  const calls = await mockReports(page, { yearly });
  await page.goto(`/funds/${FUND}`);
  return calls;
};
const years = (calls: URLSearchParams[]) => calls.map((query) => Number(query.get('year'))).sort();
const chartTable = (page: Page) => page.getByRole('table', { name: 'Thu chi 3 tháng gần nhất' });

test('GVCN thấy bốn thẻ số liệu, giao dịch gần đây, thông tin quỹ và biểu đồ 3 tháng; không có nút thêm khoản', async ({ page }) => {
  await open(page, TEACHER, OVERVIEW_TEACHER);

  await expect(page.getByRole('heading', { name: 'Tổng quan', level: 1 })).toBeVisible();
  await expect(page.getByTestId('kpi-balance')).toHaveText(vnd(1_150_000));
  await expect(page.getByTestId('kpi-income')).toHaveText(vnd(1_500_000));
  await expect(page.getByTestId('kpi-expense')).toHaveText(vnd(350_000));
  await expect(page.getByTestId('kpi-month')).toHaveText(`+${vnd(150_000)}`);

  const recent = page.getByTestId('recent').getByRole('listitem');
  await expect(recent).toHaveCount(3);
  await expect(recent.first()).toContainText('Đóng quỹ tháng 11');
  await expect(recent.first()).toContainText('10/11/2026');
  await expect(recent.first()).toContainText(`+${vnd(500_000)}`);
  await expect(recent.nth(1)).toContainText(`−${vnd(200_000)}`);

  const managers = page.getByTestId('managers');
  await expect(managers).toContainText('Trần Văn Hùng');
  await expect(managers).toContainText('GVCN');
  await expect(managers).toContainText('Phạm Thu Trang');
  await expect(page.getByTestId('trend-chart')).toBeVisible();
  await expect(chartTable(page).getByRole('row')).toHaveCount(4); // tiêu đề + 3 tháng

  await expect(page.getByRole('button', { name: '+ Khoản thu' })).toBeHidden();
  await expect(page.getByRole('link', { name: 'Phân công thủ quỹ' }).first()).toBeVisible();
  await expect(page.getByTestId('negative-balance')).toBeHidden();
  await expect(page.getByTestId('no-treasurer')).toBeHidden();
});

test('biểu đồ lấy đúng 3 tháng gần nhất từ reports/yearly, đầu năm gọi thêm năm trước', async ({ page }) => {
  await page.clock.setFixedTime(new Date('2026-01-15T05:00:00Z'));
  // Năm 2025 đã hết (currentMonth = 12); năm 2026 mới tới tháng 1.
  const calls = await open(page, TEACHER, OVERVIEW_TEACHER, (year) => yearlyReport(year, year === 2026 ? 1 : 12));

  const rows = chartTable(page).getByRole('row');
  await expect(rows).toHaveCount(4);
  await expect(rows.nth(1)).toContainText('11/2025');
  await expect(rows.nth(1).getByRole('cell')).toHaveText([vnd(4_500_000), vnd(4_750_000)]);
  await expect(rows.nth(2)).toContainText('12/2025');
  await expect(rows.nth(2).getByRole('cell')).toHaveText([vnd(3_500_000), vnd(1_930_000)]);
  await expect(rows.nth(3)).toContainText('01/2026');
  await expect(rows.nth(3).getByRole('cell')).toHaveText([vnd(3_500_000), vnd(1_930_000)]);
  expect(years(calls)).toEqual([2025, 2026]);
});

test('giữa năm chỉ gọi reports/yearly của năm hiện tại', async ({ page }) => {
  await page.clock.setFixedTime(new Date('2026-11-12T05:00:00Z'));
  const calls = await open(page, TEACHER, OVERVIEW_TEACHER, (year) => yearlyReport(year, 11));

  const rows = chartTable(page).getByRole('row');
  await expect(rows.nth(1)).toContainText('09/2026');
  await expect(rows.nth(1).getByRole('cell')).toHaveText([vnd(4_800_000), vnd(2_100_000)]);
  await expect(rows.nth(3)).toContainText('11/2026');
  await expect(rows.nth(3).getByRole('cell')).toHaveText([vnd(3_500_000), vnd(1_930_000)]);
  expect(years(calls)).toEqual([2026]);
});

test('số dư âm và chưa có thủ quỹ: hai cảnh báo vàng, số dư tô đỏ, GVCN có link phân công', async ({ page }) => {
  await open(page, TEACHER, OVERVIEW_NEGATIVE_NO_TREASURER);

  await expect(page.getByTestId('negative-balance')).toContainText('Số dư quỹ đang âm');
  await expect(page.getByTestId('no-treasurer')).toContainText('Chưa phân công thủ quỹ');
  await expect(page.getByTestId('kpi-balance')).toHaveText(vnd(-150_000));
  await expect(page.getByTestId('kpi-balance')).toHaveClass(/cf-expense/);
  await expect(page.getByTestId('kpi-month')).toHaveText(vnd(-150_000));
  await expect(page.getByTestId('managers')).not.toContainText('Thủ quỹ');
  await page.getByTestId('no-treasurer').getByRole('link', { name: 'Phân công thủ quỹ' }).click();
  await expect(page).toHaveURL(new RegExp(`/funds/${FUND}/treasurers$`));
});

test('quỹ mới chưa có giao dịch: số liệu bằng 0 và gợi ý trống', async ({ page }) => {
  await open(page, TREASURER, OVERVIEW_EMPTY);

  await expect(page.getByTestId('kpi-balance')).toHaveText(vnd(0));
  await expect(page.getByText('Chưa có khoản thu chi nào.')).toBeVisible();
  await expect(page.getByTestId('recent')).toBeHidden();
});

test('thủ quỹ: + Khoản chi mở dialog T-01, link Danh mục và Thành viên, không có link của GVCN', async ({ page }) => {
  await signInAs(page, TREASURER);
  await mockCategories(page);
  await mockOverview(page, OVERVIEW_TREASURER);
  await mockReports(page);
  await page.goto(`/funds/${FUND}`);

  await expect(page.getByRole('link', { name: 'Danh mục thu chi' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Thành viên quỹ' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Đổi tên quỹ' })).toBeHidden();
  await page.getByRole('button', { name: '+ Khoản chi' }).click();
  await expect(page.getByRole('dialog', { name: 'Thêm khoản chi' })).toBeVisible();
  await page.getByRole('dialog').getByRole('button', { name: 'Hủy' }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
});

for (const [label, me] of [['học sinh', STUDENT], ['phụ huynh', PARENT]] as const) {
  test(`${label}: bản công khai – cùng số tổng, chỉ khoản công khai, không nút hay link quản lý`, async ({ page }) => {
    await open(page, me, OVERVIEW_PUBLIC);

    await expect(page.getByTestId('public-note')).toContainText('bản công khai');
    await expect(page.getByTestId('kpi-balance')).toHaveText(vnd(OVERVIEW_PUBLIC.currentBalance));
    await expect(page.getByTestId('recent').getByRole('listitem')).toHaveCount(2);
    await expect(page.getByRole('button', { name: '+ Khoản thu' })).toBeHidden();
    await expect(page.getByRole('link', { name: 'Phân công thủ quỹ' })).toBeHidden();
    await expect(page.getByRole('link', { name: 'Danh mục thu chi' })).toBeHidden();
    await expect(page.getByTestId('managers')).toBeVisible();
  });
}

test('lỗi tải tổng quan: hiện trạng thái lỗi, "Thử lại" tải được', async ({ page }) => {
  await signInAs(page, TEACHER);
  let fail = true;
  await page.route(new RegExp(`/api/v1/funds/${FUND}$`), (route) =>
    fail
      ? route.fulfill({ status: 500, contentType: 'application/problem+json', json: { status: 500, code: 'server_error' } })
      : route.fulfill({ json: OVERVIEW_TEACHER }),
  );
  await mockOverviewYearlyOnly(page);
  await page.goto(`/funds/${FUND}`);

  await expect(page.getByText('Không tải được dữ liệu')).toBeVisible();
  fail = false;
  await page.getByRole('button', { name: 'Thử lại' }).click();
  await expect(page.getByTestId('kpi-balance')).toHaveText(vnd(1_150_000));
});

test('chỉ biểu đồ lỗi: các thẻ số liệu vẫn hiện, "Thử lại" tải lại biểu đồ', async ({ page }) => {
  await signInAs(page, TEACHER);
  let fail = true;
  await page.route(new RegExp(`/api/v1/funds/${FUND}$`), (route) => route.fulfill({ json: OVERVIEW_TEACHER }));
  await page.route(new RegExp(`/api/v1/funds/${FUND}/reports/yearly`), (route) =>
    fail
      ? route.fulfill({ status: 500, contentType: 'application/problem+json', json: { status: 500, code: 'server_error' } })
      : route.fulfill({ json: { year: 2026, months: [] } }),
  );
  await page.goto(`/funds/${FUND}`);

  await expect(page.getByTestId('kpi-balance')).toHaveText(vnd(1_150_000));
  await expect(page.getByText('Không tải được biểu đồ')).toBeVisible();
  fail = false;
  await page.getByRole('button', { name: 'Thử lại' }).click();
  await expect(page.getByTestId('trend-chart')).toBeVisible();
});

test('quỹ không thuộc người dùng: trang không tìm thấy', async ({ page }) => {
  await signInAs(page, TEACHER);
  await page.route(new RegExp(`/api/v1/funds/${FUND}$`), (route) =>
    route.fulfill({ status: 404, contentType: 'application/problem+json', json: { status: 404, code: 'not_found' } }),
  );
  await mockOverviewYearlyOnly(page);
  await page.goto(`/funds/${FUND}`);

  await expect(page.getByText('Không tìm thấy trang')).toBeVisible();
});

async function mockOverviewYearlyOnly(page: Page): Promise<void> {
  await page.route(new RegExp(`/api/v1/funds/${FUND}/reports/yearly`), (route) =>
    route.fulfill({ json: { year: 2026, months: [] } }),
  );
}
