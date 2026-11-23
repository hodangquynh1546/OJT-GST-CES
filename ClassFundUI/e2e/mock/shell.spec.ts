import { Page, expect, test } from '@playwright/test';

import { AN, FakeAuthApi, FakeUser } from '../support/fake-auth-api';

/** Khung sidebar trong quỹ theo canvas "ClassFund UI Design" (S-01): thẻ quỹ, menu theo nhóm, thanh trên. */

const STUDENT_PERMISSIONS = ['Fund.View', 'Event.Participate', 'Balance.ViewOwn', 'Balance.Withdraw'];
const OWNER_PERMISSIONS = [
  ...STUDENT_PERMISSIONS,
  'Fund.Manage',
  'Member.Manage',
  'Treasurer.Manage',
  'Category.Manage',
  'Event.Manage',
  'Charge.Manage',
  'Ledger.Manage',
  'Payment.Confirm',
  'Settlement.Execute',
  'Balance.Handle',
  'Report.View',
  'Audit.View',
];

function userAs(role: 'Owner' | 'Treasurer' | 'Student'): FakeUser {
  return {
    ...AN,
    fullName: 'Nguyễn Minh Anh',
    memberships: [
      {
        fundId: 'f1',
        fundName: 'Lớp 10A1',
        role,
        permissions: role === 'Student' ? STUDENT_PERMISSIONS : OWNER_PERMISSIONS,
      },
    ],
  };
}

async function open(page: Page, role: 'Owner' | 'Treasurer' | 'Student', path = '/funds/f1'): Promise<void> {
  const api = new FakeAuthApi(page);
  api.addAccount(userAs(role), 'Passw0rd1');
  api.signIn(userAs(role));
  await api.install();
  await page.goto(path);
}

test('thủ quỹ chính: thẻ quỹ ở đầu sidebar, ba nhóm menu đúng thứ tự và nhãn theo canvas', async ({ page }) => {
  await open(page, 'Owner');
  const side = page.getByRole('complementary');

  const card = side.getByRole('link', { name: /Chuyển quỹ, đang mở Lớp 10A1/ });
  await expect(card).toContainText('Lớp 10A1');
  await expect(card).toContainText('Thủ quỹ chính');
  await expect(card).toContainText('10A1');

  const nav = page.getByRole('navigation', { name: 'Điều hướng chính' });
  await expect(nav.getByRole('group')).toHaveCount(3);
  await expect(nav.getByRole('group', { name: 'Quỹ chung' }).getByRole('link')).toHaveText(['Tổng quan', 'Thu chi chung', 'Khoản thu chung', 'Sự kiện']);
  await expect(nav.getByRole('group', { name: 'Cá nhân' }).getByRole('link')).toHaveText(['Khoản cần đóng', 'Số dư cá nhân', 'Thông báo']);
  await expect(nav.getByRole('group', { name: 'Quản lý' }).getByRole('link')).toHaveText([
    'Rút số dư',
    'Báo cáo',
    'Học sinh',
    'Thủ quỹ',
    'Danh mục',
    'Nhật ký hoạt động',
    'Thông tin quỹ',
  ]);
  await expect(nav.getByRole('link', { name: 'Quỹ của tôi' })).toHaveCount(0);
});

test('học sinh chỉ có hai nhóm đầu, không có nhóm Quản lý', async ({ page }) => {
  await open(page, 'Student');
  const nav = page.getByRole('navigation', { name: 'Điều hướng chính' });

  await expect(nav.getByRole('group')).toHaveCount(2);
  await expect(nav.getByRole('group', { name: 'Quản lý' })).toHaveCount(0);
  await expect(nav.getByRole('link', { name: 'Rút số dư' })).toHaveCount(0);
  await expect(page.getByRole('complementary')).toContainText('Học sinh');
});

test('mục đang mở được đánh dấu; thanh trên có breadcrumb, chuông, nhãn vai trò và ảnh đại diện', async ({ page }) => {
  await open(page, 'Owner', '/funds/f1/members');
  const nav = page.getByRole('navigation', { name: 'Điều hướng chính' });

  await expect(nav.getByRole('link', { name: 'Học sinh' })).toHaveAttribute('aria-current', 'page');
  await expect(nav.getByRole('link', { name: 'Tổng quan' })).not.toHaveAttribute('aria-current', 'page');

  const top = page.getByRole('banner');
  const crumbs = top.getByRole('navigation', { name: 'Vị trí hiện tại' });
  await expect(crumbs).toContainText('Lớp 10A1');
  await expect(crumbs.getByRole('link', { name: 'Lớp 10A1' })).toHaveAttribute('href', '/funds/f1');
  await expect(crumbs.locator('[aria-current="page"]')).toHaveText('Học sinh');
  await expect(top.getByRole('link', { name: 'Thông báo' })).toHaveAttribute('href', '/funds/f1/notifications');
  await expect(top.getByText('Thủ quỹ chính')).toBeVisible();
  await expect(top.getByRole('link', { name: 'Tài khoản của Nguyễn Minh Anh' })).toContainText('MA');
});

test('breadcrumb ở trang chủ quỹ là "Tổng quan"', async ({ page }) => {
  await open(page, 'Student');

  await expect(page.getByRole('banner').locator('[aria-current="page"]')).toHaveText('Tổng quan');
});

test('thẻ quỹ chuyển về Quỹ của tôi; ảnh đại diện sang Tài khoản; trong khung không còn nút Đăng xuất', async ({ page }) => {
  await open(page, 'Owner');
  await expect(page.getByRole('button', { name: 'Đăng xuất' })).toHaveCount(0);

  await page.getByRole('link', { name: /Chuyển quỹ/ }).click();
  await expect(page).toHaveURL(/\/funds$/);
  await expect(page.getByRole('navigation', { name: 'Điều hướng chính' })).toHaveCount(0);

  await page.getByRole('link', { name: 'Mở quỹ Lớp 10A1' }).click();
  await page.getByRole('banner').getByRole('link', { name: /Tài khoản của/ }).click();
  await expect(page).toHaveURL(/\/account$/);
});

test('trang không lộ mã màn hình của tài liệu thiết kế (S-01, M-01…)', async ({ page }) => {
  // Mọi màn đã làm xong; kiểm tra trên S-05 (API chưa giả lập nên hiện trạng thái lỗi, vẫn không có mã màn).
  await open(page, 'Owner', '/funds/f1/reports/year-end');

  await expect(page.getByRole('heading', { name: 'Tổng kết cuối năm' })).toBeVisible();
  await expect(page.getByText(/Màn hình/)).toHaveCount(0);
  await expect(page.getByText(/\b[A-Z]-\d{2}\b/)).toHaveCount(0);
});
