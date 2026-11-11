import { Page, expect, test } from '@playwright/test';

import { FUND, TEACHER, TREASURER, mockProblem, signInAs } from '../support/mock-session';
import {
  MockStudent,
  STUDENTS_4_STATUS,
  STUDENT_ACTIVE,
  STUDENT_NONE,
  STUDENT_PENDING,
} from '../support/mock-students';

/**
 * M3 – G-02 "Học sinh & phụ huynh" với API giả lập (hợp đồng: docs/api/backend-api.md – Students).
 * Mật khẩu tạm chỉ hiện một lần trong hộp thoại phải tick xác nhận mới đóng được (FR-25, NFR-11).
 */
const PAGE_URL = `/funds/${FUND}/students`;
const API = `**/api/v1/funds/${FUND}/students`;
const TEMP_PASSWORD = 'Abcd-efgh-2345-mnpq';

interface Calls {
  bodies: { method: string; path: string; body: unknown }[];
}

/** Backend giả có trạng thái: danh sách đổi theo các thao tác để kiểm tra việc tải lại sau mỗi lần ghi. */
async function mockStudentsApi(page: Page, initial: MockStudent[] = STUDENTS_4_STATUS): Promise<Calls> {
  const students = initial.map((student) => ({ ...student }));
  const calls: Calls = { bodies: [] };
  await signInAs(page, TEACHER);
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ json: { antiforgeryToken: 'token' } }));
  await page.route(`${API}**`, async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(`/api/v1/funds/${FUND}/students`, '');
    const method = request.method();
    if (method === 'GET') {
      return route.fulfill({ json: students });
    }
    const body = request.postDataJSON() as Record<string, string | undefined> | null;
    calls.bodies.push({ method, path, body });
    const id = path.split('/')[1];
    const student = students.find((item) => item.id === id);
    if (method === 'POST' && path === '') {
      const created: MockStudent = {
        id: `n${students.length}`,
        fullName: body?.['fullName'] ?? '',
        username: null,
        accountStatus: 'None',
        isTreasurer: false,
        parent: null,
        createdAt: '2026-11-11T01:00:00Z',
      };
      students.push(created);
      if (!body?.['parentEmail']) {
        return route.fulfill({ status: 201, json: { id: created.id } });
      }
      const existing = body['parentEmail'] === 'ha.le@example.com';
      created.parent = { fullName: body['parentFullName'] ?? 'Phụ huynh', email: body['parentEmail'], pendingActivation: !existing };
      return route.fulfill({
        status: 201,
        json: {
          id: created.id,
          parent: {
            accountCreated: !existing,
            credential: existing ? null : { login: body['parentEmail'], temporaryPassword: TEMP_PASSWORD },
            emailSent: false,
          },
        },
      });
    }
    if (!student) {
      return route.fulfill({ status: 404, contentType: 'application/problem+json', json: { status: 404, code: 'not_found', title: 'Không tìm thấy dữ liệu.' } });
    }
    if (path.endsWith('/account')) {
      if (body?.['username'] === 'da.co.nguoi') {
        return route.fulfill({ status: 422, contentType: 'application/problem+json', json: { status: 422, code: 'user.username_taken', title: 'Tên đăng nhập đã được sử dụng.' } });
      }
      student.username = body?.['username']?.trim().toLowerCase() ?? 'huy.pham.482';
      student.accountStatus = 'PendingActivation';
      return route.fulfill({ json: { login: student.username, temporaryPassword: TEMP_PASSWORD } });
    }
    if (path.endsWith('/reset-password')) {
      student.accountStatus = 'PendingActivation';
      return route.fulfill({ json: { login: student.username, temporaryPassword: TEMP_PASSWORD } });
    }
    if (path.endsWith('/parent/unlink')) {
      student.parent = null;
      return route.fulfill({ status: 204 });
    }
    if (path.endsWith('/parent')) {
      const email = String(body?.['email']);
      if (email === 'co.hanh@example.vn') {
        return route.fulfill({ status: 422, contentType: 'application/problem+json', json: { status: 422, code: 'parent.email_in_use', title: 'Email này đang dùng cho tài khoản giáo viên / học sinh, không liên kết làm phụ huynh được.' } });
      }
      const existing = email === 'ha.le@example.com';
      student.parent = { fullName: existing ? 'Lê Thu Hà' : (body?.['fullName'] ?? `Phụ huynh ${student.fullName}`), email, pendingActivation: !existing };
      return route.fulfill({ json: { accountCreated: !existing, credential: existing ? null : { login: email, temporaryPassword: TEMP_PASSWORD }, emailSent: false } });
    }
    student.fullName = body?.['fullName'] ?? student.fullName;
    return route.fulfill({ status: 204 });
  });
  return calls;
}

const row = (page: Page, name: string) => page.getByRole('row', { name: new RegExp(name) });
const openDrawer = async (page: Page, name: string) => {
  await page.getByRole('button', { name: `Mở ${name}` }).click();
  return page.getByRole('dialog', { name });
};

async function ackAndClose(page: Page, title: string | RegExp) {
  const dialog = page.getByRole('dialog', { name: title });
  await expect(dialog.getByText('Chỉ hiển thị một lần.')).toBeVisible();
  await expect(dialog.getByTestId('credential-password')).toHaveText(TEMP_PASSWORD);
  const close = dialog.getByRole('button', { name: 'Đóng', exact: true });
  await expect(close).toBeDisabled();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeVisible();
  await dialog.getByLabel('Tôi đã lưu / chuyển thông tin này').check();
  await close.click();
  await expect(dialog).toBeHidden();
}

test('danh sách: 4 trạng thái tài khoản, thủ quỹ, phụ huynh; tóm tắt và bộ lọc có số đếm', async ({ page }) => {
  await mockStudentsApi(page);
  await page.goto(PAGE_URL);

  await expect(page.getByRole('heading', { name: 'Học sinh & phụ huynh' })).toBeVisible();
  await expect(page.getByText('Quỹ lớp 5A · 4 học sinh · 0 thủ quỹ · 1 phụ huynh đã liên kết')).toBeVisible();
  await expect(page.getByRole('note')).toContainText('không gửi email');

  await expect(row(page, 'Lê Gia Bảo')).toContainText('Chưa cấp');
  await expect(row(page, 'Lê Gia Bảo')).toContainText('— Chưa liên kết');
  await expect(row(page, 'Phạm Gia Huy')).toContainText('Chờ kích hoạt');
  await expect(row(page, 'Phạm Gia Huy')).toContainText('huy.pham.482');
  await expect(row(page, 'Phạm Gia Huy')).toContainText('long.pham@example.vn · chờ kích hoạt');
  await expect(row(page, 'Nguyễn Minh Khang')).toContainText('Đang hoạt động');
  await expect(row(page, 'Trần Quốc Việt')).toContainText('Bị khóa');

  await page.getByRole('radio', { name: /Chưa kích hoạt/ }).click();
  await expect(page.getByRole('radio', { name: 'Chưa kích hoạt (2)' })).toHaveAttribute('aria-checked', 'true');
  await expect(row(page, 'Lê Gia Bảo')).toBeVisible();
  await expect(row(page, 'Phạm Gia Huy')).toBeVisible();
  await expect(row(page, 'Nguyễn Minh Khang')).toHaveCount(0);

  await page.getByRole('radio', { name: /Tất cả/ }).click();
  await page.getByRole('searchbox', { name: 'Tìm học sinh' }).fill('viet');
  await expect(row(page, 'Trần Quốc Việt')).toBeVisible();
  await expect(row(page, 'Phạm Gia Huy')).toHaveCount(0);
  await page.getByRole('searchbox', { name: 'Tìm học sinh' }).fill('không có ai');
  await expect(page.getByText('Không có học sinh phù hợp.')).toBeVisible();
});

test('thủ quỹ không vào được G-02 (403) và không gọi API học sinh', async ({ page }) => {
  await signInAs(page, TREASURER);
  let called = false;
  await page.route(`${API}**`, (route) => {
    called = true;
    return route.fulfill({ json: [] });
  });
  await page.goto(PAGE_URL);

  await expect(page.getByText('Trang này không dành cho vai trò của bạn')).toBeVisible();
  expect(called).toBe(false);
});

test('lỗi tải danh sách → thử lại', async ({ page }) => {
  await signInAs(page, TEACHER);
  let attempts = 0;
  await page.route(`${API}**`, (route) => {
    attempts += 1;
    return attempts === 1
      ? route.fulfill({ status: 500, contentType: 'application/problem+json', json: { status: 500, code: 'internal_error', title: 'Lỗi' } })
      : route.fulfill({ json: [STUDENT_ACTIVE] });
  });
  await page.goto(PAGE_URL);

  await expect(page.getByText('Không tải được dữ liệu')).toBeVisible();
  await page.getByRole('button', { name: 'Thử lại' }).click();
  await expect(row(page, 'Nguyễn Minh Khang')).toBeVisible();
});

test('thêm học sinh: lỗi chỉ hiện sau khi lưu, không gọi API khi sai; kèm email mới → hộp thoại mật khẩu tạm phụ huynh', async ({ page }) => {
  const calls = await mockStudentsApi(page);
  await page.goto(PAGE_URL);

  await page.getByRole('button', { name: '+ Thêm học sinh' }).click();
  const dialog = page.getByRole('dialog', { name: 'Thêm học sinh' });
  await expect(dialog.getByText('Học sinh được thêm chưa có tài khoản.')).toBeVisible();
  await expect(dialog.getByText('Nhập họ tên học sinh')).toHaveCount(0);

  await dialog.getByRole('textbox', { name: 'Email phụ huynh (tùy chọn)' }).fill('khong-phai-email');
  await dialog.getByRole('button', { name: 'Lưu học sinh' }).click();
  await expect(dialog.getByText('Chưa lưu được. Kiểm tra các ô được đánh dấu.')).toBeVisible();
  await expect(dialog.getByText('Nhập họ tên học sinh (tối đa 100 ký tự).')).toBeVisible();
  await expect(dialog.getByText('Email phụ huynh không hợp lệ.')).toBeVisible();
  expect(calls.bodies).toHaveLength(0);

  await dialog.getByRole('textbox', { name: 'Họ tên học sinh *' }).fill('  Vũ Ngọc Ánh  ');
  await dialog.getByRole('textbox', { name: 'Email phụ huynh (tùy chọn)' }).fill(' loc.vu@example.vn ');
  await dialog.getByRole('textbox', { name: 'Họ tên phụ huynh (tùy chọn)' }).fill('Vũ Văn Lộc');
  await dialog.getByRole('button', { name: 'Lưu học sinh' }).click();

  await expect(dialog).toBeHidden();
  expect(calls.bodies).toEqual([
    { method: 'POST', path: '', body: { fullName: 'Vũ Ngọc Ánh', parentEmail: 'loc.vu@example.vn', parentFullName: 'Vũ Văn Lộc' } },
  ]);
  const credential = page.getByRole('dialog', { name: 'Tài khoản phụ huynh mới' });
  await expect(credential.getByText('Email đăng nhập')).toBeVisible();
  await expect(credential.getByTestId('credential-login')).toHaveText('loc.vu@example.vn');
  await ackAndClose(page, 'Tài khoản phụ huynh mới');

  await expect(row(page, 'Vũ Ngọc Ánh')).toContainText('Vũ Văn Lộc');
  await expect(page.getByText('Đã thêm học sinh Vũ Ngọc Ánh.')).toBeVisible();
});

test('thêm học sinh kèm email phụ huynh đã có tài khoản → chỉ liên kết, không có hộp thoại mật khẩu', async ({ page }) => {
  await mockStudentsApi(page);
  await page.goto(PAGE_URL);

  await page.getByRole('button', { name: '+ Thêm học sinh' }).click();
  const dialog = page.getByRole('dialog', { name: 'Thêm học sinh' });
  await dialog.getByRole('textbox', { name: 'Họ tên học sinh *' }).fill('Lê Thu Trang');
  await dialog.getByRole('textbox', { name: 'Email phụ huynh (tùy chọn)' }).fill('ha.le@example.com');
  await dialog.getByRole('button', { name: 'Lưu học sinh' }).click();

  await expect(dialog).toBeHidden();
  await expect(page.getByText('Đã liên kết với tài khoản phụ huynh hiện có.')).toBeVisible();
  await expect(page.getByRole('dialog', { name: /Tài khoản phụ huynh mới/ })).toHaveCount(0);
});

test('drawer: cấp tài khoản – tên đăng nhập sai bị chặn, trùng báo ở ô, hợp lệ → mật khẩu tạm một lần', async ({ page }) => {
  const calls = await mockStudentsApi(page);
  await page.goto(PAGE_URL);

  const drawer = await openDrawer(page, STUDENT_NONE.fullName);
  await expect(drawer.getByText('Chưa có tài khoản. Cấp tên đăng nhập + mật khẩu tạm')).toBeVisible();
  await expect(drawer.getByRole('button', { name: 'Cấp lại mật khẩu tạm' })).toHaveCount(0);
  await expect(drawer.getByText('Để trống: hệ thống gợi ý theo họ tên')).toBeVisible();

  const username = drawer.getByRole('textbox', { name: 'Tên đăng nhập' });
  await username.fill('bảo lê');
  await drawer.getByRole('button', { name: 'Cấp tài khoản & mật khẩu tạm' }).click();
  await expect(drawer.getByText('3–60 ký tự: chữ thường không dấu')).toBeVisible();
  expect(calls.bodies).toHaveLength(0);

  await username.fill('da.co.nguoi');
  await drawer.getByRole('button', { name: 'Cấp tài khoản & mật khẩu tạm' }).click();
  await expect(drawer.getByText('Tên đăng nhập đã được sử dụng.')).toBeVisible();

  await username.fill('  Bao.Le.001 ');
  await drawer.getByRole('button', { name: 'Cấp tài khoản & mật khẩu tạm' }).click();
  await expect(page.getByRole('dialog', { name: 'Tài khoản học sinh – Lê Gia Bảo' }).getByTestId('credential-login')).toHaveText('bao.le.001');
  await ackAndClose(page, 'Tài khoản học sinh – Lê Gia Bảo');

  // Danh sách tải lại: học sinh chuyển sang "Chờ kích hoạt" và drawer đổi theo.
  await expect(row(page, 'Lê Gia Bảo')).toContainText('Chờ kích hoạt');
  await expect(drawer.getByTestId('drawer-username')).toHaveText('bao.le.001');
  await expect(drawer.getByRole('button', { name: 'Cấp lại mật khẩu tạm' })).toBeVisible();
  await expect(drawer.getByRole('button', { name: 'Cấp tài khoản & mật khẩu tạm' })).toHaveCount(0);
});

test('drawer: cấp tài khoản để trống tên đăng nhập → gửi không kèm username (hệ thống gợi ý)', async ({ page }) => {
  const calls = await mockStudentsApi(page);
  await page.goto(PAGE_URL);

  const drawer = await openDrawer(page, STUDENT_NONE.fullName);
  await drawer.getByRole('button', { name: 'Cấp tài khoản & mật khẩu tạm' }).click();

  await expect(page.getByTestId('credential-login')).toHaveText('huy.pham.482');
  expect(calls.bodies).toEqual([{ method: 'POST', path: `/${STUDENT_NONE.id}/account`, body: {} }]);
});

test('drawer: cấp lại mật khẩu tạm cần xác nhận; "Không" thì không gọi API', async ({ page }) => {
  const calls = await mockStudentsApi(page);
  await page.goto(PAGE_URL);

  const drawer = await openDrawer(page, STUDENT_ACTIVE.fullName);
  await expect(drawer.getByText('Học sinh đã kích hoạt tài khoản.')).toBeVisible();
  await expect(drawer.getByText('Đủ điều kiện để được phân công thủ quỹ.')).toBeVisible();

  await drawer.getByRole('button', { name: 'Cấp lại mật khẩu tạm' }).click();
  await expect(drawer.getByText('Mật khẩu cũ hết hiệu lực và học sinh bị đăng xuất trên mọi thiết bị.')).toBeVisible();
  await drawer.getByRole('button', { name: 'Không' }).click();
  expect(calls.bodies).toHaveLength(0);

  await drawer.getByRole('button', { name: 'Cấp lại mật khẩu tạm' }).click();
  await drawer.getByRole('button', { name: 'Cấp lại', exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Mật khẩu tạm mới – Nguyễn Minh Khang' })).toBeVisible();
  await ackAndClose(page, 'Mật khẩu tạm mới – Nguyễn Minh Khang');
  await expect(row(page, 'Nguyễn Minh Khang')).toContainText('Chờ kích hoạt');
});

test('drawer: liên kết phụ huynh – email giáo viên báo lỗi ở ô email; email đã có → chỉ liên kết; email mới → mật khẩu tạm', async ({ page }) => {
  await mockStudentsApi(page);
  await page.goto(PAGE_URL);

  const drawer = await openDrawer(page, STUDENT_NONE.fullName);
  const email = drawer.getByRole('textbox', { name: 'Email phụ huynh *' });
  await drawer.getByRole('button', { name: 'Liên kết phụ huynh' }).click();
  await expect(drawer.getByText('Nhập email phụ huynh.')).toBeVisible();

  await email.fill('co.hanh@example.vn');
  await drawer.getByRole('button', { name: 'Liên kết phụ huynh' }).click();
  await expect(drawer.getByText('Email này đang dùng cho tài khoản giáo viên / học sinh')).toBeVisible();
  await expect(email).toHaveAttribute('aria-invalid', 'true');

  await email.fill('ha.le@example.com');
  await drawer.getByRole('button', { name: 'Liên kết phụ huynh' }).click();
  await expect(page.getByText('Đã liên kết với tài khoản phụ huynh hiện có. Không tạo tài khoản mới.')).toBeVisible();
  await expect(page.getByRole('dialog', { name: /Tài khoản phụ huynh mới/ })).toHaveCount(0);
  await expect(drawer.getByText('Lê Thu Hà')).toBeVisible();
  await expect(drawer.getByRole('button', { name: 'Gỡ liên kết' })).toBeVisible();
});

test('drawer: gỡ liên kết phụ huynh cần xác nhận', async ({ page }) => {
  const calls = await mockStudentsApi(page);
  await page.goto(PAGE_URL);

  const drawer = await openDrawer(page, STUDENT_PENDING.fullName);
  await expect(drawer.getByText('Chờ kích hoạt (chưa đổi mật khẩu tạm)')).toBeVisible();
  await drawer.getByRole('button', { name: 'Gỡ liên kết' }).click();
  await expect(drawer.getByText('Phụ huynh không xem được quỹ này nữa.')).toBeVisible();
  await drawer.getByRole('button', { name: 'Không' }).click();
  expect(calls.bodies).toHaveLength(0);

  await drawer.getByRole('button', { name: 'Gỡ liên kết' }).click();
  await drawer.getByRole('button', { name: 'Gỡ liên kết' }).last().click();

  await expect(page.getByText('Đã gỡ liên kết phụ huynh của Phạm Gia Huy.')).toBeVisible();
  await expect(drawer.getByRole('textbox', { name: 'Email phụ huynh *' })).toBeVisible();
  await expect(row(page, 'Phạm Gia Huy')).toContainText('— Chưa liên kết');
  expect(calls.bodies.at(-1)).toEqual({ method: 'POST', path: `/${STUDENT_PENDING.id}/parent/unlink`, body: {} });
});

test('drawer: sửa họ tên – họ tên trống bị chặn; đóng bằng Esc; link sang phân công thủ quỹ', async ({ page }) => {
  const calls = await mockStudentsApi(page);
  await page.goto(PAGE_URL);

  const drawer = await openDrawer(page, STUDENT_ACTIVE.fullName);
  await drawer.getByRole('button', { name: 'Sửa họ tên' }).click();
  const name = drawer.getByRole('textbox', { name: 'Họ tên *' });
  await name.fill('   ');
  await drawer.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(drawer.getByText('Nhập họ tên học sinh')).toBeVisible();
  expect(calls.bodies).toHaveLength(0);

  await name.fill('  Nguyễn Minh Khang B ');
  await drawer.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(page.getByText('Đã đổi họ tên học sinh.')).toBeVisible();
  expect(calls.bodies).toEqual([{ method: 'PUT', path: `/${STUDENT_ACTIVE.id}`, body: { fullName: 'Nguyễn Minh Khang B' } }]);
  await expect(row(page, 'Nguyễn Minh Khang B')).toBeVisible();

  await expect(drawer.getByRole('link', { name: 'Mở “Phân công thủ quỹ”' })).toHaveAttribute('href', `/funds/${FUND}/treasurers`);
  await page.keyboard.press('Escape');
  await expect(drawer).toBeHidden();
});

test('lỗi server khi cấp tài khoản (học sinh đã có tài khoản) → báo lỗi và tải lại danh sách', async ({ page }) => {
  await mockStudentsApi(page);
  await page.goto(PAGE_URL);
  await mockProblem(page, `${API}/${STUDENT_NONE.id}/account`, 422, 'student.account_exists', 'Học sinh đã có tài khoản.');

  const drawer = await openDrawer(page, STUDENT_NONE.fullName);
  await drawer.getByRole('button', { name: 'Cấp tài khoản & mật khẩu tạm' }).click();

  await expect(drawer.getByRole('alert').filter({ hasText: 'Học sinh đã có tài khoản.' })).toBeVisible();
});
