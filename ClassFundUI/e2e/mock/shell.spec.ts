import { expect, test } from '@playwright/test';

import { FUND, PARENT, TEACHER, TEACHER_NO_FUND, TREASURER, signInAs, user } from '../support/mock-session';

/** Khung ứng dụng (header, menu, guard) – API giả lập. */
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

test('GVCN chưa có quỹ: trang chính là /funds, mở quỹ bất kỳ → 404', async ({ page }) => {
  await signInAs(page, TEACHER_NO_FUND);
  await page.goto('/');
  await expect(page).toHaveURL(/\/funds$/);
  await page.goto(`/funds/${FUND}`);
  await expect(page.getByText('Không tìm thấy trang')).toBeVisible();
});

test('desktop: sidebar 232px bên trái, thứ tự menu GVCN theo sitemap', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await signInAs(page, TEACHER);
  await page.goto(`/funds/${FUND}`);
  const nav = page.getByRole('navigation', { name: 'Điều hướng chính' });
  const box = await nav.boundingBox();
  expect(box?.x).toBe(0);
  expect(box?.width).toBe(232);
  const y = async (name: string) => (await nav.getByRole('link', { name, exact: true }).boundingBox())?.y ?? -1;
  const labels = ['Quỹ của tôi', 'Tổng quan', 'Thu chi', 'Báo cáo', 'Học sinh & phụ huynh', 'Phân công thủ quỹ', 'Thông tin quỹ'];
  const positions = await Promise.all(labels.map(y));
  expect(positions.every((value) => value >= 0)).toBe(true);
  expect([...positions].sort((a, b) => a - b)).toEqual(positions);
});

test('cửa sổ hẹp hơn 1280px giữ layout desktop và cuộn ngang', async ({ page }) => {
  await page.setViewportSize({ width: 1024, height: 768 });
  await signInAs(page, PARENT);
  await page.goto(`/funds/${FUND}`);
  const nav = page.getByRole('navigation', { name: 'Điều hướng chính' });
  expect((await nav.boundingBox())?.width).toBe(232);
  await expect(nav.getByText('Tổng quan')).toBeVisible();
  const width = await page.evaluate(() => document.documentElement.scrollWidth);
  expect(width).toBeGreaterThanOrEqual(1280);
});
