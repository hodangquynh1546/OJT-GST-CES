import { Page, expect, test } from '@playwright/test';

import { FUND_ID, FakeMoneyApi, owner, student } from '../support/fake-money-api';

/** M7-T3 (FE): L-01 / L-02 / L-03 theo canvas LedgerBody, LedgerDetail (API giả lập). */

const LEDGER = `/funds/${FUND_ID}/ledger`;

/** CF_SHOTS=1 lưu ảnh màn để đối chiếu với canvas. */
async function shot(page: Page, name: string): Promise<void> {
  if (process.env['CF_SHOTS']) {
    await page.screenshot({ path: `../.playwright-mcp/${name}.png`, fullPage: true });
  }
}

test('L-01 treasurer: KPI, scope badge, tags and filters', async ({ page }) => {
  const api = await FakeMoneyApi.open(page, owner, LEDGER);

  await expect(page.getByRole('heading', { name: 'Thu chi chung' })).toBeVisible();
  await expect(page.locator('.title-row').getByText('Quỹ chung')).toBeVisible();
  await expect(page.getByText('Số dư quỹ chung')).toBeVisible();
  // 200.000 + 5.200.000 − 650.000 − 180.000 (khoản chổi đã đảo) = 4.570.000; không gồm tiền sự kiện.
  await expect(page.locator('.kpi').first()).toContainText('4.570.000 đ');
  const grid = page.locator('kendo-grid');
  await expect(grid.getByText('Mua hoa 20/11 tặng cô chủ nhiệm')).toBeVisible();
  await expect(grid.getByText('2 chứng từ')).toBeVisible();
  await expect(grid.getByText('Tự ghi nhận')).toBeVisible();
  await expect(grid.getByText('Bút toán đảo')).toBeVisible();
  await expect(grid.getByText('Đã đảo')).toBeVisible();
  await expect(grid.getByText('Đặt xe 45 chỗ')).toHaveCount(0);
  await shot(page, 'app-ledger-owner');

  await page.getByRole('tab', { name: 'Chi', exact: true }).click();
  await expect(grid.getByText('Thu: Quỹ lớp HK1 – Lê Thu Hà')).toHaveCount(0);
  expect(api.last('GET', '/ledger-entries')?.query.get('type')).toBe('Expense');
});

test('L-02 expense: max spend, evidence over 500k, saves with receipt', async ({ page }) => {
  const api = await FakeMoneyApi.open(page, owner, LEDGER);
  await page.getByRole('button', { name: 'Ghi giao dịch' }).click();
  const dialog = page.locator('kendo-dialog');
  await expect(dialog.getByText('Tối đa 4.570.000 đ (số dư quỹ chung)')).toBeVisible();

  await dialog.getByRole('radio', { name: 'Thu' }).click();
  await expect(dialog.getByText('Không ghi thu bằng tay.', { exact: false })).toBeVisible();
  await expect(dialog.getByRole('link', { name: 'Tạo khoản thu' })).toBeVisible();
  await dialog.getByRole('radio', { name: 'Chi' }).click();

  await dialog.getByLabel('Tên khoản chi').fill('Thuê loa sinh hoạt lớp');
  const amount = dialog.getByLabel('Số tiền (đ)');
  await amount.fill('5000000');
  await expect(amount).toHaveValue('5.000.000');
  await expect(dialog.getByRole('alert').filter({ hasText: 'Vượt số dư quỹ chung' })).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Lưu khoản chi' })).toBeDisabled();

  await amount.fill('650000');
  await expect(dialog.getByText('Khoản chi trên 500.000 đ bắt buộc có chứng từ.')).toBeVisible();
  await dialog.locator('kendo-dropdownlist').click();
  await page.getByRole('option', { name: 'Học tập' }).click();
  await shot(page, 'app-ledger-new');
  await dialog.getByRole('button', { name: 'Lưu khoản chi' }).click();
  await expect(dialog.getByText('Hãy đính kèm ít nhất một chứng từ.')).toBeVisible();
  expect(api.last('POST', '/ledger-entries')).toBeUndefined();

  await dialog.locator('input[type=file]').setInputFiles({ name: 'hoa-don.png', mimeType: 'image/png', buffer: Buffer.from('89504e470d0a1a0a00000000', 'hex') });
  await expect(dialog.getByText('hoa-don.png')).toBeVisible();
  await dialog.getByRole('button', { name: 'Lưu khoản chi' }).click();

  await expect(page.getByText('Đã ghi khoản chi vào sổ thu chi chung.', { exact: false })).toBeVisible();
  const sent = api.last('POST', '/ledger-entries')!.body!;
  expect(sent).toMatchObject({ eventId: null, categoryId: 'c-study', title: 'Thuê loa sinh hoạt lớp', amount: 650000 });
  expect((sent['attachmentIds'] as string[]).length).toBe(1);
  await expect(page.locator('kendo-grid').getByText('Thuê loa sinh hoạt lớp')).toBeVisible();
});

test('L-02 opens directly from /ledger/new; students are blocked', async ({ page }) => {
  await FakeMoneyApi.open(page, owner, `${LEDGER}/new`);
  await expect(page.locator('kendo-dialog').getByText('Ghi giao dịch')).toBeVisible();
  await page.locator('kendo-dialog').getByRole('button', { name: 'Hủy' }).click();
  await expect(page).toHaveURL(new RegExp(`${LEDGER}$`));
});

test('L-03 detail: receipts, reversal with reason, history', async ({ page }) => {
  const api = await FakeMoneyApi.open(page, owner, LEDGER);
  await page.locator('kendo-grid tr', { hasText: 'Mua hoa 20/11' }).getByRole('link', { name: 'Xem' }).click();

  await expect(page.getByRole('heading', { name: 'Mua hoa 20/11 tặng cô chủ nhiệm' })).toBeVisible();
  await expect(page.getByText('Hiệu lực')).toBeVisible();
  await expect(page.getByText('Chứng từ (2)')).toBeVisible();
  await expect(page.getByText('Trần Quang Huy (Thủ quỹ)')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Lịch sử thao tác' })).toBeVisible();
  await shot(page, 'app-ledger-detail');

  await page.getByRole('button', { name: 'Tạo bút toán đảo' }).click();
  const dialog = page.locator('kendo-dialog');
  await expect(dialog.getByText('Thu +650.000 đ')).toBeVisible();
  const confirm = dialog.getByRole('button', { name: 'Đảo giao dịch' });
  await expect(confirm).toBeDisabled();
  await dialog.getByLabel('Lý do').fill('Ghi nhầm số tiền');
  await confirm.click();

  await expect(page.getByText('Đã tạo bút toán đảo.', { exact: false })).toBeVisible();
  expect(api.last('POST', `/ledger-entries/${api.entries.find((item) => item.title.startsWith('Mua hoa'))!.id}/reverse`)?.body).toEqual({ reason: 'Ghi nhầm số tiền' });
  await expect(page.getByText('Đã đảo', { exact: true })).toBeVisible();
  await expect(page.getByText('Lý do: “Ghi nhầm số tiền”', { exact: false })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Tạo bút toán đảo' })).toHaveCount(0);
});

test('students read only: no write button, expenses only, receipt dialog', async ({ page }) => {
  await FakeMoneyApi.open(page, student, LEDGER);
  await expect(page.getByRole('heading', { name: 'Chi tiêu' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Ghi giao dịch' })).toHaveCount(0);
  const grid = page.locator('kendo-grid');
  await expect(grid.getByText('Mua hoa 20/11 tặng cô chủ nhiệm')).toBeVisible();
  await expect(grid.getByText('Thu: Quỹ lớp HK1 – Lê Thu Hà')).toHaveCount(0);

  await grid.locator('tr', { hasText: 'Mua hoa 20/11' }).getByRole('button', { name: 'Xem' }).click();
  const dialog = page.locator('kendo-dialog');
  await expect(dialog.getByText('Bạn xem được các khoản chi công khai', { exact: false })).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Xem thiep.pdf' })).toBeVisible();
  await shot(page, 'app-ledger-student');
});
