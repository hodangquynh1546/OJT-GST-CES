import { Page, expect, test } from '@playwright/test';

/** M2 + M5 (API giả lập): F-01 Quỹ của tôi, F-02 Tạo quỹ, F-03 Thông tin quỹ, C-01 Danh mục. */

const OWNER_PERMISSIONS = [
  'Fund.View', 'Fund.Manage', 'Member.Manage', 'Treasurer.Manage', 'Category.Manage', 'Event.Manage', 'Event.Participate',
  'Charge.Manage', 'Ledger.Manage', 'Payment.Submit', 'Payment.Confirm', 'Settlement.Execute', 'Balance.ViewOwn',
  'Balance.Withdraw', 'Balance.Handle', 'Report.View', 'Audit.View',
];
const STUDENT_PERMISSIONS = ['Fund.View', 'Event.Participate', 'Payment.Submit', 'Balance.ViewOwn', 'Balance.Withdraw'];

function me(memberships: object[], canCreate = true) {
  return {
    id: 'u1',
    email: 'minhanh@truong.edu.vn',
    fullName: 'Nguyễn Minh Anh',
    mustChangePassword: false,
    accountPermissions: canCreate ? ['Fund.Create'] : [],
    memberships,
  };
}

const fund10A1 = { fundId: 'f1', fundName: 'Lớp 10A1', role: 'Owner', permissions: OWNER_PERMISSIONS };
const clb = { fundId: 'f2', fundName: 'CLB Tiếng Anh', role: 'Student', permissions: STUDENT_PERMISSIONS };
const archived9A1 = { fundId: 'f3', fundName: 'Lớp 9A1', role: 'Treasurer', permissions: OWNER_PERMISSIONS.filter((p) => p !== 'Treasurer.Manage') };

async function common(page: Page, user: object) {
  await page.route('**/api/v1/auth/me', (route) => route.fulfill({ json: user }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ json: { antiforgeryToken: 't' } }));
  await page.route('**/api/v1/users/*/avatar', (route) => route.fulfill({ status: 404, contentType: 'application/problem+json', body: '{"code":"not_found"}' }));
}

test('F-01 shows fund cards with role and treasurer KPIs', async ({ page }) => {
  // Danh sách + vai trò lấy từ phiên (M1); số liệu thẻ từ GET /funds (M2).
  await common(page, me([fund10A1, clb, archived9A1]));
  await page.route('**/api/v1/funds', (route) =>
    route.fulfill({
      json: [
        { id: 'f1', name: 'Lớp 10A1', schoolYear: '2026-2027', status: 'Active', myRole: 'Owner', memberCount: 32, generalBalance: 4820000, pendingPaymentCount: 4 },
        { id: 'f2', name: 'CLB Tiếng Anh', schoolYear: '2026-2027', status: 'Active', myRole: 'Student', memberCount: 18, generalBalance: 1200000, pendingPaymentCount: null },
        { id: 'f3', name: 'Lớp 9A1', schoolYear: '2025-2026', status: 'Archived', myRole: 'Treasurer', memberCount: 30, generalBalance: 0, pendingPaymentCount: 0 },
      ],
    }),
  );

  await page.goto('/funds');

  await expect(page.getByRole('heading', { name: 'Quỹ của tôi' })).toBeVisible();
  const owner = page.getByRole('link', { name: /Lớp 10A1/ });
  await expect(owner).toContainText('2026–2027 · 32 thành viên');
  await expect(owner).toContainText('Thủ quỹ chính');
  await expect(owner).toContainText('4.820.000 đ');
  await expect(owner).toContainText('4 thanh toán');
  await expect(page.getByRole('link', { name: /CLB Tiếng Anh/ })).not.toContainText('Chờ xác nhận');
  await expect(page.getByRole('link', { name: /Lớp 9A1/ })).toContainText('Đã tổng kết');
  await page.screenshot({ path: 'test-results/F-01.png', fullPage: true });

  await page.getByRole('link', { name: /Tạo quỹ/ }).click();
  await expect(page).toHaveURL(/\/funds\/new$/);
});

test('F-01 still lists funds from the session when GET /funds fails', async ({ page }) => {
  await common(page, me([fund10A1]));
  await page.route('**/api/v1/funds', (route) => route.fulfill({ status: 500, contentType: 'application/problem+json', body: '{"code":"internal_error"}' }));

  await page.goto('/funds');

  const card = page.getByRole('link', { name: /Lớp 10A1/ });
  await expect(card).toContainText('Thủ quỹ chính');
  await expect(card).not.toContainText('Số dư quỹ chung');
});

test('F-01 empty state for student without funds has no create button', async ({ page }) => {
  await common(page, me([], false));
  await page.route('**/api/v1/funds', (route) => route.fulfill({ json: [] }));

  await page.goto('/funds');

  await expect(page.getByText('Bạn chưa thuộc quỹ nào')).toBeVisible();
  await expect(page.getByRole('link', { name: /Tạo quỹ/ })).toHaveCount(0);
});

test('F-02 creates a fund, refreshes session and opens it', async ({ page }) => {
  let created = false;
  await page.route('**/api/v1/auth/me', (route) => route.fulfill({ json: me(created ? [fund10A1] : []) }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ json: { antiforgeryToken: 't' } }));
  let body: unknown;
  await page.route('**/api/v1/funds', async (route) => {
    if (route.request().method() === 'POST') {
      body = route.request().postDataJSON();
      created = true;
      await route.fulfill({
        status: 201,
        json: { id: 'f1', name: 'Lớp 10A1', schoolYear: '2026-2027', status: 'Active', createdAt: '2026-11-18T03:00:00Z', ownerName: 'Nguyễn Minh Anh', memberCount: 1, myRole: 'Owner' },
      });
    } else {
      await route.fulfill({ json: [] });
    }
  });

  await page.goto('/funds/new');
  await expect(page.getByText('Đóng góp quỹ lớp')).toBeVisible();
  await page.screenshot({ path: 'test-results/F-02.png', fullPage: true });

  await page.getByRole('button', { name: 'Tạo quỹ' }).click();
  await expect(page.getByText('Vui lòng nhập tên quỹ')).toBeVisible();

  await page.getByLabel('Tên quỹ *').fill('  Lớp 10A1 ');
  await page.getByRole('button', { name: 'Tạo quỹ' }).click();

  await expect(page).toHaveURL(/\/funds\/f1$/);
  expect(body).toEqual(expect.objectContaining({ name: 'Lớp 10A1' }));
});

test('F-02 tells provisioned accounts they cannot create funds', async ({ page }) => {
  await common(page, me([clb], false));
  await page.goto('/funds/new');
  await expect(page.getByText('Tài khoản của bạn không tạo quỹ được')).toBeVisible();
});

test('F-03 renames the fund; school year is read-only', async ({ page }) => {
  await common(page, me([fund10A1]));
  const fund = { id: 'f1', name: 'Lớp 10A1', schoolYear: '2026-2027', status: 'Active', createdAt: '2026-09-01T03:00:00Z', ownerName: 'Nguyễn Minh Anh', memberCount: 32, myRole: 'Owner' };
  let saved: unknown;
  await page.route('**/api/v1/funds/f1', async (route) => {
    if (route.request().method() === 'PUT') {
      saved = route.request().postDataJSON();
      await route.fulfill({ json: { ...fund, name: 'Lớp 10A1 – THPT A' } });
    } else {
      await route.fulfill({ json: fund });
    }
  });

  await page.goto('/funds/f1/settings');

  await expect(page.getByText('Đang hoạt động')).toBeVisible();
  await page.screenshot({ path: 'test-results/F-03.png', fullPage: true });
  await expect(page.getByLabel('Năm học')).toBeDisabled();
  await expect(page.getByLabel('Năm học')).toHaveValue('2026–2027');

  await page.getByLabel('Tên quỹ').fill('Lớp 10A1 – THPT A');
  await page.getByRole('button', { name: 'Lưu thay đổi' }).click();
  await expect(page.getByText('Đã lưu thông tin quỹ.')).toBeVisible();
  expect(saved).toEqual({ name: 'Lớp 10A1 – THPT A', schoolYear: '2026-2027' });
});

test('C-01 lists categories by type, adds, renames and deactivates', async ({ page }) => {
  await common(page, me([fund10A1]));
  let categories = [
    { id: 'c1', name: 'Đóng góp quỹ lớp', type: 'Income', isSystem: true, isActive: true, usageCount: 28 },
    { id: 'c2', name: 'Tài trợ', type: 'Income', isSystem: false, isActive: false, usageCount: 0 },
    { id: 'c3', name: 'In ấn', type: 'Expense', isSystem: true, isActive: true, usageCount: 3 },
  ];
  await page.route('**/api/v1/funds/f1/categories**', async (route) => {
    const request = route.request();
    if (request.method() === 'POST') {
      const body = request.postDataJSON() as { name: string; type: string };
      if (body.name.toLowerCase() === 'in ấn') {
        await route.fulfill({ status: 409, contentType: 'application/problem+json', body: JSON.stringify({ code: 'category.duplicate_name', title: 'trùng' }) });
        return;
      }
      const created = { id: 'c9', name: body.name, type: body.type, isSystem: false, isActive: true, usageCount: 0 };
      categories = [...categories, created];
      await route.fulfill({ status: 201, json: created });
    } else if (request.method() === 'PUT') {
      const id = request.url().split('/').pop()!;
      const body = request.postDataJSON() as { name: string; isActive: boolean };
      categories = categories.map((item) => (item.id === id ? { ...item, ...body } : item));
      await route.fulfill({ json: categories.find((item) => item.id === id) });
    } else {
      await route.fulfill({ json: categories });
    }
  });

  await page.goto('/funds/f1/categories');

  const income = page.getByRole('region', { name: 'Danh mục thu' });
  const expense = page.getByRole('region', { name: 'Danh mục chi' });
  await expect(income).toContainText('Mặc định · 28 giao dịch');
  await expect(income.locator('[data-category="Tài trợ"]')).toContainText('Ngừng dùng');
  await page.screenshot({ path: 'test-results/C-01.png', fullPage: true });

  // Trùng tên → lỗi gắn vào ô tên.
  await page.getByLabel('Tên danh mục').fill('In ấn');
  await page.getByRole('button', { name: 'Thêm danh mục' }).click();
  await expect(page.getByText('Đã có danh mục cùng tên trong loại này.')).toBeVisible();

  await page.getByLabel('Tên danh mục').fill('Văn nghệ');
  await page.getByRole('button', { name: 'Thêm danh mục' }).click();
  await expect(expense.locator('[data-category="Văn nghệ"]')).toBeVisible();

  const print = expense.locator('[data-category="In ấn"]');
  await print.getByRole('button', { name: 'Đổi tên' }).click();
  await page.getByLabel('Tên mới cho In ấn').fill('In ấn, photo');
  await page.getByRole('button', { name: 'Lưu', exact: true }).click();
  const renamed = expense.locator('[data-category="In ấn, photo"]');
  await expect(renamed).toBeVisible();

  await renamed.getByRole('button', { name: 'Ngừng dùng' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Ngừng dùng' }).click();
  await expect(renamed.getByRole('button', { name: 'Dùng lại' })).toBeVisible();
});
