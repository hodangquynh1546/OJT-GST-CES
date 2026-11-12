import { Browser, Page, expect, test } from '@playwright/test';

import { RealApi, RealStudent, createActiveStudent, createStudent, isFullE2E, registerTeacher, unique } from '../support/real-api';

/**
 * M5 – luồng thật FE + BE + PostgreSQL (module-plan.md M5; FR-10, FR-12, BR-08, BR-23):
 * thủ quỹ xem danh mục mặc định, thêm / đổi tên / ẩn / hiện lại; GVCN không có menu Danh mục; Thành viên chỉ họ tên + vai trò.
 * Học sinh seed bằng API của M3, phân công thủ quỹ bằng API của M4.
 */
test.skip(!isFullE2E, 'Chỉ chạy khi E2E_FULL=1 (cần backend + DB, xem docs/architecture/foundation-guide.md)');

/** Học sinh đăng nhập trong một context trình duyệt riêng (phiên của GVCN ở `page` không bị đổi) và mở trang của quỹ. */
async function openAs(browser: Browser, student: RealStudent, path: string): Promise<Page> {
  const context = await browser.newContext({ baseURL: 'http://localhost:4200' });
  const page = await context.newPage();
  await page.goto('/login');
  await page.getByLabel('Email hoặc tên đăng nhập').fill(student.login);
  await page.getByLabel('Mật khẩu').fill(student.password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page).not.toHaveURL(/\/login$/);
  await page.goto(path);
  return page;
}

const nav = (page: Page) => page.getByRole('navigation', { name: 'Điều hướng chính' });
const group = (page: Page, title: string) => page.getByRole('region', { name: title });

test('thủ quỹ quản lý danh mục: mặc định → thêm → trùng tên bị chặn → đổi tên → ẩn / hiện lại; GVCN không có menu Danh mục', async ({ page, browser }) => {
  const api = new RealApi(page.request);
  await registerTeacher(api, 'Thầy Hùng M5');
  const { id: fundId } = await api.post<{ id: string }>('/funds', { name: unique('Quỹ lớp 5E ') });
  const khang = await createActiveStudent(api, fundId, 'Nguyễn Minh Khang');
  await api.post(`/funds/${fundId}/treasurers`, { studentId: khang.studentId });

  // GVCN: menu không có Danh mục (chỉ thủ quỹ quản lý) và vào thẳng đường dẫn bị chặn.
  await page.goto(`/funds/${fundId}/students`);
  await expect(nav(page).getByRole('link', { name: 'Danh mục' })).toBeHidden();
  await page.goto(`/funds/${fundId}/categories`);
  await expect(page).toHaveURL(/\/forbidden$/);

  const treasurer = await openAs(browser, khang, `/funds/${fundId}/categories`);
  await expect(treasurer.getByRole('heading', { name: 'Danh mục thu chi', level: 1 })).toBeVisible();
  const income = group(treasurer, 'Danh mục thu');
  const expense = group(treasurer, 'Danh mục chi');

  // 1. Quỹ mới có 6 danh mục mặc định (2 thu, 4 chi), chưa dùng.
  await expect(income.getByText('2 đang dùng · 0 đã ẩn')).toBeVisible();
  await expect(expense.getByText('4 đang dùng · 0 đã ẩn')).toBeVisible();
  await expect(income.getByText('Đóng quỹ')).toBeVisible();
  await expect(expense.getByText('Văn phòng phẩm')).toBeVisible();

  // 2. Thêm danh mục chi; tên trùng (không phân biệt hoa thường) bị chặn ngay ở form.
  await expense.getByLabel('Tên danh mục chi mới').fill('Quà 20/11');
  await expense.getByRole('button', { name: '+ Thêm' }).click();
  await expect(treasurer.getByText('Đã thêm danh mục “Quà 20/11”.')).toBeVisible();
  await expect(expense.getByText('5 đang dùng · 0 đã ẩn')).toBeVisible();
  await expense.getByLabel('Tên danh mục chi mới').fill('quà 20/11');
  await expense.getByRole('button', { name: '+ Thêm' }).click();
  await expect(expense.getByRole('alert')).toHaveText('Đã có danh mục chi tên “quà 20/11”.');

  // 3. Đổi tên rồi ẩn; tải lại vẫn đúng (dữ liệu nằm ở DB).
  await expense.getByRole('button', { name: 'Đổi tên Quà 20/11' }).click();
  await expense.getByLabel('Tên mới cho Quà 20/11').fill('Quà 20/11 – tặng cô');
  await expense.getByRole('button', { name: 'Lưu' }).click();
  await expect(treasurer.getByText('Đã đổi tên danh mục thành “Quà 20/11 – tặng cô”.')).toBeVisible();
  await expense.getByRole('button', { name: 'Ẩn Chi khác' }).click();
  await expect(treasurer.getByText('Đã ẩn “Chi khác”.')).toBeVisible();
  await expect(expense.getByText('4 đang dùng · 1 đã ẩn')).toBeVisible();

  await treasurer.reload();
  await expect(group(treasurer, 'Danh mục chi').getByText('4 đang dùng · 1 đã ẩn')).toBeVisible();
  await expect(group(treasurer, 'Danh mục chi').getByText('Quà 20/11 – tặng cô')).toBeVisible();
  await expect(group(treasurer, 'Danh mục chi').getByText('Đã ẩn', { exact: true })).toBeVisible();

  // 4. Hiện lại; đổi tên sang tên của danh mục khác bị chặn.
  await group(treasurer, 'Danh mục chi').getByRole('button', { name: 'Hiện lại Chi khác' }).click();
  await expect(treasurer.getByText('Đã hiện lại “Chi khác”.')).toBeVisible();
  await expect(group(treasurer, 'Danh mục chi').getByText('5 đang dùng · 0 đã ẩn')).toBeVisible();
  await group(treasurer, 'Danh mục chi').getByRole('button', { name: 'Đổi tên Khen thưởng' }).click();
  await group(treasurer, 'Danh mục chi').getByLabel('Tên mới cho Khen thưởng').fill('CHI KHÁC');
  await group(treasurer, 'Danh mục chi').getByRole('button', { name: 'Lưu' }).click();
  await expect(group(treasurer, 'Danh mục chi').getByRole('alert')).toHaveText('Đã có danh mục chi tên “CHI KHÁC”.');
  await treasurer.context().close();
});

test('Thành viên: GVCN và thủ quỹ xem họ tên + vai trò (không email, tên đăng nhập); học sinh bị chặn', async ({ page, browser }) => {
  const api = new RealApi(page.request);
  await registerTeacher(api, 'Thầy Hùng M5');
  const { id: fundId } = await api.post<{ id: string }>('/funds', { name: unique('Quỹ lớp 5F ') });
  const khang = await createActiveStudent(api, fundId, 'Nguyễn Minh Khang');
  const trang = await createActiveStudent(api, fundId, 'Phạm Thu Trang');
  await createStudent(api, fundId, 'Lê Gia Bảo');
  await api.post(`/funds/${fundId}/treasurers`, { studentId: khang.studentId });

  // GVCN có Member.View: menu không có mục này nhưng vào đường dẫn vẫn xem được.
  await page.goto(`/funds/${fundId}/members`);
  await expect(page.getByRole('heading', { name: 'Thành viên quỹ', level: 1 })).toBeVisible();
  const list = page.getByRole('region', { name: 'Danh sách thành viên' });
  await expect(list.locator('.row')).toHaveCount(4);
  await expect(list.locator('.row').nth(0)).toContainText('Thầy Hùng M5');
  await expect(list.locator('.row').nth(0)).toContainText('GVCN');
  await expect(list.locator('.row').nth(1)).toContainText('Nguyễn Minh Khang');
  await expect(list.locator('.row').nth(1)).toContainText('Thủ quỹ');
  await expect(list.getByText('Lê Gia Bảo')).toBeVisible();
  await expect(list.getByText('@')).toHaveCount(0);
  await expect(list.getByText(khang.login)).toHaveCount(0);

  await page.getByRole('radio', { name: 'Học sinh (2)' }).click();
  await expect(list.locator('.row')).toHaveCount(2);

  // Thủ quỹ cũng xem được; học sinh thường bị chặn.
  const treasurer = await openAs(browser, khang, `/funds/${fundId}/members`);
  await expect(nav(treasurer).getByRole('link', { name: 'Thành viên' })).toBeVisible();
  await expect(treasurer.getByRole('radio', { name: 'Tất cả (4)' })).toBeVisible();
  await treasurer.context().close();

  const student = await openAs(browser, trang, `/funds/${fundId}/members`);
  await expect(student).toHaveURL(/\/forbidden$/);
  await student.context().close();
});
