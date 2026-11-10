import { expect, test } from '@playwright/test';

import { E2E_PASSWORD, RealApi, isFullE2E, login, registerTeacher, unique } from '../support/real-api';

/** M1 – luồng thật FE + BE + PostgreSQL: đăng ký GVCN → đăng xuất → đăng nhập → đổi tên, đổi mật khẩu. */
test.skip(!isFullE2E, 'Chỉ chạy khi E2E_FULL=1 (cần backend + DB, xem docs/architecture/foundation-guide.md)');

test('GVCN đăng ký trên UI → Quỹ của tôi → đăng xuất → đăng nhập lại bằng email (chữ hoa)', async ({ page }) => {
  const email = `${unique('gv')}@example.vn`;

  await page.goto('/register-teacher');
  await page.getByLabel('Họ tên *').fill('Cô Hạnh E2E');
  await page.getByLabel('Email *').fill(email);
  await page.getByLabel('Mật khẩu *', { exact: true }).fill(E2E_PASSWORD);
  await page.getByLabel('Xác nhận mật khẩu *').fill(E2E_PASSWORD);
  await page.getByRole('button', { name: 'Đăng ký', exact: true }).click();
  await expect(page).toHaveURL(/\/funds$/);
  await expect(page.locator('app-header')).toContainText('Cô Hạnh E2E');

  await page.goto('/account');
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto('/funds');
  await expect(page).toHaveURL(/\/login$/);

  await page.getByLabel('Email hoặc tên đăng nhập').fill(email.toUpperCase());
  await page.getByLabel('Mật khẩu').fill('SaiMatKhau1');
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page.locator('.cf-alert')).toContainText('Email / tên đăng nhập hoặc mật khẩu không đúng.');

  await page.getByLabel('Mật khẩu').fill(E2E_PASSWORD);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page).toHaveURL(/\/funds$/);
});

test('email đã đăng ký → lỗi ở ô Email', async ({ page, request }) => {
  const teacher = await registerTeacher(new RealApi(request));

  await page.goto('/register-teacher');
  await page.getByLabel('Họ tên *').fill('Người khác');
  await page.getByLabel('Email *').fill(teacher.login);
  await page.getByLabel('Mật khẩu *', { exact: true }).fill(E2E_PASSWORD);
  await page.getByLabel('Xác nhận mật khẩu *').fill(E2E_PASSWORD);
  await page.getByRole('button', { name: 'Đăng ký', exact: true }).click();
  await expect(page.getByText('Email này đã được dùng cho một tài khoản khác.')).toBeVisible();
});

test('A-04: sửa họ tên, đổi mật khẩu → phiên khác bị đăng xuất, mật khẩu cũ hết dùng được', async ({ page, request, browser }) => {
  const teacher = await registerTeacher(new RealApi(request), 'Thầy Hùng');

  // Thiết bị khác đang đăng nhập cùng tài khoản.
  const other = await browser.newContext({ baseURL: 'http://localhost:4200' });
  const otherApi = new RealApi(other.request);
  await login(otherApi, teacher);
  expect((await otherApi.get<{ fullName: string }>('/auth/me')).fullName).toBe('Thầy Hùng');

  await page.goto('/login');
  await page.getByLabel('Email hoặc tên đăng nhập').fill(teacher.login);
  await page.getByLabel('Mật khẩu').fill(teacher.password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page).toHaveURL(/\/funds$/);

  await page.goto('/account');
  await page.getByRole('button', { name: 'Sửa họ tên' }).click();
  await page.getByLabel('Họ tên mới').fill('Trần Văn Hùng');
  await page.getByRole('button', { name: 'Lưu họ tên' }).click();
  await expect(page.locator('app-header')).toContainText('Trần Văn Hùng');

  await page.getByLabel('Mật khẩu hiện tại').fill('SaiMatKhau1');
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill('MatKhauMoi2');
  await page.getByLabel('Nhập lại mật khẩu mới').fill('MatKhauMoi2');
  await page.getByRole('button', { name: 'Đổi mật khẩu' }).click();
  await expect(page.getByText('Mật khẩu hiện tại không đúng.')).toBeVisible();

  await page.getByLabel('Mật khẩu hiện tại').fill(teacher.password);
  await page.getByRole('button', { name: 'Đổi mật khẩu' }).click();
  await expect(page.getByText('Đã đổi mật khẩu. Các thiết bị khác đã bị đăng xuất.')).toBeVisible();

  expect((await other.request.get('/api/v1/auth/me')).status()).toBe(401);
  await other.close();

  // Phiên hiện tại vẫn dùng được; mật khẩu cũ không đăng nhập được nữa.
  await page.reload();
  await expect(page.locator('app-header')).toContainText('Trần Văn Hùng');
  const fresh = new RealApi(request);
  const old = await request.fetch('/api/v1/auth/login', {
    method: 'POST',
    data: { login: teacher.login, password: teacher.password },
    headers: { 'X-XSRF-TOKEN': (await fresh.get<{ antiforgeryToken: string }>('/auth/antiforgery')).antiforgeryToken },
  });
  expect(old.status()).toBe(401);
  await login(fresh, { login: teacher.login, password: 'MatKhauMoi2' });
});
