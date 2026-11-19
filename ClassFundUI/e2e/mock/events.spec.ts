import { Page, expect, test } from '@playwright/test';

import { FUND_ID, FakeMoneyApi, owner, student } from '../support/fake-money-api';

/** M8-T3 (FE): E-01 / E-02 / E-03 theo canvas EventListBody, Event, StuEventDetail (API giả lập). */

const EVENTS = `/funds/${FUND_ID}/events`;

async function shot(page: Page, name: string): Promise<void> {
  if (process.env['CF_SHOTS']) {
    await page.screenshot({ path: `../.playwright-mcp/${name}.png`, fullPage: true });
  }
}

test('E-01 treasurer: tabs, status badges, stats and actions', async ({ page }) => {
  await FakeMoneyApi.open(page, owner, EVENTS);

  await expect(page.getByRole('heading', { name: 'Sự kiện' })).toBeVisible();
  await expect(page.getByRole('tab', { name: 'Đang diễn ra 2' })).toBeVisible();
  const picnic = page.getByRole('article', { name: 'Dã ngoại Ba Vì' });
  await expect(picnic.getByText('Đang mở đăng ký & thu chi')).toBeVisible();
  await expect(picnic.getByText('6 tham gia · 2 đã đóng đủ · thu 9.100.000 đ · chi 4.800.000 đ')).toBeVisible();
  await expect(picnic.getByRole('link', { name: 'Quản lý' })).toBeVisible();
  await expect(page.getByRole('article', { name: 'Liên hoan giữa kỳ' }).getByRole('link', { name: 'Quyết toán' })).toBeVisible();
  await shot(page, 'app-events-owner');

  await page.getByRole('tab', { name: 'Đã quyết toán / hủy 2' }).click();
  await expect(page.getByRole('article', { name: 'Cắm trại hè' }).getByText('Không phát sinh thu chi')).toBeVisible();
});

test('E-02 create event invites members', async ({ page }) => {
  const api = await FakeMoneyApi.open(page, owner, EVENTS);
  await page.getByRole('button', { name: 'Tạo sự kiện' }).click();
  const dialog = page.locator('kendo-dialog');
  await dialog.getByRole('button', { name: 'Tạo và gửi lời mời' }).click();
  await expect(dialog.getByText('Vui lòng nhập tên sự kiện.')).toBeVisible();

  await dialog.getByLabel('Tên sự kiện').fill('Văn nghệ chào mừng 20/11');
  await dialog.getByLabel('Mô tả').fill('Thuê trang phục, đạo cụ');
  const date = dialog.locator('kendo-datepicker input');
  await date.click();
  await date.press('Home');
  await date.pressSequentially('19112026');
  await expect(date).toHaveValue('19/11/2026');
  await shot(page, 'app-events-new');
  await dialog.getByRole('button', { name: 'Tạo và gửi lời mời' }).click();

  await expect(page.getByText('Đã tạo sự kiện và gửi lời mời tới 32 thành viên.')).toBeVisible();
  expect(api.last('POST', '/events')?.body).toEqual({ name: 'Văn nghệ chào mừng 20/11', description: 'Thuê trang phục, đạo cụ', eventDate: '2026-11-19' });
  await expect(page.getByRole('article', { name: 'Văn nghệ chào mừng 20/11' })).toBeVisible();
});

test('E-01 student answers on the card', async ({ page }) => {
  const api = await FakeMoneyApi.open(page, student, EVENTS);
  const picnic = page.getByRole('article', { name: 'Dã ngoại Ba Vì' });
  await expect(picnic.getByText('Bạn chưa trả lời')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Tạo sự kiện' })).toHaveCount(0);
  await shot(page, 'app-events-student');
  await picnic.getByRole('button', { name: 'Tham gia', exact: true }).click();
  await expect(page.getByText('Đã xác nhận tham gia.', { exact: false })).toBeVisible();
  expect(api.last('PUT', '/events/e1/me/response')?.body).toEqual({ response: 'Accepted' });
  await expect(picnic.getByText('Bạn: Tham gia')).toBeVisible();
});

test('E-03 treasurer: KPI, participant filter, end event with preview', async ({ page }) => {
  const api = await FakeMoneyApi.open(page, owner, `${EVENTS}/e1`);

  await expect(page.getByRole('heading', { name: 'Dã ngoại Ba Vì' })).toBeVisible();
  await expect(page.getByLabel('Tiến trình sự kiện')).toContainText('Đang mở – đăng ký & thu chi');
  await expect(page.locator('.kpi', { hasText: 'Số dư sự kiện' })).toContainText('4.300.000 đ');
  const grid = page.locator('kendo-grid');
  await expect(grid.getByText('Thủ quỹ chính')).toBeVisible();
  await expect(grid.getByText('Tự ghi nhận')).toBeVisible();
  await expect(grid.getByText('Chưa đóng khoản nào – bị loại khi kết thúc')).toHaveCount(2);
  await expect(grid.getByText('Thiếu 80.000 đ')).toBeVisible();
  await shot(page, 'app-event-owner');

  await page.getByRole('tab', { name: 'Chưa đóng 3' }).click();
  await expect(grid.getByText('Lê Thu Hà')).toHaveCount(0);

  await page.getByRole('tab', { name: 'Khoản thu sự kiện' }).click();
  await expect(page.getByText('350.000 đ/người · hạn 18/11/2026', { exact: false })).toBeVisible();

  await page.getByRole('tab', { name: 'Thu chi sự kiện' }).click();
  await expect(page.locator('kendo-grid').getByText('Đặt xe 45 chỗ')).toBeVisible();
  await expect(page.locator('kendo-grid').getByText('Tự động')).toBeVisible();

  await page.getByRole('button', { name: 'Kết thúc sự kiện' }).click();
  const dialog = page.locator('kendo-dialog');
  await expect(dialog.getByText('Bị loại – chưa đóng khoản nào (2)')).toBeVisible();
  await expect(dialog.getByText('Bùi Thảo My · Trịnh Bảo Ngọc')).toBeVisible();
  await expect(dialog.getByText('Phạm Đức Long (thiếu 80.000 đ)')).toBeVisible();
  await shot(page, 'app-event-end');
  await dialog.getByRole('button', { name: 'Kết thúc (còn 4 người)' }).click();

  await expect(page.getByText('Đã kết thúc sự kiện: còn 4 người.', { exact: false })).toBeVisible();
  expect(api.last('POST', '/events/e1/complete')).toBeDefined();
  await expect(page.getByText('Sự kiện đã kết thúc: không tạo khoản thu và không ghi chi mới.', { exact: false })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Quyết toán' })).toBeVisible();
});

test('E-03 treasurer records an event expense within the event balance', async ({ page }) => {
  const api = await FakeMoneyApi.open(page, owner, `${EVENTS}/e1`);
  await page.getByRole('tab', { name: 'Thu chi sự kiện' }).click();
  await page.getByRole('button', { name: 'Ghi chi sự kiện' }).click();
  const dialog = page.locator('kendo-dialog');
  await expect(dialog.getByText('Sự kiện: Dã ngoại Ba Vì')).toBeVisible();
  await expect(dialog.getByText('Tối đa 4.300.000 đ (số dư sự kiện)')).toBeVisible();
  await expect(dialog.getByRole('radio', { name: 'Thu' })).toHaveCount(0);

  await dialog.getByLabel('Tên khoản chi').fill('Nước uống cho cả đoàn');
  await dialog.getByLabel('Số tiền (đ)').fill('300000');
  await dialog.locator('kendo-dropdownlist').click();
  await page.getByRole('option', { name: 'Quà tặng' }).click();
  await dialog.getByRole('button', { name: 'Lưu khoản chi' }).click();

  await expect(page.getByText('Đã ghi khoản chi vào sổ thu chi của sự kiện.', { exact: false })).toBeVisible();
  expect(api.last('POST', '/ledger-entries')?.body).toMatchObject({ eventId: 'e1', amount: 300000, attachmentIds: [] });
});

test('E-03 cancel needs a reason and is refused once money exists', async ({ page }) => {
  await FakeMoneyApi.open(page, owner, `${EVENTS}/e1`);
  await page.getByRole('button', { name: 'Hủy sự kiện' }).click();
  const dialog = page.locator('kendo-dialog');
  await dialog.getByRole('button', { name: 'Hủy sự kiện' }).click();
  await expect(dialog.getByText('Vui lòng nhập lý do hủy.')).toBeVisible();
  await dialog.getByLabel('Lý do').fill('Mưa bão');
  await dialog.getByRole('button', { name: 'Hủy sự kiện' }).click();
  await expect(page.getByText('Sự kiện đã có thu tiền hoặc ghi chi, không hủy được.', { exact: false })).toBeVisible();
});

test('E-03 student: my part, overview, public expenses', async ({ page }) => {
  const api = await FakeMoneyApi.open(page, student, `${EVENTS}/e1`);
  await expect(page.getByRole('heading', { name: 'Phần của bạn' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Kết thúc sự kiện' })).toHaveCount(0);
  await expect(page.getByText('Bạn chưa trả lời.', { exact: false })).toBeVisible();

  await page.getByRole('radio', { name: 'Tham gia', exact: true }).click();
  await expect(page.getByText('Phí dã ngoại · 350.000 đ')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Nộp tiền' })).toBeVisible();
  await expect(page.getByText('Nếu chưa đóng khoản nào khi sự kiện kết thúc', { exact: false })).toBeVisible();
  await expect(page.locator('kendo-grid').getByText('Đặt xe 45 chỗ')).toBeVisible();
  expect(api.last('GET', '/ledger-entries')?.query.get('type')).toBe('Expense');
  await shot(page, 'app-event-student');
});

test('E-01 treasurer also answers; the chosen button is greyed out and not clickable', async ({ page }) => {
  const api = await FakeMoneyApi.open(page, owner, EVENTS);
  const picnic = page.getByRole('article', { name: 'Dã ngoại Ba Vì' });
  const accept = picnic.getByRole('button', { name: 'Tham gia', exact: true });
  const decline = picnic.getByRole('button', { name: 'Không tham gia' });
  await expect(picnic.getByText('Bạn chưa trả lời')).toBeVisible();
  await expect(accept).toBeEnabled();
  await expect(decline).toBeEnabled();

  await accept.click();
  await expect(picnic.getByText('Bạn: Tham gia')).toBeVisible();
  await expect(accept).toBeDisabled();
  await expect(decline).toBeEnabled();
  expect(api.last('PUT', '/events/e1/me/response')?.body).toEqual({ response: 'Accepted' });

  await decline.click();
  await expect(picnic.getByText('Bạn: Không tham gia')).toBeVisible();
  await expect(decline).toBeDisabled();
  await expect(accept).toBeEnabled();
  // Sự kiện đã kết thúc không còn nút trả lời.
  await expect(page.getByRole('article', { name: 'Liên hoan giữa kỳ' }).getByRole('button', { name: 'Tham gia', exact: true })).toHaveCount(0);
});

test('E-03 treasurer "Phần của bạn": join to pay like students, decline to only manage', async ({ page }) => {
  await FakeMoneyApi.open(page, owner, `${EVENTS}/e1`);
  const mine = page.getByRole('region', { name: 'Phần của bạn' });
  await expect(mine.getByText('Bạn chưa trả lời', { exact: false }).first()).toBeVisible();

  await mine.getByRole('button', { name: 'Tham gia', exact: true }).click();
  await expect(mine.getByText('Phí dã ngoại · 350.000 đ')).toBeVisible();
  await expect(mine.getByRole('link', { name: 'Nộp tiền' })).toBeVisible();
  await expect(mine.getByRole('button', { name: 'Tham gia', exact: true })).toBeDisabled();

  await mine.getByRole('button', { name: 'Không tham gia' }).click();
  await expect(mine.getByText('chỉ quản lý và ghi thu chi', { exact: false })).toBeVisible();
  await expect(mine.getByRole('button', { name: 'Không tham gia' })).toBeDisabled();
  await expect(mine.getByRole('button', { name: 'Tham gia', exact: true })).toBeEnabled();
  // Vẫn là người quản lý: nút Kết thúc / Hủy giữ nguyên.
  await expect(page.getByRole('button', { name: 'Kết thúc sự kiện' })).toBeVisible();
});