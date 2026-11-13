import { Page, expect, test } from '@playwright/test';

import { mockOverviewPage } from '../support/mock-overview';
import {
  FUND,
  PARENT,
  PUBLIC_PERMISSIONS,
  TEACHER,
  TEACHER_NO_FUND,
  TEACHER_PERMISSIONS,
  TREASURER,
  mockProblem,
  signInAs,
  user,
} from '../support/mock-session';

/** M2 – G-01 Quỹ của tôi / Quỹ của con, tạo quỹ, G-04 Thông tin quỹ (API giả lập; Canvas board FundList, FundSettings). */

test.beforeEach(async ({ page }) => {
  await mockOverviewPage(page); // mở quỹ từ G-01 vào S-01 (M7a)
});

interface MockFund {
  id: string;
  name: string;
  role: string;
  permissions: string[];
  currentBalance: number;
  createdAt: string;
  treasurerStatus: 'Unassigned' | 'Assigned';
  treasurerNames: string[];
  studentCount?: number;
  linkedStudents?: string[];
}

const NEW_FUND = '0199a000-0000-7000-8000-0000000000aa';

function teacherFund(overrides: Partial<MockFund> = {}): MockFund {
  return {
    id: FUND,
    name: 'Quỹ lớp 5A',
    role: 'Teacher',
    permissions: TEACHER_PERMISSIONS,
    currentBalance: 3_720_000,
    createdAt: '2026-08-15T02:00:00+00:00',
    treasurerStatus: 'Assigned',
    treasurerNames: ['Nguyễn Minh Khang', 'Phạm Thu Trang'],
    studentCount: 34,
    ...overrides,
  };
}

/** GET /funds trả `funds`; POST /funds thêm quỹ mới vào `funds` và cập nhật /auth/me tương ứng. */
async function mockFunds(page: Page, me: ReturnType<typeof user>, funds: MockFund[]): Promise<{ bodies: unknown[] }> {
  const bodies: unknown[] = [];
  const current = () => ({
    ...me,
    memberships: funds.map((fund) => ({ fundId: fund.id, fundName: fund.name, role: fund.role, permissions: fund.permissions, linkedStudents: fund.linkedStudents })),
  });
  await page.route('**/api/v1/auth/me', (route) => route.fulfill({ json: current() }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ json: { antiforgeryToken: 'token' } }));
  await page.route('**/api/v1/funds', async (route) => {
    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON() as { name: string };
      bodies.push(body);
      funds.push(teacherFund({ id: NEW_FUND, name: body.name, currentBalance: 0, createdAt: '2026-11-10T02:00:00+00:00', treasurerStatus: 'Unassigned', treasurerNames: [], studentCount: 0 }));
      return route.fulfill({ status: 201, json: { id: NEW_FUND } });
    }
    return route.fulfill({ json: funds });
  });
  await page.route(/\/api\/v1\/funds\/[^/]+$/, async (route) => {
    const body = route.request().postDataJSON() as { name: string };
    bodies.push(body);
    const fund = funds.find((item) => route.request().url().endsWith(item.id));
    if (fund) {
      fund.name = body.name;
    }
    return route.fulfill({ status: 204 });
  });
  return { bodies };
}

test('GVCN chưa có quỹ: trạng thái rỗng → tạo quỹ đầu tiên → thẻ quỹ chưa phân công + thông báo + header', async ({ page }) => {
  const { bodies } = await mockFunds(page, TEACHER_NO_FUND, []);
  await page.goto('/funds');

  await expect(page.getByRole('heading', { name: 'Quỹ của tôi' })).toBeVisible();
  await expect(page.getByText('Các quỹ bạn là giáo viên chủ nhiệm · 0 quỹ')).toBeVisible();
  await expect(page.getByText('Bạn chưa có quỹ nào')).toBeVisible();

  await page.getByRole('button', { name: '+ Tạo quỹ đầu tiên' }).click();
  const dialog = page.getByRole('dialog', { name: 'Tạo quỹ mới' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('note')).toContainText('Đóng quỹ, Thu khác, Văn phòng phẩm');

  // Lỗi chỉ hiện sau khi bấm "Tạo quỹ"; không gọi API khi tên trống.
  await expect(dialog.getByText('Hiển thị cho học sinh và phụ huynh của quỹ.')).toBeVisible();
  await dialog.getByRole('button', { name: 'Tạo quỹ', exact: true }).click();
  await expect(dialog.getByText('Nhập tên quỹ (tối đa 100 ký tự).')).toBeVisible();
  await expect(dialog.getByRole('textbox', { name: 'Tên quỹ *' })).toHaveAttribute('aria-invalid', 'true');
  expect(bodies).toHaveLength(0);

  await dialog.getByRole('textbox', { name: 'Tên quỹ *' }).fill('  Quỹ lớp 4C  ');
  await dialog.getByRole('button', { name: 'Tạo quỹ', exact: true }).click();

  await expect(dialog).toBeHidden();
  expect(bodies).toEqual([{ name: 'Quỹ lớp 4C' }]);
  await expect(page.getByText('Đã tạo “Quỹ lớp 4C”. Bước tiếp: thêm học sinh rồi phân công thủ quỹ.')).toBeVisible();
  await expect(page.getByText('Các quỹ bạn là giáo viên chủ nhiệm · 1 quỹ')).toBeVisible();
  const card = page.getByRole('link', { name: /Quỹ lớp 4C/ });
  await expect(card).toContainText('Chưa phân công thủ quỹ');
  await expect(card).toContainText('0 học sinh');
  await expect(card).toContainText('Phân công thủ quỹ →');
  await expect(card).toHaveAttribute('href', `/funds/${NEW_FUND}/treasurers`);
  // restoreSession(): quỹ mới có trong /auth/me → menu / header nhận quỹ khi mở.
  await card.click();
  await expect(page.locator('app-header')).toContainText('Quỹ lớp 4C');
});

test('GVCN: thẻ quỹ có số dư, nhãn trạng thái, số học sinh, thủ quỹ, ngày tạo; liên kết theo trạng thái', async ({ page }) => {
  await mockFunds(page, TEACHER, [
    teacherFund(),
    teacherFund({ id: NEW_FUND, name: 'Quỹ dã ngoại', currentBalance: -50_000, treasurerStatus: 'Unassigned', treasurerNames: [] }),
  ]);
  await page.goto('/funds');

  const assigned = page.getByRole('link', { name: /Quỹ lớp 5A/ });
  await expect(assigned).toContainText('Đã có thủ quỹ');
  await expect(assigned).toContainText('3.720.000 đ');
  await expect(assigned).toContainText('34 học sinh');
  await expect(assigned).toContainText('Nguyễn Minh Khang, Phạm Thu Trang');
  await expect(assigned).toContainText('15/08/2026');
  await expect(assigned).toHaveAttribute('href', `/funds/${FUND}`);

  const unassigned = page.getByRole('link', { name: /Quỹ dã ngoại/ });
  await expect(unassigned).toContainText('Chưa phân công thủ quỹ');
  await expect(unassigned).toContainText('−50.000 đ');
  await expect(unassigned).toContainText('—');
  await expect(unassigned).toHaveAttribute('href', `/funds/${NEW_FUND}/treasurers`);
});

test('phụ huynh: "Quỹ của con" hiện tên con, không số học sinh, không nút tạo quỹ', async ({ page }) => {
  await mockFunds(page, PARENT, [
    {
      id: FUND,
      name: 'Quỹ lớp 5A',
      role: 'Parent',
      permissions: PUBLIC_PERMISSIONS,
      currentBalance: 860_000,
      createdAt: '2026-08-15T02:00:00+00:00',
      treasurerStatus: 'Assigned',
      treasurerNames: ['Vũ Minh Anh'],
      linkedStudents: ['Phạm Gia Huy'],
    },
  ]);
  await page.goto('/funds');

  await expect(page.getByRole('heading', { name: 'Quỹ của con' })).toBeVisible();
  await expect(page.getByText('Các quỹ có con bạn được giáo viên liên kết · 1 quỹ')).toBeVisible();
  await expect(page.getByRole('button', { name: '+ Tạo quỹ' })).toHaveCount(0);
  const card = page.getByRole('link', { name: /Quỹ lớp 5A/ });
  await expect(card).toContainText('Con: Phạm Gia Huy');
  await expect(card).toContainText('Vũ Minh Anh');
  await expect(card).toContainText('Xem bản công khai →');
  await expect(card).not.toContainText('học sinh');
  await expect(page.getByText('Con bạn có làm thủ quỹ thì tài khoản phụ huynh của bạn cũng không có quyền thủ quỹ.')).toBeVisible();
});

test('lỗi tải danh sách → AppState lỗi, "Thử lại" tải lại', async ({ page }) => {
  await signInAs(page, TEACHER);
  let calls = 0;
  await page.route('**/api/v1/funds', (route) => {
    calls += 1;
    return calls === 1
      ? route.fulfill({ status: 500, contentType: 'application/problem+json', json: { code: 'internal_error' } })
      : route.fulfill({ json: [teacherFund()] });
  });
  await page.goto('/funds');

  await expect(page.getByRole('alert')).toContainText('Không tải được dữ liệu');
  await page.getByRole('button', { name: 'Thử lại' }).click();
  await expect(page.getByRole('link', { name: /Quỹ lớp 5A/ })).toBeVisible();
});

test('tạo quỹ: lỗi 400 từ server gắn vào ô Tên quỹ', async ({ page }) => {
  await mockFunds(page, TEACHER_NO_FUND, []);
  await page.route('**/api/v1/funds', (route) =>
    route.request().method() === 'POST'
      ? route.fulfill({
          status: 400,
          contentType: 'application/problem+json',
          json: { status: 400, code: 'validation_failed', title: 'Dữ liệu không hợp lệ.', errors: { name: ['Tên quỹ tối đa 100 ký tự.'] } },
        })
      : route.fallback(),
  );
  await page.goto('/funds');
  await page.getByRole('button', { name: '+ Tạo quỹ', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Tạo quỹ mới' });
  await dialog.getByRole('textbox', { name: 'Tên quỹ *' }).fill('Quỹ');
  await dialog.getByRole('button', { name: 'Tạo quỹ', exact: true }).click();

  await expect(dialog.getByText('Tên quỹ tối đa 100 ký tự.')).toBeVisible();
  await expect(dialog).toBeVisible();
});

test('G-04: đổi tên quỹ → thông báo, header cập nhật; Hoàn tác trả về tên đang lưu', async ({ page }) => {
  const { bodies } = await mockFunds(page, TEACHER, [teacherFund()]);
  await page.goto(`/funds/${FUND}/settings`);

  await expect(page.getByRole('heading', { name: 'Thông tin quỹ' })).toBeVisible();
  const form = page.getByRole('form', { name: 'Thông tin quỹ' });
  await expect(form).toContainText('15/08/2026');
  await expect(form).toContainText('Nguyễn Minh Khang (bạn)');
  await expect(form).toContainText('Nguyễn Minh Khang, Phạm Thu Trang');
  await expect(form).toContainText('34 học sinh');
  await expect(form.getByRole('link', { name: 'Học sinh & phụ huynh' })).toHaveAttribute('href', `/funds/${FUND}/students`);
  await expect(form.getByRole('link', { name: 'Phân công thủ quỹ' })).toHaveAttribute('href', `/funds/${FUND}/treasurers`);
  await expect(form).toContainText('Do thủ quỹ quản lý (GVCN chỉ xem)');

  const name = form.getByRole('textbox', { name: 'Tên quỹ *' });
  await expect(name).toHaveValue('Quỹ lớp 5A');

  await name.fill('');
  await form.getByRole('button', { name: 'Lưu' }).click();
  await expect(form.getByText('Nhập tên quỹ (tối đa 100 ký tự).')).toBeVisible();
  await form.getByRole('button', { name: 'Hoàn tác' }).click();
  await expect(name).toHaveValue('Quỹ lớp 5A');
  await expect(form.getByText('Nhập tên quỹ (tối đa 100 ký tự).')).toHaveCount(0);
  expect(bodies).toHaveLength(0);

  await name.fill(' Quỹ lớp 5A – 2026 ');
  await form.getByRole('button', { name: 'Lưu' }).click();
  await expect(page.getByText('Đã đổi tên quỹ.')).toBeVisible();
  expect(bodies).toEqual([{ name: 'Quỹ lớp 5A – 2026' }]);
  await expect(page.locator('app-header')).toContainText('Quỹ lớp 5A – 2026');
  await expect(name).toHaveValue('Quỹ lớp 5A – 2026');
});

test('G-04: thủ quỹ (không có Fund.Manage) vào /settings → trang 403', async ({ page }) => {
  await signInAs(page, TREASURER);
  await page.goto(`/funds/${FUND}/settings`);
  await expect(page).toHaveURL(/\/forbidden$/);
});

test('G-04: quỹ không còn trong danh sách → 404', async ({ page }) => {
  await signInAs(page, TEACHER);
  await page.route('**/api/v1/funds', (route) => route.fulfill({ json: [] }));
  await page.goto(`/funds/${FUND}/settings`);
  await expect(page.getByText('Không tìm thấy trang')).toBeVisible();
});

test('G-04: đổi tên khi đã mất quyền (403 từ server) → AppState 403', async ({ page }) => {
  await mockFunds(page, TEACHER, [teacherFund()]);
  await mockProblem(page, `**/api/v1/funds/${FUND}`, 403, 'forbidden');
  await page.goto(`/funds/${FUND}/settings`);
  const form = page.getByRole('form', { name: 'Thông tin quỹ' });
  await form.getByRole('textbox', { name: 'Tên quỹ *' }).fill('Tên mới');
  await form.getByRole('button', { name: 'Lưu' }).click();
  await expect(page.getByText('Trang này không dành cho vai trò của bạn')).toBeVisible();
});
