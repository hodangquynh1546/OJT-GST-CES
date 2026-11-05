import { Page, expect, test } from '@playwright/test';

const FUND = '019a0000-0000-7000-8000-000000000001';

function user(kind: string, role: string, permissions: string[], mustChangePassword = false) {
  return {
    id: 'u1',
    kind,
    fullName: 'Nguyễn Minh Khang',
    mustChangePassword,
    accountPermissions: [],
    memberships: [{ fundId: FUND, fundName: 'Quỹ lớp 5A', role, permissions }],
  };
}

const TEACHER = user('Teacher', 'Teacher', [
  'Fund.View',
  'Ledger.ViewFull',
  'Fund.Manage',
  'Student.Manage',
  'Treasurer.Assign',
  'Member.View',
]);
const TREASURER = user('Student', 'Treasurer', [
  'Fund.View',
  'Ledger.ViewFull',
  'Member.View',
  'Transaction.Manage',
  'Category.Manage',
]);
const PARENT = user('Parent', 'Parent', ['Fund.View', 'Ledger.ViewPublic']);

/** Backend Auth chưa triển khai nên giả lập GET /auth/me. */
async function signInAs(page: Page, me: object | null) {
  await page.route('**/api/v1/auth/me', (route) =>
    me
      ? route.fulfill({ json: me })
      : route.fulfill({ status: 401, contentType: 'application/problem+json', json: { code: 'unauthorized' } }),
  );
}

test('chưa đăng nhập bị chuyển về trang đăng nhập', async ({ page }) => {
  await signInAs(page, null);
  await page.goto(`/funds/${FUND}`);
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole('heading', { name: 'Đăng nhập' })).toBeVisible();
});

test('mật khẩu tạm bị chuyển sang đặt mật khẩu mới', async ({ page }) => {
  await signInAs(page, user('Student', 'Student', ['Fund.View'], true));
  await page.goto('/');
  await expect(page).toHaveURL(/\/change-password$/);
});

test('GVCN thấy menu quản lý và nhãn vai trò', async ({ page }) => {
  await signInAs(page, TEACHER);
  await page.goto(`/funds/${FUND}`);
  const nav = page.getByRole('navigation', { name: 'Điều hướng chính' });
  await expect(nav.getByRole('link', { name: 'Học sinh & phụ huynh' })).toBeVisible();
  await expect(nav.getByRole('link', { name: 'Danh mục' })).toHaveCount(0);
  await expect(page.getByText('GVCN', { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: '· Đổi quỹ ▾' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Tổng quan' })).toBeVisible();
});

test('học sinh được chuyển thẳng vào quỹ, thủ quỹ không có Đổi quỹ', async ({ page }) => {
  await signInAs(page, TREASURER);
  await page.goto('/');
  await expect(page).toHaveURL(new RegExp(`/funds/${FUND}$`));
  await expect(page.getByRole('link', { name: 'Danh mục' })).toBeVisible();
  await expect(page.getByRole('link', { name: '· Đổi quỹ ▾' })).toHaveCount(0);
});

test('điều hướng giữa các mục và đánh dấu mục đang mở', async ({ page }) => {
  await signInAs(page, TREASURER);
  await page.goto(`/funds/${FUND}`);
  await page.getByRole('link', { name: 'Báo cáo' }).click();
  await expect(page).toHaveURL(/\/reports$/);
  await expect(page.getByRole('link', { name: 'Báo cáo' })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByRole('heading', { name: 'Báo cáo' })).toBeVisible();
});

test('thiếu quyền vào trang 403', async ({ page }) => {
  await signInAs(page, PARENT);
  await page.goto(`/funds/${FUND}/students`);
  await expect(page).toHaveURL(/\/forbidden$/);
  await expect(page.getByText('Trang này không dành cho vai trò của bạn')).toBeVisible();
});

test('quỹ không thuộc mình vào trang 404', async ({ page }) => {
  await signInAs(page, TEACHER);
  await page.goto('/funds/019a0000-0000-7000-8000-00000000ffff');
  await expect(page.getByText('Không tìm thấy trang')).toBeVisible();
});

test('thanh dưới của GVCN theo thứ tự Quỹ, Học sinh, Thủ quỹ, Thu chi', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 780 });
  await signInAs(page, TEACHER);
  await page.goto(`/funds/${FUND}`);
  const nav = page.getByRole('navigation', { name: 'Điều hướng chính' });
  const x = async (name: string) => (await nav.getByRole('link', { name, exact: true }).boundingBox())?.x ?? -1;
  const positions = [await x('Quỹ'), await x('Học sinh'), await x('Thủ quỹ'), await x('Thu chi'), await x('Tài khoản')];
  expect(positions.every((value) => value >= 0)).toBe(true);
  expect([...positions].sort((a, b) => a - b)).toEqual(positions);
});

test('mobile dùng thanh dưới, không tràn ngang', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 780 });
  await signInAs(page, PARENT);
  await page.goto(`/funds/${FUND}`);
  const nav = page.getByRole('navigation', { name: 'Điều hướng chính' });
  await expect(nav.getByRole('link', { name: 'Tài khoản' })).toBeVisible();
  const box = await nav.boundingBox();
  expect(box?.y).toBeGreaterThan(700);
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
  expect(overflow).toBe(false);
});
