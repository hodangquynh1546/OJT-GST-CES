import { Browser, Page, expect, test } from '@playwright/test';

/**
 * M1-T3: luồng xác thực chạy với ClassFund.Api thật (E2E_FULL=1).
 * CHỈ chạy trên PostgreSQL tạm trong Docker (cách chạy: docs/testing/test-strategy.md), KHÔNG trỏ Neon dùng chung:
 * các test tự đăng ký tài khoản mới nên ghi dữ liệu.
 * Mỗi test tự đăng ký email riêng nên chạy lại được; tài khoản seed mật khẩu tạm chỉ dùng được một lần
 * (sau khi đổi mật khẩu phải seed lại database tạm).
 */
test.skip(process.env['E2E_FULL'] !== '1', 'Chỉ chạy với E2E_FULL=1 (cần ClassFund.Api thật).');

const PASSWORD = 'Passw0rd1';
const SEED_STUDENT = process.env['E2E_STUDENT_EMAIL'] ?? 'hocsinh1.demo@classfund.local';
const SEED_PASSWORD = 'Demo1234';

const newEmail = () => `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;

async function register(page: Page, email: string, fullName = 'Nguyễn E2E', password = PASSWORD): Promise<void> {
  await page.goto('/register');
  await page.getByLabel('Họ và tên').fill(fullName);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByLabel('Nhập lại mật khẩu', { exact: true }).fill(password);
  await page.getByRole('checkbox').check();
  await page.getByRole('button', { name: 'Tạo tài khoản' }).click();
  await expect(page).toHaveURL(/\/funds$/);
}

async function login(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
}

async function newSession(browser: Browser): Promise<Page> {
  return (await browser.newContext()).newPage();
}

test('đăng ký → vào hệ thống → tải lại vẫn đăng nhập → đăng xuất → đăng nhập lại', async ({ page }) => {
  const email = newEmail();
  await register(page, email, 'Trần Bình');
  await expect(page.getByRole('link', { name: 'Tài khoản của Trần Bình' })).toBeVisible();

  await page.reload();
  await expect(page).toHaveURL(/\/funds$/);
  await expect(page.getByRole('link', { name: 'Tài khoản của Trần Bình' })).toBeVisible();

  await page.getByRole('banner').getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto('/account');
  await expect(page).toHaveURL(/\/login$/);

  await login(page, email.toUpperCase(), PASSWORD);
  await expect(page).toHaveURL(/\/funds$/);
});

test('cookie phiên là HttpOnly + SameSite=Strict và không có token trong localStorage', async ({ page }) => {
  await register(page, newEmail());

  const cookies = await page.context().cookies();
  const session = cookies.find((cookie) => cookie.name === 'cf_session');
  expect(session?.httpOnly).toBe(true);
  expect(session?.sameSite).toBe('Strict');
  const stored = await page.evaluate(() => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }));
  expect(stored).not.toMatch(/token|session|password/i);
});

test('đăng ký trùng email (khác hoa thường) báo lỗi ngay ở ô Email', async ({ page, browser }) => {
  const email = newEmail();
  await register(page, email);

  const other = await newSession(browser);
  await other.goto('/register');
  await other.getByLabel('Họ và tên').fill('Người Khác');
  await other.getByLabel('Email').fill(email.toUpperCase());
  await other.getByLabel('Mật khẩu', { exact: true }).fill(PASSWORD);
  await other.getByLabel('Nhập lại mật khẩu', { exact: true }).fill(PASSWORD);
  await other.getByRole('checkbox').check();
  await other.getByRole('button', { name: 'Tạo tài khoản' }).click();

  await expect(other.getByText('Email này đã được đăng ký.')).toBeVisible();
  await expect(other).toHaveURL(/\/register$/);
});

test('backend từ chối mật khẩu yếu dù bỏ qua kiểm tra ở form', async ({ page }) => {
  await page.goto('/register');
  const token = (await (await page.request.get('/api/v1/auth/antiforgery')).json()).antiforgeryToken;

  const response = await page.request.post('/api/v1/auth/register', {
    headers: { 'X-XSRF-TOKEN': token },
    data: { fullName: 'A', email: newEmail(), password: 'abc' },
  });

  expect(response.status()).toBe(400);
  const body = await response.json();
  expect(body.code).toBe('validation_failed');
  expect(body.errors.password).toBeDefined();
});

test('đăng nhập sai: cùng một thông báo cho email lạ và mật khẩu sai', async ({ page }) => {
  const email = newEmail();
  await register(page, email);
  await page.getByRole('banner').getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page).toHaveURL(/\/login$/);

  await login(page, email, 'Sai-mat-khau-1');
  const wrongPassword = page.getByRole('alert').filter({ hasText: 'Email hoặc mật khẩu không đúng.' });
  await expect(wrongPassword).toBeVisible();

  await login(page, newEmail(), PASSWORD);
  await expect(page.getByRole('alert').filter({ hasText: 'Email hoặc mật khẩu không đúng.' })).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
});

test('sửa họ tên ở trang tài khoản được lưu vào database và còn sau khi tải lại', async ({ page }) => {
  await register(page, newEmail());
  await page.goto('/account');

  await page.getByLabel('Họ và tên').fill('Lê Cường');
  await page.getByRole('button', { name: 'Lưu thay đổi' }).click();
  await expect(page.getByText('Đã lưu thông tin cá nhân.')).toBeVisible();

  await page.reload();
  await expect(page.getByLabel('Họ và tên')).toHaveValue('Lê Cường');
  await expect(page.getByText('LC', { exact: true })).toBeVisible();
});

test('đổi mật khẩu tự nguyện: sai mật khẩu hiện tại bị từ chối nhưng vẫn đăng nhập; mật khẩu cũ hết hiệu lực sau khi đổi', async ({
  page,
  browser,
}) => {
  const email = newEmail();
  await register(page, email);
  await page.goto('/change-password');

  await page.getByLabel('Mật khẩu hiện tại', { exact: true }).fill('Sai-mat-khau-1');
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill('MatKhauMoi1');
  await page.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill('MatKhauMoi1');
  await page.getByRole('button', { name: 'Đổi mật khẩu' }).click();
  await expect(page.getByText('Mật khẩu hiện tại không đúng.')).toBeVisible();
  await expect(page).toHaveURL(/\/change-password$/);

  await page.getByLabel('Mật khẩu hiện tại', { exact: true }).fill(PASSWORD);
  await page.getByRole('button', { name: 'Đổi mật khẩu' }).click();
  await expect(page).toHaveURL(/\/funds$/);
  await expect(page.getByText('Đã đổi mật khẩu.')).toBeVisible();

  const other = await newSession(browser);
  await login(other, email, PASSWORD);
  await expect(other.getByRole('alert').filter({ hasText: 'Email hoặc mật khẩu không đúng.' })).toBeVisible();
  await login(other, email, 'MatKhauMoi1');
  await expect(other).toHaveURL(/\/funds$/);
});

test('đổi mật khẩu đăng xuất các thiết bị khác nhưng giữ thiết bị đang dùng (BR-29)', async ({ page, browser }) => {
  const email = newEmail();
  await register(page, email);

  const other = await newSession(browser);
  await login(other, email, PASSWORD);
  await expect(other).toHaveURL(/\/funds$/);

  await page.goto('/change-password');
  await page.getByLabel('Mật khẩu hiện tại', { exact: true }).fill(PASSWORD);
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill('MatKhauMoi1');
  await page.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill('MatKhauMoi1');
  await page.getByRole('button', { name: 'Đổi mật khẩu' }).click();
  await expect(page).toHaveURL(/\/funds$/);

  // Thiết bị đang dùng vẫn đăng nhập được.
  expect((await page.request.get('/api/v1/auth/me')).status()).toBe(200);
  // Thiết bị khác bị vô hiệu ngay.
  expect((await other.request.get('/api/v1/auth/me')).status()).toBe(401);
  await other.goto('/account');
  await expect(other).toHaveURL(/\/login$/);
});

test('mật khẩu tạm (tài khoản seed): bị buộc đổi, endpoint khác trả 403, đổi xong vào quỹ với đúng vai trò', async ({ page }) => {
  await login(page, SEED_STUDENT, SEED_PASSWORD);
  const alreadyChanged = page.getByRole('alert').filter({ hasText: 'Email hoặc mật khẩu không đúng.' });
  const forcedBanner = page.getByText('Trước khi đổi, bạn chỉ có thể đổi mật khẩu hoặc đăng xuất.');
  await expect(alreadyChanged.or(forcedBanner)).toBeVisible();
  test.skip(await alreadyChanged.isVisible(), 'Tài khoản seed đã đổi mật khẩu: seed lại database tạm để chạy lại.');

  await expect(page).toHaveURL(/\/change-password$/);
  await expect(page.getByRole('heading', { name: 'Đặt mật khẩu mới' })).toBeVisible();
  await page.goto('/funds');
  await expect(page).toHaveURL(/\/change-password$/);

  // Backend chặn thật: xem phiên được, endpoint khác 403 password.change_required.
  expect((await page.request.get('/api/v1/auth/me')).status()).toBe(200);
  const token = (await (await page.request.get('/api/v1/auth/antiforgery')).json()).antiforgeryToken;
  const blocked = await page.request.put('/api/v1/auth/me', { headers: { 'X-XSRF-TOKEN': token }, data: { fullName: 'X' } });
  expect(blocked.status()).toBe(403);
  expect((await blocked.json()).code).toBe('password.change_required');

  await page.getByLabel('Mật khẩu tạm', { exact: true }).fill(SEED_PASSWORD);
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill('MatKhauMoi1');
  await page.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill('MatKhauMoi1');
  await page.getByRole('button', { name: 'Lưu và vào lớp' }).click();

  // Có đúng một quỹ → vào thẳng quỹ, thấy tên quỹ và vai trò Học sinh.
  await expect(page).toHaveURL(/\/funds$/);
  await page.getByRole('link', { name: 'Mở quỹ Lớp 10A1 (demo)' }).click();
  await expect(page).toHaveURL(/\/funds\/[0-9a-f-]{36}$/);
  await expect(page.getByRole('banner').getByText('Lớp 10A1 (demo)')).toBeVisible();
  await expect(page.getByRole('banner').getByText('Học sinh', { exact: true }).first()).toBeVisible();
});

test('thủ quỹ seed: sau đăng nhập vào Quỹ của tôi (không menu), chọn quỹ mới có menu; trang tài khoản không có menu quỹ', async ({ page }) => {
  await login(page, 'thuquy.demo@classfund.local', SEED_PASSWORD);
  await expect(page).toHaveURL(/\/funds$/);
  await expect(page.getByRole('navigation', { name: 'Điều hướng chính' })).toHaveCount(0);
  await page.getByRole('link', { name: 'Mở quỹ Lớp 10A1 (demo)' }).click();
  await expect(page).toHaveURL(/\/funds\/[0-9a-f-]{36}$/);

  await expect(page.getByRole('banner').getByText('Thủ quỹ chính').first()).toBeVisible();
  await page.goto('/account');
  await expect(page.getByLabel('Họ và tên')).toHaveValue(/^Thủ quỹ Demo/);
  await page.getByRole('link', { name: 'Quỹ của tôi' }).click();
  await expect(page).toHaveURL(/\/funds$/);
});
