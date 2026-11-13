import { Page, expect, test } from '@playwright/test';

import { mockOverviewPage } from '../support/mock-overview';
import { FUND, PARENT, TEACHER, TEACHER_NO_FUND, mockProblem, signInAs, user } from '../support/mock-session';

/** M1 – Auth & Tài khoản (A-01 → A-04) với API giả lập. */
const session = (me: object) => ({ antiforgeryToken: 'tok-user', user: me });

async function mockAntiforgery(page: Page): Promise<void> {
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ json: { antiforgeryToken: 'tok-anon' } }));
}

test.beforeEach(async ({ page }) => {
  await mockAntiforgery(page);
  await mockOverviewPage(page); // đăng nhập xong vào S-01 (M7a)
});

test.describe('A-02 Đăng nhập', () => {
  test('một ô email / tên đăng nhập, không chọn vai trò; đúng → trang chính theo /auth/me', async ({ page }) => {
    await signInAs(page, null);
    let body: unknown;
    await page.route('**/api/v1/auth/login', async (route) => {
      body = route.request().postDataJSON();
      await route.fulfill({ json: session(TEACHER_NO_FUND) });
    });

    await page.goto('/login');
    await expect(page.getByRole('combobox')).toHaveCount(0);
    await expect(page.getByRole('radio')).toHaveCount(0);
    await page.getByLabel('Email hoặc tên đăng nhập').fill('co.hanh@example.vn');
    await page.getByLabel('Mật khẩu').fill('Matkhau123');
    await page.getByRole('button', { name: 'Đăng nhập' }).click();

    await expect(page).toHaveURL(/\/funds$/);
    expect(body).toEqual({ login: 'co.hanh@example.vn', password: 'Matkhau123' });
  });

  test('bỏ trống → lỗi từng ô sau khi bấm Đăng nhập, không gọi API', async ({ page }) => {
    await signInAs(page, null);
    let called = false;
    await page.route('**/api/v1/auth/login', (route) => {
      called = true;
      return route.abort();
    });
    await page.goto('/login');
    await expect(page.getByText('Nhập mật khẩu.')).toHaveCount(0);
    await page.getByRole('button', { name: 'Đăng nhập' }).click();
    await expect(page.getByText('Nhập email hoặc tên đăng nhập.')).toBeVisible();
    await expect(page.getByText('Nhập mật khẩu.')).toBeVisible();
    expect(called).toBe(false);
  });

  for (const [code, status, text] of [
    ['auth.invalid_credentials', 401, 'Email / tên đăng nhập hoặc mật khẩu không đúng.'],
    ['auth.account_disabled', 401, 'Tài khoản của bạn đang bị khóa.'],
    ['auth.rate_limited', 429, 'Bạn đã thử quá nhiều lần. Vui lòng chờ 1 phút rồi thử lại.'],
  ] as const) {
    test(`lỗi ${code} hiện thông báo đúng và vẫn ở trang đăng nhập`, async ({ page }) => {
      await signInAs(page, null);
      await mockProblem(page, '**/api/v1/auth/login', status, code);
      await page.goto('/login');
      await page.getByLabel('Email hoặc tên đăng nhập').fill('khang.nguyen.204');
      await page.getByLabel('Mật khẩu').fill('SaiMatKhau1');
      await page.getByRole('button', { name: 'Đăng nhập' }).click();
      await expect(page.locator('.cf-alert')).toContainText(text);
      await expect(page.getByText('Nhập mật khẩu.')).toHaveCount(0);
      await expect(page).toHaveURL(/\/login$/);
      await expect(page.getByLabel('Mật khẩu')).toHaveValue('');
    });
  }

  test('link sang đăng ký giáo viên', async ({ page }) => {
    await signInAs(page, null);
    await page.goto('/login');
    await page.getByRole('link', { name: 'Đăng ký tài khoản giáo viên' }).click();
    await expect(page).toHaveURL(/\/register-teacher$/);
  });
});

test.describe('A-01 Đăng ký giáo viên', () => {
  test('kiểm tra ô sau khi bấm Tạo tài khoản: mật khẩu yếu, xác nhận không khớp', async ({ page }) => {
    await signInAs(page, null);
    await page.goto('/register-teacher');
    await page.getByLabel('Họ tên *').fill('Cô Hạnh');
    await page.getByLabel('Email *').fill('co.hanh@example.vn');
    await page.getByLabel('Mật khẩu *', { exact: true }).fill('chicochu');
    await page.getByLabel('Xác nhận mật khẩu *').fill('khac');
    await page.getByRole('button', { name: 'Đăng ký', exact: true }).click();

    await expect(page.locator('.cf-alert')).toContainText('Kiểm tra các ô được đánh dấu');
    await expect(page.getByText('Mật khẩu cần 8–128 ký tự, có cả chữ và số.')).toBeVisible();
    await expect(page.getByText('Mật khẩu xác nhận không khớp.')).toBeVisible();
  });

  test('email đã dùng → lỗi gắn vào ô Email', async ({ page }) => {
    await signInAs(page, null);
    await mockProblem(page, '**/api/v1/auth/register-teacher', 422, 'user.email_taken');
    await page.goto('/register-teacher');
    await page.getByLabel('Họ tên *').fill('Cô Hạnh');
    await page.getByLabel('Email *').fill('co.hanh@example.vn');
    await page.getByLabel('Mật khẩu *', { exact: true }).fill('Matkhau123');
    await page.getByLabel('Xác nhận mật khẩu *').fill('Matkhau123');
    await page.getByRole('button', { name: 'Đăng ký', exact: true }).click();
    await expect(page.getByText('Email này đã được dùng cho một tài khoản khác.')).toBeVisible();
    await expect(page).toHaveURL(/\/register-teacher$/);
  });

  test('thành công → đăng nhập luôn → Quỹ của tôi; body không có vai trò', async ({ page }) => {
    await signInAs(page, null);
    let body: Record<string, unknown> = {};
    await page.route('**/api/v1/auth/register-teacher', async (route) => {
      body = route.request().postDataJSON();
      await route.fulfill({ status: 201, json: session(TEACHER_NO_FUND) });
    });
    await page.goto('/register-teacher');
    await expect(page.getByRole('note')).toContainText('luôn là Giáo viên');
    await page.getByLabel('Họ tên *').fill('  Cô Hạnh  ');
    await page.getByLabel('Email *').fill('co.hanh@example.vn');
    await page.getByLabel('Mật khẩu *', { exact: true }).fill('Matkhau123');
    await page.getByLabel('Xác nhận mật khẩu *').fill('Matkhau123');
    await page.getByRole('button', { name: 'Đăng ký', exact: true }).click();

    await expect(page).toHaveURL(/\/funds$/);
    expect(Object.keys(body).sort()).toEqual(['confirmPassword', 'email', 'fullName', 'password']);
    expect(body['fullName']).toBe('Cô Hạnh');
  });
});

test.describe('A-03 Đặt mật khẩu mới', () => {
  const temp = user('Student', 'Student', ['Fund.View', 'Ledger.ViewPublic'], true);

  test('sai mật khẩu tạm → lỗi ở ô Mật khẩu tạm; đúng → vào Tổng quan quỹ', async ({ page }) => {
    await signInAs(page, temp);
    let attempt = 0;
    await page.route('**/api/v1/auth/change-password', (route) =>
      ++attempt === 1
        ? route.fulfill({ status: 422, contentType: 'application/problem+json', json: { status: 422, code: 'auth.wrong_password' } })
        : route.fulfill({ json: session({ ...temp, mustChangePassword: false }) }),
    );

    await page.goto(`/funds/${FUND}`);
    await expect(page).toHaveURL(/\/change-password$/);
    await expect(page.getByRole('status')).toContainText('mật khẩu tạm do giáo viên chủ nhiệm cấp');

    await page.getByLabel('Mật khẩu tạm').fill('Sai-0000');
    await page.getByLabel('Mật khẩu mới', { exact: true }).fill('MatKhauMoi1');
    await page.getByLabel('Nhập lại mật khẩu mới').fill('MatKhauMoi1');
    await page.getByRole('button', { name: 'Lưu và tiếp tục' }).click();
    await expect(page.getByText('Mật khẩu hiện tại không đúng.')).toBeVisible();

    await page.getByLabel('Mật khẩu tạm').fill('Tam-1234-abcd-5678');
    await page.getByRole('button', { name: 'Lưu và tiếp tục' }).click();
    await expect(page).toHaveURL(new RegExp(`/funds/${FUND}$`));
  });

  test('mật khẩu mới trùng mật khẩu tạm bị chặn ở client', async ({ page }) => {
    await signInAs(page, temp);
    await page.goto('/change-password');
    await page.getByLabel('Mật khẩu tạm').fill('Tam12345');
    await page.getByLabel('Mật khẩu mới', { exact: true }).fill('Tam12345');
    await page.getByLabel('Nhập lại mật khẩu mới').fill('Tam12345');
    await page.getByRole('button', { name: 'Lưu và tiếp tục' }).click();
    await expect(page.getByText('Mật khẩu mới phải khác mật khẩu hiện tại.')).toBeVisible();
  });

  test('Đăng xuất → trang đăng nhập', async ({ page }) => {
    await signInAs(page, temp);
    await page.route('**/api/v1/auth/logout', (route) => route.fulfill({ status: 204 }));
    await page.goto('/change-password');
    await page.getByRole('button', { name: 'Đăng xuất' }).click();
    await expect(page).toHaveURL(/\/login$/);
  });
});

test.describe('A-04 Tài khoản', () => {
  test('GVCN: thông tin, sửa họ tên', async ({ page }) => {
    const teacher = { ...TEACHER, email: 'hung.tran@example.com' };
    await signInAs(page, teacher);
    await page.route('**/api/v1/auth/me', (route) =>
      route.request().method() === 'PUT'
        ? route.fulfill({ json: { ...teacher, fullName: route.request().postDataJSON().fullName } })
        : route.fulfill({ json: teacher }),
    );
    await page.goto('/account');
    await expect(page.getByRole('heading', { name: 'Tài khoản', level: 1 })).toBeVisible();
    await expect(page.getByText('hung.tran@example.com')).toBeVisible();
    await expect(page.getByText('Giáo viên (GVCN của quỹ mình tạo)')).toBeVisible();

    await page.getByRole('button', { name: 'Sửa họ tên' }).click();
    await page.getByLabel('Họ tên mới').fill('Trần Văn Hùng');
    await page.getByRole('button', { name: 'Lưu họ tên' }).click();
    await expect(page.locator('dd').first()).toHaveText('Trần Văn Hùng');
    await expect(page.locator('app-header')).toContainText('Trần Văn Hùng');
  });

  test('phụ huynh: con được liên kết; đổi mật khẩu báo phiên khác bị đăng xuất', async ({ page }) => {
    await signInAs(page, PARENT);
    await page.route('**/api/v1/auth/change-password', (route) => route.fulfill({ json: session(PARENT) }));
    await page.goto('/account');
    await expect(page.getByText('Phạm Gia Huy (Quỹ lớp 5A)')).toBeVisible();

    await page.getByLabel('Mật khẩu hiện tại').fill('Matkhau123');
    await page.getByLabel('Mật khẩu mới', { exact: true }).fill('MatKhauMoi1');
    await page.getByLabel('Nhập lại mật khẩu mới').fill('MatKhauMoi1');
    await page.getByRole('button', { name: 'Đổi mật khẩu' }).click();
    await expect(page.getByText('Đã đổi mật khẩu. Các thiết bị khác đã bị đăng xuất.')).toBeVisible();
  });

  test('Đăng xuất → trang đăng nhập', async ({ page }) => {
    await signInAs(page, TEACHER);
    await page.route('**/api/v1/auth/logout', (route) => route.fulfill({ status: 204 }));
    await page.goto('/account');
    await page.getByRole('button', { name: 'Đăng xuất' }).click();
    await expect(page).toHaveURL(/\/login$/);
  });
});
