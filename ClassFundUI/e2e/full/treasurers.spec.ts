import { Browser, Page, expect, test } from '@playwright/test';

/**
 * M4-T3: phân quyền thủ quỹ với ClassFund.Api thật (E2E_FULL=1) – CHỈ chạy trên PostgreSQL tạm trong Docker
 * (docs/testing/test-strategy.md), KHÔNG trỏ Neon dùng chung: các test tạo tài khoản mới.
 * Thủ quỹ chính là tài khoản seed thuquy.demo@classfund.local (mật khẩu Demo1234, không bị đổi trong file này).
 */
test.skip(process.env['E2E_FULL'] !== '1', 'Chỉ chạy với E2E_FULL=1 (cần ClassFund.Api thật).');

const OWNER = 'thuquy.demo@classfund.local';
const OWNER_PASSWORD = 'Demo1234';
const FUND_NAME = 'Lớp 10A1 (demo)';
const NEW_PASSWORD = 'MatKhauMoi1';

/** Hậu tố ngẫu nhiên để tên học sinh không trùng giữa các lần chạy trên cùng một DB tạm. */
const tag = () => Math.random().toString(36).slice(2, 6).toUpperCase();

const newEmail = () => `tq-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;

async function loginAs(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
}

const sidebar = (page: Page) => page.getByRole('navigation', { name: 'Điều hướng chính' });

async function openFund(page: Page): Promise<void> {
  await expect(page).toHaveURL(/\/funds$/);
  await page.getByRole('link', { name: `Mở quỹ ${FUND_NAME}` }).click();
  await expect(sidebar(page)).toBeVisible();
}

async function ownerOnTreasurers(page: Page): Promise<void> {
  await loginAs(page, OWNER, OWNER_PASSWORD);
  await openFund(page);
  await sidebar(page).getByRole('link', { name: 'Thủ quỹ' }).click();
  await expect(page.getByRole('heading', { name: 'Thủ quỹ', exact: true })).toBeVisible();
}

/** Thủ quỹ chính tạo một học sinh qua API (cùng phiên cookie của trang) rồi học sinh vào bằng mật khẩu tạm và đổi mật khẩu. */
async function createActiveStudent(page: Page, browser: Browser, name: string): Promise<{ email: string; student: Page }> {
  const email = newEmail();
  const token = (await (await page.request.get('/api/v1/auth/antiforgery')).json()).antiforgeryToken;
  const fundId = /\/funds\/([0-9a-f-]{36})/.exec(page.url())?.[1];
  const created = await page.request.post(`/api/v1/funds/${fundId}/members`, { headers: { 'X-XSRF-TOKEN': token }, data: { email, fullName: name } });
  expect(created.status()).toBe(201);
  const temporaryPassword = (await created.json()).temporaryPassword as string;

  const student = await (await browser.newContext()).newPage();
  await loginAs(student, email, temporaryPassword);
  await expect(student).toHaveURL(/\/change-password$/);
  await student.getByLabel('Mật khẩu tạm', { exact: true }).fill(temporaryPassword);
  await student.getByLabel('Mật khẩu mới', { exact: true }).fill(NEW_PASSWORD);
  await student.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill(NEW_PASSWORD);
  await student.getByRole('button', { name: 'Lưu và vào lớp' }).click();
  await openFund(student);
  return { email, student };
}

/** Đường dẫn API của quỹ đang mở trên trang `page` (mã quỹ lấy từ URL giao diện). */
function fundApi(page: Page, path: string): string {
  const fundId = /\/funds\/([0-9a-f-]{36})/.exec(page.url())?.[1];
  return `/api/v1/funds/${fundId}${path}`;
}

async function pick(page: Page, search: string, name: RegExp): Promise<void> {
  await page.getByRole('combobox').click();
  await page.getByRole('combobox').fill(search);
  await expect(page.getByRole('option', { name })).toBeVisible();
  await page.getByRole('option', { name }).click();
}

test('phân quyền rồi thu hồi: quyền của học sinh đổi NGAY ở phiên đang mở, không cần đăng nhập lại', async ({ page, browser }) => {
  await ownerOnTreasurers(page);
  const code = tag();
  const name = `Trần Sắp Làm ${code}`;
  const { email, student } = await createActiveStudent(page, browser, name);

  // Học sinh: chưa có mục quản lý, vào thẳng trang Học sinh bị chặn.
  await expect(sidebar(student).getByRole('link', { name: 'Học sinh' })).toHaveCount(0);

  // Thủ quỹ chính phân quyền.
  await page.reload();
  await pick(page, code, new RegExp(name));
  await page.getByRole('button', { name: 'Phân quyền thủ quỹ' }).click();
  await expect(page.getByText(`Đã phân quyền thủ quỹ cho ${name}.`)).toBeVisible();
  const row = page.getByRole('listitem').filter({ hasText: name });
  await expect(row).toContainText('Phân quyền');
  await expect(row.getByRole('button', { name: /Thu hồi/ })).toBeVisible();

  // Cùng phiên của học sinh (không đăng nhập lại): tải lại trang là thấy quyền mới.
  await student.reload();
  await expect(sidebar(student).getByRole('link', { name: 'Học sinh' })).toBeVisible();
  await expect(sidebar(student).getByRole('link', { name: 'Xác nhận thanh toán' })).toBeVisible();
  await expect(student.getByRole('banner')).toContainText('Thủ quỹ');
  // Nhưng thủ quỹ không phân quyền thủ quỹ khác (FR-13): không có mục Thủ quỹ, gõ thẳng đường dẫn bị chặn.
  await expect(sidebar(student).getByRole('link', { name: 'Thủ quỹ', exact: true })).toHaveCount(0);
  await student.goto(page.url());
  await expect(student).toHaveURL(/\/forbidden$/);
  expect((await student.request.get(fundApi(page, '/members'))).status()).toBe(200); // backend cho phép ngay

  // Thu hồi.
  await row.getByRole('button', { name: /Thu hồi/ }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Thu hồi' }).click();
  await expect(page.getByText(`Đã thu hồi quyền thủ quỹ của ${name}.`)).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: name })).toHaveCount(0);

  await student.goBack();
  await student.reload();
  await expect(sidebar(student).getByRole('link', { name: 'Học sinh' })).toHaveCount(0);
  expect((await student.request.get(fundApi(page, '/members'))).status()).toBe(403); // backend chặn ngay
  // Vẫn là thành viên của quỹ.
  await expect(sidebar(student).getByRole('link', { name: 'Tổng quan' })).toBeVisible();
  await student.goto('/account');
  await expect(student.getByLabel('Email')).toHaveValue(email);
});

test('thủ quỹ chính không có nút thu hồi; học sinh bị ngừng không còn trong danh sách chọn', async ({ page, browser }) => {
  await ownerOnTreasurers(page);
  const owner = page.getByRole('listitem').filter({ hasText: 'Thủ quỹ Demo' });
  await expect(owner).toContainText('Người tạo quỹ · không thể thu hồi');
  await expect(owner.getByRole('button')).toHaveCount(0); // AZ-03

  const code = tag();
  const name = `Lê Sẽ Bị Ngừng ${code}`;
  const { email } = await createActiveStudent(page, browser, name);
  await page.reload();
  await page.getByRole('combobox').click();
  await page.getByRole('combobox').fill(code);
  await expect(page.getByRole('option', { name: new RegExp(name) })).toBeVisible();

  // Ngừng ở trang Học sinh rồi quay lại: không còn trong danh sách chọn.
  const fundId = /\/funds\/([0-9a-f-]{36})/.exec(page.url())?.[1];
  const token = (await (await page.request.get('/api/v1/auth/antiforgery')).json()).antiforgeryToken;
  const members = await (await page.request.get(`/api/v1/funds/${fundId}/members?search=${encodeURIComponent(email)}`)).json();
  const remove = await page.request.post(`/api/v1/funds/${fundId}/members/${members.items[0].id}/remove`, { headers: { 'X-XSRF-TOKEN': token } });
  expect(remove.status()).toBe(200);
  await page.reload();
  await page.getByRole('combobox').click();
  await page.getByRole('combobox').fill(code);
  await expect(page.getByRole('option', { name: new RegExp(name) })).toHaveCount(0);
});

test('thủ quỹ thường không vào được trang Thủ quỹ (menu và đường dẫn trực tiếp)', async ({ page, browser }) => {
  await ownerOnTreasurers(page);
  const code = tag();
  const name = `Phạm Thủ Quỹ ${code}`;
  const { student } = await createActiveStudent(page, browser, name);
  await page.reload();
  await pick(page, code, new RegExp(name));
  await page.getByRole('button', { name: 'Phân quyền thủ quỹ' }).click();
  await expect(page.getByText(`Đã phân quyền thủ quỹ cho ${name}.`)).toBeVisible();

  await student.reload();
  await expect(sidebar(student).getByRole('link', { name: 'Danh mục' })).toBeVisible();
  await student.goto(page.url());
  await expect(student).toHaveURL(/\/forbidden$/);
  const treasurersApi = fundApi(page, '/treasurers');
  const token = (await (await student.request.get('/api/v1/auth/antiforgery')).json()).antiforgeryToken;
  expect((await student.request.get(treasurersApi)).status()).toBe(403);
  expect((await student.request.post(treasurersApi, { headers: { 'X-XSRF-TOKEN': token }, data: { memberId: '00000000-0000-0000-0000-000000000001' } })).status()).toBe(403);
});
