import { Page, expect, test } from '@playwright/test';

import { CATEGORIES_FULL, MockCategory } from '../support/mock-categories';
import { FUND, STUDENT, TEACHER, TREASURER, signInAs } from '../support/mock-session';

/** M5 – T-02 Danh mục thu chi và T-03 Thành viên (API giả lập; Canvas boards `FundSettings` (thủ quỹ) và `Members`; FR-10, FR-12, BR-08). */

interface Calls {
  posts: unknown[];
  puts: { id: string; body: unknown }[];
}

/** GET (lọc `includeHidden`) / POST / PUT có trạng thái: ghi xong danh sách phản ánh đúng như backend. */
async function mockCategoryApi(page: Page, categories: MockCategory[] = structuredClone(CATEGORIES_FULL)): Promise<Calls> {
  const calls: Calls = { posts: [], puts: [] };
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ json: { antiforgeryToken: 'token' } }));
  await page.route(new RegExp(`/api/v1/funds/${FUND}/categories(\\?.*)?$`), async (route) => {
    const request = route.request();
    if (request.method() === 'POST') {
      const body = request.postDataJSON() as { type: MockCategory['type']; name: string };
      calls.posts.push(body);
      const created: MockCategory = { id: `new-${categories.length}`, ...body, isHidden: false, transactionCount: 0 };
      categories.push(created);
      return route.fulfill({ status: 201, json: { id: created.id } });
    }
    const url = new URL(request.url());
    const includeHidden = url.searchParams.get('includeHidden') === 'true';
    return route.fulfill({ json: categories.filter((item) => includeHidden || !item.isHidden) });
  });
  await page.route(new RegExp(`/api/v1/funds/${FUND}/categories/[^/?]+$`), async (route) => {
    const id = route.request().url().split('/').at(-1)!;
    const body = route.request().postDataJSON() as { name: string; isHidden: boolean };
    calls.puts.push({ id, body });
    Object.assign(categories.find((item) => item.id === id)!, body);
    return route.fulfill({ status: 204 });
  });
  return calls;
}

const group = (page: Page, title: string) => page.getByRole('region', { name: title });

test('thủ quỹ thấy hai nhóm thu / chi với số khoản, danh mục đã ẩn có tag và nút "Hiện lại"', async ({ page }) => {
  await signInAs(page, TREASURER);
  await mockCategoryApi(page);
  await page.goto(`/funds/${FUND}/categories`);

  await expect(page.getByRole('heading', { name: 'Danh mục thu chi', level: 1 })).toBeVisible();
  const income = group(page, 'Danh mục thu');
  await expect(income.getByText('2 đang dùng · 0 đã ẩn')).toBeVisible();
  await expect(income.getByText('4 khoản')).toBeVisible();
  const expense = group(page, 'Danh mục chi');
  await expect(expense.getByText('4 đang dùng · 1 đã ẩn')).toBeVisible();
  await expect(expense.getByText('Đã ẩn', { exact: true })).toBeVisible();
  await expect(expense.getByRole('button', { name: 'Hiện lại Photo tài liệu' })).toBeVisible();
  await expect(expense.getByRole('button', { name: 'Ẩn Văn phòng phẩm' })).toBeVisible();
});

test('thêm danh mục: tên trống / trùng (kể cả danh mục ẩn) báo lỗi, tên hợp lệ thêm vào đúng nhóm', async ({ page }) => {
  await signInAs(page, TREASURER);
  const calls = await mockCategoryApi(page);
  await page.goto(`/funds/${FUND}/categories`);
  const expense = group(page, 'Danh mục chi');

  await expense.getByRole('button', { name: '+ Thêm' }).click();
  await expect(expense.getByRole('alert')).toHaveText('Nhập tên danh mục.');

  await expense.getByLabel('Tên danh mục chi mới').fill('photo TÀI LIỆU');
  await expect(expense.getByRole('alert')).toHaveCount(0);
  await expense.getByRole('button', { name: '+ Thêm' }).click();
  await expect(expense.getByRole('alert')).toHaveText('Đã có danh mục chi tên “photo TÀI LIỆU”.');
  expect(calls.posts).toEqual([]);

  // Cùng tên ở loại thu thì được.
  await group(page, 'Danh mục thu').getByLabel('Tên danh mục thu mới').fill('Photo tài liệu');
  await group(page, 'Danh mục thu').getByRole('button', { name: '+ Thêm' }).click();
  await expect(page.getByText('Đã thêm danh mục “Photo tài liệu”.')).toBeVisible();
  expect(calls.posts).toEqual([{ type: 'Income', name: 'Photo tài liệu' }]);
  await expect(group(page, 'Danh mục thu').getByText('3 đang dùng · 0 đã ẩn')).toBeVisible();
  await expect(group(page, 'Danh mục thu').getByLabel('Tên danh mục thu mới')).toHaveValue('');
});

test('đổi tên tại chỗ, Hủy, và ẩn / hiện lại giữ nguyên tên', async ({ page }) => {
  await signInAs(page, TREASURER);
  const calls = await mockCategoryApi(page);
  await page.goto(`/funds/${FUND}/categories`);
  const expense = group(page, 'Danh mục chi');

  await expense.getByRole('button', { name: 'Đổi tên Khen thưởng' }).click();
  await expense.getByLabel('Tên mới cho Khen thưởng').fill('');
  await expense.getByRole('button', { name: 'Lưu' }).click();
  await expect(expense.getByRole('alert')).toHaveText('Nhập tên danh mục.');
  await expense.getByRole('button', { name: 'Hủy' }).click();
  await expect(expense.getByLabel('Tên mới cho Khen thưởng')).toHaveCount(0);

  await expense.getByRole('button', { name: 'Đổi tên Khen thưởng' }).click();
  await expense.getByLabel('Tên mới cho Khen thưởng').fill('Khen thưởng, quà tặng');
  await expense.getByRole('button', { name: 'Lưu' }).click();
  await expect(page.getByText('Đã đổi tên danh mục thành “Khen thưởng, quà tặng”.')).toBeVisible();
  await expect(expense.getByText('Khen thưởng, quà tặng')).toBeVisible();

  await expense.getByRole('button', { name: 'Ẩn Chi khác' }).click();
  await expect(page.getByText('Đã ẩn “Chi khác”.')).toBeVisible();
  await expect(expense.getByText('3 đang dùng · 2 đã ẩn')).toBeVisible();
  await expense.getByRole('button', { name: 'Hiện lại Photo tài liệu' }).click();
  await expect(page.getByText('Đã hiện lại “Photo tài liệu”.')).toBeVisible();

  expect(calls.puts.map((call) => call.body)).toEqual([
    { name: 'Khen thưởng, quà tặng', isHidden: false },
    { name: 'Chi khác', isHidden: true },
    { name: 'Photo tài liệu', isHidden: false },
  ]);
});

test('GVCN không có menu Danh mục và vào thẳng đường dẫn thì bị chặn; học sinh không có menu Thành viên', async ({ page }) => {
  await signInAs(page, TEACHER);
  await page.goto(`/funds/${FUND}/students`);
  const nav = page.getByRole('navigation', { name: 'Điều hướng chính' });
  await expect(nav.getByRole('link', { name: 'Danh mục' })).toHaveCount(0);
  await expect(nav.getByRole('link', { name: 'Thành viên' })).toHaveCount(0);
  await page.goto(`/funds/${FUND}/categories`);
  await expect(page).toHaveURL(/\/forbidden$/);
  await expect(page.getByText('Trang này không dành cho vai trò của bạn')).toBeVisible();
});

test('học sinh vào thẳng /members và /categories thì bị chặn', async ({ page }) => {
  await signInAs(page, STUDENT);
  for (const path of ['members', 'categories']) {
    await page.goto(`/funds/${FUND}/${path}`);
    await expect(page).toHaveURL(/\/forbidden$/);
  }
});

test('T-03: thủ quỹ xem thành viên, lọc theo vai trò, không có email / tên đăng nhập', async ({ page }) => {
  await signInAs(page, TREASURER);
  await page.route(`**/api/v1/funds/${FUND}/members`, (route) =>
    route.fulfill({
      json: [
        { fullName: 'Trần Văn Hùng', role: 'Teacher' },
        { fullName: 'Nguyễn Minh Khang', role: 'Treasurer' },
        { fullName: 'Lê Gia Bảo', role: 'Student' },
        { fullName: 'Đặng An', role: 'Student' },
      ],
    }),
  );
  await page.goto(`/funds/${FUND}/members`);

  await expect(page.getByRole('heading', { name: 'Thành viên quỹ', level: 1 })).toBeVisible();
  await expect(page.getByRole('note')).toContainText('chỉ xem');
  const filters = page.getByRole('radiogroup', { name: 'Lọc vai trò' });
  for (const label of ['Tất cả (4)', 'GVCN (1)', 'Thủ quỹ (1)', 'Học sinh (2)']) {
    await expect(filters.getByRole('radio', { name: label })).toBeVisible();
  }

  const list = page.getByRole('region', { name: 'Danh sách thành viên' });
  await expect(list.locator('.row')).toHaveCount(4);
  await filters.getByRole('radio', { name: 'Học sinh (2)' }).click();
  await expect(list.locator('.row')).toHaveCount(2);
  await expect(list.getByText('Lê Gia Bảo')).toBeVisible();
  await expect(list.getByText('@')).toHaveCount(0);
});
