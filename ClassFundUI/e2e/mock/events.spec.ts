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
  await expect(picnic.getByText('Đang mở đăng ký & thu tiền')).toBeVisible();
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
  const [date, deadline] = [dialog.locator('kendo-datepicker input').nth(0), dialog.locator('kendo-datepicker input').nth(1)];
  await date.click();
  await date.press('Home');
  await date.pressSequentially('19122099');
  await expect(date).toHaveValue('19/12/2099');
  await dialog.getByRole('button', { name: 'Tạo và gửi lời mời' }).click();
  await expect(dialog.getByText('Vui lòng chọn hạn đóng danh sách.')).toBeVisible();
  await deadline.click();
  await deadline.press('Home');
  await deadline.pressSequentially('10122099');
  await expect(deadline).toHaveValue('10/12/2099');
  await shot(page, 'app-events-new');
  await dialog.getByRole('button', { name: 'Tạo và gửi lời mời' }).click();

  await expect(page.getByText('Đã tạo sự kiện và gửi lời mời tới 32 thành viên.')).toBeVisible();
  expect(api.last('POST', '/events')?.body).toEqual({ name: 'Văn nghệ chào mừng 20/11', description: 'Thuê trang phục, đạo cụ', eventDate: '2099-12-19', listDeadline: '2099-12-10' });
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

test('E-03 treasurer: KPI, participant filter, close the list with preview and refund', async ({ page }) => {
  const api = await FakeMoneyApi.open(page, owner, `${EVENTS}/e1`);

  await expect(page.getByRole('heading', { name: 'Dã ngoại Ba Vì' })).toBeVisible();
  await expect(page.getByLabel('Tiến trình sự kiện')).toContainText('Đang mở – đăng ký & thu tiền');
  await expect(page.locator('.kpi', { hasText: 'Số dư sự kiện' })).toContainText('4.300.000 đ');
  const grid = page.locator('kendo-grid');
  await expect(grid.getByText('Thủ quỹ chính')).toBeVisible();
  await expect(grid.getByText('Tự ghi nhận')).toBeVisible();
  await expect(grid.getByText('Chưa nộp khoản nào – bị loại khi đóng danh sách')).toHaveCount(2);
  await expect(grid.getByText('Thiếu 80.000 đ', { exact: false })).toHaveCount(2);
  await shot(page, 'app-event-owner');

  await page.getByRole('tab', { name: 'Chưa nộp 4' }).click();
  await expect(grid.getByText('Lê Thu Hà')).toHaveCount(0);

  await page.getByRole('tab', { name: 'Khoản thu sự kiện' }).click();
  await expect(page.getByText('350.000 đ/người · cùng một mức cho mọi người', { exact: false })).toBeVisible();
  await expect(page.getByText('4 đã nộp').first()).toBeVisible();

  await page.getByRole('tab', { name: 'Thu chi sự kiện' }).click();
  await expect(page.locator('kendo-grid').getByText('Đặt xe 45 chỗ')).toBeVisible();
  await expect(page.locator('kendo-grid').getByText('Tự động')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Ghi chi sự kiện' })).toHaveCount(0);
  await expect(page.getByText('Chỉ ghi chi sau khi đóng danh sách')).toBeVisible();

  await page.getByRole('button', { name: 'Đóng danh sách' }).click();
  const dialog = page.locator('kendo-dialog');
  await expect(dialog.getByText('Bị loại – chưa nộp đủ (4 người, hoàn 700.000 đ)')).toBeVisible();
  await expect(dialog.getByText('Bùi Thảo My – chưa nộp khoản nào')).toBeVisible();
  await expect(dialog.getByText('Phạm Đức Long – hoàn 350.000 đ vào số dư')).toBeVisible();
  const confirm = dialog.getByRole('button', { name: 'Đóng danh sách (còn 2 người)' });
  await expect(confirm).toBeDisabled(); // chưa tới hạn: phải tích xác nhận đóng sớm
  await dialog.getByLabel(/Tôi xác nhận đóng sớm/).check();
  await shot(page, 'app-event-end');
  await confirm.click();

  await expect(page.getByText('Đã đóng danh sách: còn 2 người, loại 4 người', { exact: false })).toBeVisible();
  expect(api.last('POST', '/events/e1/close-list')?.body).toEqual({ confirmEarly: true });
  await expect(page.getByText('Đã đóng danh sách: danh sách người tham gia đã chốt', { exact: false })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Lịch sử đóng danh sách' })).toContainText('Phạm Đức Long');
  await expect(page.getByRole('link', { name: 'Quyết toán' })).toBeVisible();
});

test('E-03 "Nhắc đóng" từng người chưa đóng (TD-18)', async ({ page }) => {
  const api = await FakeMoneyApi.open(page, owner, `${EVENTS}/e1`);
  const grid = page.locator('kendo-grid');

  const row = grid.getByRole('row', { name: /Bùi Thảo My/ });
  await row.getByRole('button', { name: 'Nhắc đóng' }).click();
  await expect(page.getByText('Đã nhắc Bùi Thảo My đóng tiền.')).toBeVisible();
  expect(api.last('POST', '/events/e1/participants/u-my/remind')).toBeTruthy();

  await row.getByRole('button', { name: 'Nhắc đóng' }).click();
  await expect(page.getByText('Vừa nhắc xong. Vui lòng chờ ít nhất 1 giờ rồi nhắc lại.')).toBeVisible();
  await expect(grid.getByRole('row', { name: /Lê Thu Hà/ }).getByRole('button', { name: 'Nhắc đóng' })).toHaveCount(0);
});

test('E-03 treasurer records an event expense within the event balance', async ({ page }) => {
  const api = await FakeMoneyApi.open(page, owner, `${EVENTS}/e1`);
  // Chỉ ghi chi được sau khi đóng danh sách: sự kiện e2 (Completed) mô phỏng bước này.
  await page.goto(`${EVENTS}/e2`);
  await page.getByRole('tab', { name: 'Thu chi sự kiện' }).click();
  await page.getByRole('button', { name: 'Ghi chi sự kiện' }).click();
  const dialog = page.locator('kendo-dialog');
  await expect(dialog.getByText('Sự kiện: Liên hoan giữa kỳ')).toBeVisible();
  await expect(dialog.getByText('Tối đa 150.000 đ (số dư sự kiện)')).toBeVisible();
  await expect(dialog.getByRole('radio', { name: 'Thu' })).toHaveCount(0);

  await dialog.getByLabel('Tên khoản chi').fill('Nước uống cho cả đoàn');
  await dialog.getByLabel('Số tiền (đ)').fill('100000');
  await dialog.locator('kendo-dropdownlist').click();
  await page.getByRole('option', { name: 'Quà tặng' }).click();
  await dialog.getByRole('button', { name: 'Lưu khoản chi' }).click();

  await expect(page.getByText('Đã ghi khoản chi vào sổ thu chi của sự kiện.', { exact: false })).toBeVisible();
  expect(api.last('POST', '/ledger-entries')?.body).toMatchObject({ eventId: 'e2', amount: 100000, attachmentIds: [] });
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
  await expect(page.getByRole('button', { name: 'Đóng danh sách' })).toHaveCount(0);
  await expect(page.getByText('Bạn chưa trả lời.', { exact: false })).toBeVisible();

  await page.getByRole('radio', { name: 'Tham gia', exact: true }).click();
  await expect(page.getByText('Phí dã ngoại · 350.000 đ')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Nộp tiền' })).toHaveCount(0);
  await expect(page.getByText('Cần nộp đủ TẤT CẢ các khoản thu trước khi đóng danh sách', { exact: false })).toBeVisible();
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
  await expect(mine.getByRole('link', { name: 'Nộp tiền' })).toHaveCount(0);
  await expect(mine.getByRole('button', { name: 'Tham gia', exact: true })).toBeDisabled();

  await mine.getByRole('button', { name: 'Không tham gia' }).click();
  await expect(mine.getByText('chỉ quản lý và ghi thu chi', { exact: false })).toBeVisible();
  await expect(mine.getByRole('button', { name: 'Không tham gia' })).toBeDisabled();
  await expect(mine.getByRole('button', { name: 'Tham gia', exact: true })).toBeEnabled();
  // Vẫn là người quản lý: nút Đóng danh sách / Hủy giữ nguyên.
  await expect(page.getByRole('button', { name: 'Đóng danh sách' })).toBeVisible();
});