import { Page, expect, test } from '@playwright/test';

import { FUND_ID, FakeBalanceReportsApi, owner, student } from '../support/fake-balance-reports-api';

/** M10-T4 + M13-T3 (FE): B-01, B-02, S-01, S-02 theo canvas (API giả lập). */

const FUND = `/funds/${FUND_ID}`;

/** CF_SHOTS=1 lưu ảnh màn để đối chiếu với canvas. */
async function shot(page: Page, name: string): Promise<void> {
  if (process.env['CF_SHOTS']) {
    await page.screenshot({ path: `../.playwright-mcp/${name}.png`, fullPage: true });
  }
}

test('B-01: chỉ xem số dư và lịch sử, không có nút rút', async ({ page }) => {
  await FakeBalanceReportsApi.open(page, student, `${FUND}/balance`);

  await expect(page.getByRole('heading', { name: 'Số dư cá nhân' })).toBeVisible();
  await expect(page.getByText('Số dư hiện có · Lớp 10A1')).toBeVisible();
  await expect(page.getByText('Số dư ở các quỹ khác')).toHaveCount(0);
  const grid = page.locator('kendo-grid');
  await expect(grid.getByText('Sự kiện: Tham quan bảo tàng')).toBeVisible();
  await expect(grid.getByText('Hoàn khi đóng danh sách: Dã ngoại Ba Vì')).toBeVisible();
  await expect(grid.getByText('−25.000 đ')).toBeVisible();
  await expect(page.getByRole('button', { name: /Rút/ })).toHaveCount(0);
  await shot(page, 'app-b01');
});

test('B-02 Rút số dư: danh sách học sinh, kiểm tra số tiền, rút và lịch sử', async ({ page }) => {
  const api = await FakeBalanceReportsApi.open(page, owner, `${FUND}/withdrawals`);
  await expect(page.getByRole('heading', { name: 'Rút số dư' })).toBeVisible();
  const grid = page.locator('kendo-grid').first();
  await expect(grid.getByText('ha.le@truong.edu.vn')).toBeVisible();
  await expect(page.getByText('Thủ quỹ đang giữ hộ')).toContainText('101.000 đ');
  await expect(grid.locator('tr', { hasText: 'Phạm Đức Long' }).getByRole('button', { name: 'Rút tiền' })).toBeDisabled();
  await shot(page, 'app-b02');

  await grid.locator('tr', { hasText: 'Lê Thu Hà' }).getByRole('button', { name: 'Rút tiền' }).click();
  const dialog = page.locator('kendo-dialog');
  const confirm = dialog.getByRole('button', { name: 'Xác nhận rút' });
  await expect(confirm).toBeDisabled();
  const amount = dialog.getByLabel('Số tiền rút (đ)');
  await amount.fill('5000');
  await expect(dialog.getByText('Số tiền rút tối thiểu 10.000đ.')).toBeVisible();
  await amount.fill('10500');
  await expect(dialog.getByText('Số tiền rút phải là bội của 1.000đ (dạng xx.000).')).toBeVisible();
  await amount.fill('40000');
  await expect(dialog.getByText('Số tiền rút vượt quá số dư hiện có.')).toBeVisible();
  await expect(confirm).toBeDisabled();
  expect(api.last('POST', '/balances/u-ha/withdraw')).toBeUndefined();

  await amount.fill('30000');
  await confirm.click();
  await expect(page.getByText('Đã ghi nhận rút 30.000 đ của Lê Thu Hà.')).toBeVisible();
  expect(api.last('POST', '/balances/u-ha/withdraw')?.body).toEqual({ amount: 30000 });
  await expect(grid.locator('tr', { hasText: 'Lê Thu Hà' })).toContainText('8.000 đ');
  await expect(page.getByRole('region', { name: 'Lịch sử rút' })).toContainText('30.000 đ');
});

test('S-01 treasurer: KPI, recent transactions, open charges, events, todo', async ({ page }) => {
  await FakeBalanceReportsApi.open(page, owner, FUND);
  await expect(page.getByRole('heading', { name: 'Tổng quan quỹ chung' })).toBeVisible();
  await expect(page.locator('.kpi', { hasText: 'Số dư quỹ chung' })).toContainText('4.820.000 đ');
  await expect(page.getByText('Chuyển khoản · đã xác nhận')).toBeVisible();
  await expect(page.getByText('Tiền mặt · chưa đối chiếu với thủ quỹ khác')).toBeVisible();
  await expect(page.getByRole('region', { name: 'Khoản thu chưa hoàn thành' }).getByText('26/32')).toBeVisible();
  await expect(page.getByRole('region', { name: 'Việc cần làm' }).getByText('Quyết toán “Liên hoan giữa kỳ”')).toBeVisible();
  await expect(page.getByRole('region', { name: 'Việc cần làm' }).getByText('Đóng danh sách “Dã ngoại Ba Vì”')).toBeVisible();
  await shot(page, 'app-s01');
});

test('S-01 student: greeting, my part, due alert, notifications', async ({ page }) => {
  await FakeBalanceReportsApi.open(page, student, FUND);
  await expect(page.getByRole('heading', { name: 'Chào Thu Hà' })).toBeVisible();
  await expect(page.locator('.kpi', { hasText: 'Bạn chưa nộp' })).toContainText('580.000 đ');
  await expect(page.getByText('Chưa nộp đủ các khoản khi đóng danh sách sự kiện', { exact: false })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Thông báo mới' }).getByText('Tiền thừa +38.000 đ')).toBeVisible();
  await expect(page.getByText('Giao dịch gần đây')).toHaveCount(0);
  await shot(page, 'app-s01-student');
});

test('S-02: general tab, events tab, students tab and CSV export', async ({ page }) => {
  await FakeBalanceReportsApi.open(page, owner, `${FUND}/reports`);
  await expect(page.getByRole('heading', { name: 'Báo cáo' })).toBeVisible();
  await expect(page.getByText('Chi theo danh mục')).toBeVisible();
  await expect(page.getByRole('cell', { name: '11/2026' })).toBeVisible();
  await expect(page.getByText('trong đó tự ghi nhận 200.000 đ')).toBeVisible();
  await shot(page, 'app-s02');

  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Xuất Excel' }).click();
  expect((await download).suggestedFilename()).toMatch(/^bao-cao-thu-chi-chung-\d{4}-\d{2}-\d{2}\.csv$/);

  await page.getByRole('tab', { name: 'Sự kiện' }).click();
  await expect(page.getByText('Cộng số dư 1.140.000')).toBeVisible();
  await expect(page.getByText('Chờ quyết toán')).toBeVisible();
  await shot(page, 'app-s02-events');

  await page.getByRole('tab', { name: 'Học sinh' }).click();
  await expect(page.locator('.cf-tag--danger', { hasText: 'Áo lớp' })).toBeVisible();
  await expect(page.getByText('Học sinh chỉ xem được dòng của chính mình.')).toBeVisible();
});
