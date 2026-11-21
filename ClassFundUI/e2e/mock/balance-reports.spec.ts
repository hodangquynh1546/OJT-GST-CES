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

test('B-01: balance, other funds, history and withdraw-all confirmation', async ({ page }) => {
  const api = await FakeBalanceReportsApi.open(page, student, `${FUND}/balance`);

  await expect(page.getByRole('heading', { name: 'Số dư cá nhân' })).toBeVisible();
  await expect(page.getByText('Số dư hiện có · Lớp 10A1')).toBeVisible();
  await expect(page.getByText('CLB Tiếng Anh')).toBeVisible();
  const grid = page.locator('kendo-grid');
  await expect(grid.getByText('Sự kiện: Tham quan bảo tàng')).toBeVisible();
  await expect(grid.getByText('−25.000 đ')).toBeVisible();
  await shot(page, 'app-b01');

  await page.getByRole('button', { name: 'Rút toàn bộ 38.000 đ' }).click();
  const dialog = page.locator('kendo-dialog');
  await expect(dialog.getByText('Rút toàn bộ 38.000 đ?')).toBeVisible();
  await dialog.getByRole('button', { name: 'Gửi yêu cầu rút' }).click();

  await expect(page.getByText('Đã gửi yêu cầu rút 38.000 đ', { exact: false })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Đang chờ thủ quỹ trả tiền' })).toBeDisabled();
  expect(api.last('POST', '/me/withdrawal-requests')).toBeDefined();
});

test('B-02: pending list, mark paid, reject with reason', async ({ page }) => {
  const api = await FakeBalanceReportsApi.open(page, owner, `${FUND}/withdrawals`);
  await expect(page.getByRole('heading', { name: 'Yêu cầu rút tiền' })).toBeVisible();
  const grid = page.locator('kendo-grid');
  await expect(grid.getByText('ha.le@truong.edu.vn')).toBeVisible();
  await expect(grid.getByText('Phạm Đức Long')).toHaveCount(0);
  await shot(page, 'app-b02');

  await grid.locator('tr', { hasText: 'Lê Thu Hà' }).getByRole('button', { name: 'Đã trả tiền' }).click();
  await expect(page.getByText('Đã xác nhận trả 38.000 đ cho Lê Thu Hà.')).toBeVisible();
  expect(api.last('POST', '/withdrawal-requests/w1/paid')).toBeDefined();

  await grid.locator('tr', { hasText: 'Đỗ Khánh Linh' }).getByRole('button', { name: 'Từ chối' }).click();
  const dialog = page.locator('kendo-dialog');
  await dialog.getByRole('button', { name: 'Từ chối' }).click();
  await expect(dialog.getByText('Vui lòng nhập lý do từ chối.')).toBeVisible();
  await dialog.getByLabel('Lý do').fill('Đóng khoản Dã ngoại trước');
  await dialog.getByRole('button', { name: 'Từ chối' }).click();
  await expect(page.getByText('Đã từ chối và gửi lý do cho thành viên.')).toBeVisible();
  expect(api.last('POST', '/withdrawal-requests/w2/reject')?.body).toEqual({ reason: 'Đóng khoản Dã ngoại trước' });

  await page.getByRole('tab', { name: 'Đã trả' }).click();
  await expect(grid.getByText('Đã trả 18/11', { exact: false }).first()).toBeVisible();
});

test('S-01 treasurer: KPI, recent transactions, open charges, events, todo', async ({ page }) => {
  await FakeBalanceReportsApi.open(page, owner, FUND);
  await expect(page.getByRole('heading', { name: 'Tổng quan quỹ chung' })).toBeVisible();
  await expect(page.locator('.kpi', { hasText: 'Số dư quỹ chung' })).toContainText('4.820.000 đ');
  await expect(page.getByText('Chuyển khoản · đã xác nhận')).toBeVisible();
  await expect(page.getByText('Tiền mặt · chưa đối chiếu với thủ quỹ khác')).toBeVisible();
  await expect(page.getByRole('region', { name: 'Khoản thu chưa hoàn thành' }).getByText('26/32')).toBeVisible();
  await expect(page.getByRole('region', { name: 'Việc cần làm' }).getByText('Quyết toán “Liên hoan giữa kỳ”')).toBeVisible();
  await shot(page, 'app-s01');
});

test('S-01 student: greeting, my part, due alert, notifications', async ({ page }) => {
  await FakeBalanceReportsApi.open(page, student, FUND);
  await expect(page.getByRole('heading', { name: 'Chào Thu Hà' })).toBeVisible();
  await expect(page.locator('.kpi', { hasText: 'Bạn cần đóng' })).toContainText('580.000 đ');
  await expect(page.getByText('Chưa đóng khoản nào khi sự kiện kết thúc', { exact: false })).toBeVisible();
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
  await expect(page.getByText('HK1 chờ xác nhận')).toBeVisible();
  await expect(page.getByText('Học sinh chỉ xem được dòng của chính mình (FR-51).')).toBeVisible();
});
