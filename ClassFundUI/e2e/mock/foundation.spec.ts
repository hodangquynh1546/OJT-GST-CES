import { expect, test } from '@playwright/test';

/** Foundation: chưa đăng nhập → về trang đăng nhập; đăng nhập (giả lập) → shell có menu theo quyền. */
test('anonymous user is redirected to login', async ({ page }) => {
  await page.route('**/api/v1/auth/me', (route) =>
    route.fulfill({ status: 401, contentType: 'application/problem+json', body: '{"code":"unauthorized"}' }),
  );
  await page.goto('/');
  await expect(page).toHaveURL(/\/login$/);
});

test('student sees general fund and events but not management menu', async ({ page }) => {
  await page.route('**/api/v1/auth/me', (route) =>
    route.fulfill({
      json: {
        id: 'u1',
        email: 'hs@demo.local',
        fullName: 'Học Sinh',
        mustChangePassword: false,
        accountPermissions: [],
        memberships: [
          {
            fundId: 'f1',
            fundName: 'Lớp 10A1',
            role: 'Student',
            permissions: ['Fund.View', 'Event.Participate', 'Payment.Submit', 'Balance.ViewOwn', 'Balance.Withdraw'],
          },
        ],
      },
    }),
  );
  await page.goto('/');
  await expect(page).toHaveURL(/\/funds\/f1$/);
  const nav = page.getByRole('navigation', { name: 'Điều hướng chính' });
  await expect(nav.getByText('Thu chi chung')).toBeVisible();
  await expect(nav.getByText('Sự kiện')).toBeVisible();
  await expect(nav.getByText('Xác nhận thanh toán')).toHaveCount(0);
});
