import { Page, expect, test } from '@playwright/test';

import { AN, FakeAuthApi, FakeUser } from '../support/fake-auth-api';
import { FUND_ID, FakeMembersApi, Role, member } from '../support/fake-members-api';

/** M3-T3: màn M-01 Học sinh với API giả lập (docs/testing/test-strategy.md). */

const OWNER_PERMISSIONS = [
  'Fund.View',
  'Fund.Manage',
  'Member.Manage',
  'Treasurer.Manage',
  'Category.Manage',
  'Event.Manage',
  'Event.Participate',
  'Charge.Manage',
  'Ledger.Manage',
  'Payment.Submit',
  'Payment.Confirm',
  'Settlement.Execute',
  'Balance.ViewOwn',
  'Balance.Withdraw',
  'Balance.Handle',
  'Report.View',
  'Audit.View',
];
const TREASURER_PERMISSIONS = OWNER_PERMISSIONS.filter((item) => item !== 'Treasurer.Manage');
const STUDENT_PERMISSIONS = ['Fund.View', 'Event.Participate', 'Payment.Submit', 'Balance.ViewOwn', 'Balance.Withdraw'];

function userAs(role: Role): FakeUser {
  const permissions = role === 'Owner' ? OWNER_PERMISSIONS : role === 'Treasurer' ? TREASURER_PERMISSIONS : STUDENT_PERMISSIONS;
  return { ...AN, fullName: 'Nguyễn Minh Anh', memberships: [{ fundId: FUND_ID, fundName: 'Lớp 10A1', role, permissions }] };
}

const BASE = () => [
  member({ fullName: 'Nguyễn Minh Anh', email: 'minhanh@truong.edu.vn', role: 'Owner', canManageAccount: false }),
  member({ fullName: 'Trần Quang Huy', email: 'huy.tran@truong.edu.vn', role: 'Treasurer' }),
  member({ fullName: 'Lê Thu Hà', email: 'ha.le@truong.edu.vn' }),
  member({ fullName: 'Bùi Thảo My', email: 'my.bui@truong.edu.vn', mustChangePassword: true }),
  member({ fullName: 'Phạm Đức Long', email: 'long.pham@truong.edu.vn' }),
  member({ fullName: 'Đã Nghỉ Học', email: 'nghi@truong.edu.vn', status: 'Removed', removedAt: '2026-10-01T03:00:00Z' }),
];

async function open(page: Page, role: Role = 'Owner', members = BASE()): Promise<FakeMembersApi> {
  const auth = new FakeAuthApi(page);
  auth.addAccount(userAs(role), 'Passw0rd1');
  auth.signIn(userAs(role));
  await auth.install();
  const api = new FakeMembersApi(page, members);
  await api.install();
  await page.goto(`/funds/${FUND_ID}/members`);
  return api;
}

const rowOf = (page: Page, name: string | RegExp) => page.getByRole('row', { name });

test.describe('Danh sách', () => {
  test('hiện số thành viên hoạt động, từng người với vai trò, trạng thái và ngày tham gia', async ({ page }) => {
    await open(page);

    await expect(page.getByRole('heading', { name: 'Học sinh', exact: true })).toBeVisible();
    await expect(page.getByText('5 thành viên đang hoạt động.')).toBeVisible();
    await expect(rowOf(page, /Nguyễn Minh Anh/)).toContainText('Thủ quỹ chính');
    await expect(rowOf(page, /Trần Quang Huy/)).toContainText('Thủ quỹ');
    await expect(rowOf(page, /Lê Thu Hà/)).toContainText('Học sinh');
    await expect(rowOf(page, /Lê Thu Hà/)).toContainText('Hoạt động');
    await expect(rowOf(page, /Bùi Thảo My/)).toContainText('Chưa đổi mật khẩu tạm');
    await expect(rowOf(page, /Lê Thu Hà/)).toContainText('02/09/2026');
    await expect(page.getByText('Đã Nghỉ Học')).toHaveCount(0);
  });

  test('thủ quỹ chính không có nút trên hàng của chính mình; có nút trên thủ quỹ và học sinh (AZ-02)', async ({ page }) => {
    await open(page, 'Owner');

    await expect(rowOf(page, /Nguyễn Minh Anh/).getByRole('button')).toHaveCount(0);
    await expect(rowOf(page, /Trần Quang Huy/).getByRole('button', { name: 'Ngừng' })).toBeVisible();
    await expect(rowOf(page, /Lê Thu Hà/).getByRole('button', { name: 'Cấp mật khẩu tạm' })).toBeVisible();
  });

  test('thủ quỹ thường chỉ có nút trên học sinh, không có trên thủ quỹ chính hay thủ quỹ khác (AZ-02)', async ({ page }) => {
    await open(page, 'Treasurer');

    await expect(rowOf(page, /Nguyễn Minh Anh/).getByRole('button')).toHaveCount(0);
    await expect(rowOf(page, /Trần Quang Huy/).getByRole('button')).toHaveCount(0);
    await expect(rowOf(page, /Lê Thu Hà/).getByRole('button', { name: 'Ngừng' })).toBeVisible();
  });

  test('tìm theo tên hoặc email, có debounce và quay về trang 1', async ({ page }) => {
    const api = await open(page);

    await page.getByRole('searchbox', { name: 'Tìm thành viên' }).fill('thảo');
    await expect(page.getByText('Bùi Thảo My')).toBeVisible();
    await expect(page.getByText('Lê Thu Hà')).toHaveCount(0);
    expect(api.last('GET', '/')?.query.get('search')).toBe('thảo');

    await page.getByRole('searchbox', { name: 'Tìm thành viên' }).fill('long.pham@');
    await expect(page.getByText('Phạm Đức Long')).toBeVisible();
    await expect(page.getByText('Bùi Thảo My')).toHaveCount(0);

    await page.getByRole('searchbox', { name: 'Tìm thành viên' }).fill('không-có-ai');
    await expect(page.getByText('Không có thành viên phù hợp.')).toBeVisible();
  });

  test('tab Đã ngừng hiện người đã ngừng kèm nút Thêm lại; Thêm lại kích hoạt và chuyển sang Đang hoạt động', async ({ page }) => {
    const api = await open(page);

    await page.getByRole('tab', { name: 'Đã ngừng' }).click();
    await expect(page.getByRole('tab', { name: 'Đã ngừng' })).toHaveAttribute('aria-selected', 'true');
    await expect(rowOf(page, /Đã Nghỉ Học/)).toContainText('Đã ngừng');
    expect(api.last('GET', '/')?.query.get('status')).toBe('Removed');

    await rowOf(page, /Đã Nghỉ Học/).getByRole('button', { name: 'Thêm lại' }).click();
    await expect(page.getByText('Đã kích hoạt lại: Đã Nghỉ Học.')).toBeVisible();
    await expect(page.getByText('Chưa có thành viên nào bị ngừng.')).toBeVisible();

    await page.getByRole('tab', { name: 'Đang hoạt động' }).click();
    // Toast "Đã kích hoạt lại: Đã Nghỉ Học." chứa cùng tên nên phải khoanh vùng vào hàng của bảng.
    await expect(rowOf(page, /Đã Nghỉ Học/)).toBeVisible();
    await expect(page.getByText('6 thành viên đang hoạt động.')).toBeVisible();
  });

  test('người không có quyền quản lý thành viên bị chuyển tới trang 403', async ({ page }) => {
    await open(page, 'Student');

    await expect(page).toHaveURL(/\/forbidden$/);
  });
});

test.describe('Thêm một email', () => {
  test('email mới: tạo tài khoản và hiện mật khẩu tạm MỘT lần kèm cảnh báo; đóng rồi không còn thấy', async ({ page }) => {
    await open(page);

    await page.getByLabel('Email học sinh').fill('  Khoa.Tran@Truong.edu.vn ');
    await page.getByLabel('Họ tên').fill('Trần Đăng Khoa');
    await page.getByRole('button', { name: 'Thêm vào lớp' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('Mật khẩu tạm của Trần Đăng Khoa');
    await expect(dialog).toContainText('khoa.tran@truong.edu.vn');
    await expect(dialog).toContainText('Tam-0001-abcd');
    await expect(dialog).toContainText('Mật khẩu này sẽ không hiển thị lại');

    await dialog.getByRole('button', { name: 'Đã lưu, đóng' }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(page.getByText('Tam-0001-abcd')).toHaveCount(0);
    await expect(rowOf(page, /Trần Đăng Khoa/)).toContainText('Chưa đổi mật khẩu tạm');
    await expect(page.getByText('6 thành viên đang hoạt động.')).toBeVisible();
    await expect(page.getByLabel('Email học sinh')).toHaveValue('');
  });

  test('nút Sao chép đưa email và mật khẩu tạm vào clipboard', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await open(page);
    await page.getByLabel('Email học sinh').fill('khoa@truong.edu.vn');
    await page.getByLabel('Họ tên').fill('Khoa');
    await page.getByRole('button', { name: 'Thêm vào lớp' }).click();

    await page.getByRole('dialog').getByRole('button', { name: 'Sao chép' }).click();

    await expect(page.getByRole('dialog').getByRole('button', { name: 'Đã sao chép' })).toBeVisible();
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('khoa@truong.edu.vn\tTam-0001-abcd');
  });

  test('email đã có tài khoản: chỉ thêm vào lớp, không có mật khẩu tạm', async ({ page }) => {
    await open(page);

    await page.getByLabel('Email học sinh').fill('co-tai-khoan@truong.edu.vn');
    await page.getByLabel('Họ tên').fill('Tên bị bỏ qua');
    await page.getByRole('button', { name: 'Thêm vào lớp' }).click();

    await expect(page.getByText('Đã thêm vào lớp: Đã Có Tài Khoản.')).toBeVisible();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    // AZ-07: tài khoản không do thủ quỹ quản lý → khóa nút sửa tên / cấp mật khẩu, vẫn ngừng được.
    const row = rowOf(page, /Đã Có Tài Khoản/);
    await expect(row.getByRole('button', { name: 'Cấp mật khẩu tạm' })).toBeDisabled();
    await expect(row.getByRole('button', { name: 'Sửa tên' })).toBeDisabled();
    await expect(row.getByRole('button', { name: 'Ngừng' })).toBeEnabled();
  });

  test('đã là thành viên: lỗi 409 hiện ngay ở ô Email', async ({ page }) => {
    await open(page);

    await page.getByLabel('Email học sinh').fill('ha.le@truong.edu.vn');
    await page.getByLabel('Họ tên').fill('Lê Thu Hà');
    await page.getByRole('button', { name: 'Thêm vào lớp' }).click();

    await expect(page.getByText('Học sinh này đã ở trong quỹ.')).toBeVisible();
    await expect(page.getByLabel('Email học sinh')).toHaveAttribute('aria-invalid', 'true');
  });

  test('kiểm tra tại form: để trống và email sai định dạng, không gọi API', async ({ page }) => {
    const api = await open(page);

    await page.getByRole('button', { name: 'Thêm vào lớp' }).click();
    await expect(page.getByText('Vui lòng nhập thông tin này.')).toHaveCount(2);

    await page.getByLabel('Email học sinh').fill('khong-phai-email');
    await page.getByLabel('Họ tên').fill('A');
    await page.getByRole('button', { name: 'Thêm vào lớp' }).click();
    await expect(page.getByText('Email không hợp lệ.')).toBeVisible();
    expect(api.last('POST', '/')).toBeUndefined();
  });

  test('quỹ đã tổng kết: báo lỗi và ẩn form thêm', async ({ page }) => {
    const api = await open(page);
    api.archived = true;

    await page.getByLabel('Email học sinh').fill('moi@truong.edu.vn');
    await page.getByLabel('Họ tên').fill('Mới');
    await page.getByRole('button', { name: 'Thêm vào lớp' }).click();

    await expect(page.getByText('Quỹ đã tổng kết cuối năm, chỉ được xem. Không thể thêm thành viên mới.')).toBeVisible();
    await expect(page.getByLabel('Email học sinh')).toHaveCount(0);
  });
});

test.describe('Thêm nhiều email (OQ-20)', () => {
  test('dán danh sách: đếm email, báo số không hợp lệ, bỏ trùng; gửi rồi hiện kết quả từng dòng và mật khẩu tạm', async ({ page }) => {
    const api = await open(page);

    await page.getByRole('button', { name: 'Thêm nhiều email' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel(/Dán danh sách email/).fill(
      'a@truong.edu.vn\nb@truong.edu.vn, co-tai-khoan@truong.edu.vn;  ha.le@truong.edu.vn\nkhong-hop-le\nA@truong.edu.vn',
    );
    await expect(dialog.locator('.bulk-count')).toContainText('5 email');
    await expect(dialog.locator('.bulk-count')).toContainText('1 không hợp lệ');
    await dialog.getByRole('button', { name: 'Thêm 5 email' }).click();

    await expect(dialog.getByText('Mật khẩu tạm chỉ hiện lúc này')).toBeVisible();
    expect(api.last('POST', '/bulk')?.body).toEqual({
      emails: ['a@truong.edu.vn', 'b@truong.edu.vn', 'co-tai-khoan@truong.edu.vn', 'ha.le@truong.edu.vn', 'khong-hop-le'],
    });
    const rows = dialog.getByRole('row');
    await expect(rows.filter({ hasText: 'a@truong.edu.vn' })).toContainText('Đã tạo tài khoản');
    await expect(rows.filter({ hasText: 'a@truong.edu.vn' })).toContainText('Tam-0001-abcd');
    await expect(rows.filter({ hasText: 'b@truong.edu.vn' })).toContainText('Tam-0002-abcd');
    await expect(rows.filter({ hasText: 'co-tai-khoan@' })).toContainText('Đã thêm vào lớp');
    await expect(rows.filter({ hasText: 'ha.le@' })).toContainText('Học sinh này đã ở trong quỹ.');
    await expect(rows.filter({ hasText: 'khong-hop-le' })).toContainText('Email không hợp lệ.');
    await expect(rows.filter({ hasText: 'co-tai-khoan@' }).locator('code')).toHaveCount(0);

    await dialog.getByRole('button', { name: 'Đóng' }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(page.getByText('Tam-0001-abcd')).toHaveCount(0);
    await expect(page.getByText('8 thành viên đang hoạt động.')).toBeVisible();
  });

  test('Sao chép tất cả mật khẩu chỉ gồm tài khoản mới tạo', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await open(page);
    await page.getByRole('button', { name: 'Thêm nhiều email' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel(/Dán danh sách email/).fill('a@truong.edu.vn\nco-tai-khoan@truong.edu.vn\nb@truong.edu.vn');
    await dialog.getByRole('button', { name: 'Thêm 3 email' }).click();

    await dialog.getByRole('button', { name: 'Sao chép tất cả mật khẩu' }).click();

    // Windows đổi LF thành CRLF khi ghi clipboard – chuẩn hóa trước khi so.
    const copied = await page.evaluate(() => navigator.clipboard.readText());
    expect(copied.replace(/\r\n/g, '\n')).toBe('a@truong.edu.vn\tTam-0001-abcd\nb@truong.edu.vn\tTam-0002-abcd');
  });

  test('quá 50 email: báo lỗi và không gọi API', async ({ page }) => {
    const api = await open(page);
    await page.getByRole('button', { name: 'Thêm nhiều email' }).click();
    const dialog = page.getByRole('dialog');

    await dialog.getByLabel(/Dán danh sách email/).fill(Array.from({ length: 51 }, (_, i) => `u${i}@truong.edu.vn`).join('\n'));
    await dialog.getByRole('button', { name: 'Thêm 51 email' }).click();

    await expect(dialog.getByText('Mỗi lần chỉ thêm tối đa 50 email. Bạn đang có 51.')).toBeVisible();
    expect(api.last('POST', '/bulk')).toBeUndefined();
  });

  test('chưa nhập gì thì nút Thêm bị khóa; Hủy bỏ đóng hộp thoại', async ({ page }) => {
    await open(page);
    await page.getByRole('button', { name: 'Thêm nhiều email' }).click();
    const dialog = page.getByRole('dialog');

    await expect(dialog.getByRole('button', { name: 'Thêm 0 email' })).toBeDisabled();
    await dialog.getByRole('button', { name: 'Hủy bỏ' }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
  });
});

test.describe('Cấp lại mật khẩu tạm, sửa tên, ngừng', () => {
  test('cấp mật khẩu tạm: xác nhận rồi hiện mật khẩu mới một lần', async ({ page }) => {
    const api = await open(page);

    await rowOf(page, /Lê Thu Hà/).getByRole('button', { name: 'Cấp mật khẩu tạm' }).click();
    await expect(page.getByRole('dialog')).toContainText('các thiết bị đang đăng nhập của họ bị đăng xuất');
    expect(api.last('POST', '/m-ha.le/reset-password')).toBeUndefined();
    await page.getByRole('dialog').getByRole('button', { name: 'Cấp mật khẩu tạm' }).click();

    await expect(page.getByRole('dialog')).toContainText('Mật khẩu tạm của Lê Thu Hà');
    await expect(page.getByRole('dialog')).toContainText('Tam-0001-abcd');
    await page.getByRole('dialog').getByRole('button', { name: 'Đã lưu, đóng' }).click();
    await expect(page.getByText('Tam-0001-abcd')).toHaveCount(0);
    await expect(rowOf(page, /Lê Thu Hà/)).toContainText('Chưa đổi mật khẩu tạm');
  });

  test('hủy xác nhận thì không cấp mật khẩu', async ({ page }) => {
    const api = await open(page);

    await rowOf(page, /Lê Thu Hà/).getByRole('button', { name: 'Cấp mật khẩu tạm' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Hủy bỏ' }).click();

    expect(api.last('POST', '/m-ha.le/reset-password')).toBeUndefined();
  });

  test('sửa họ tên: kiểm tra trống, lưu xong dòng đổi tên', async ({ page }) => {
    const api = await open(page);

    await rowOf(page, /Lê Thu Hà/).getByRole('button', { name: 'Sửa tên' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Họ và tên').fill('');
    await dialog.getByRole('button', { name: 'Lưu' }).click();
    await expect(dialog.getByText('Vui lòng nhập thông tin này.')).toBeVisible();
    expect(api.last('PUT', '/m-ha.le')).toBeUndefined();

    await dialog.getByLabel('Họ và tên').fill('  Lê Thu Hà (đổi)  ');
    await dialog.getByRole('button', { name: 'Lưu' }).click();

    await expect(page.getByText('Đã lưu họ tên.')).toBeVisible();
    await expect(rowOf(page, /Lê Thu Hà \(đổi\)/)).toBeVisible();
    expect(api.last('PUT', '/m-ha.le')?.body).toEqual({ fullName: 'Lê Thu Hà (đổi)' });
  });

  test('ngừng thành viên: hộp thoại nêu hệ quả (khoản chưa đóng bị hủy) rồi chuyển sang tab Đã ngừng', async ({ page }) => {
    const api = await open(page);
    api.impact.set('long.pham@truong.edu.vn', { unpaidCount: 2 });

    await rowOf(page, /Phạm Đức Long/).getByRole('button', { name: 'Ngừng' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('Phạm Đức Long');
    await expect(dialog).toContainText('dữ liệu lịch sử vẫn được giữ lại');
    await expect(dialog).toContainText('2 khoản chưa đóng của khoản thu đang mở sẽ bị hủy.');
    await dialog.getByRole('button', { name: 'Ngừng thành viên' }).click();

    await expect(page.getByText('Đã ngừng Phạm Đức Long.')).toBeVisible();
    await expect(rowOf(page, /Phạm Đức Long/)).toHaveCount(0); // không dùng getByText: toast cũng chứa tên
    await expect(page.getByText('4 thành viên đang hoạt động.')).toBeVisible();
    await page.getByRole('tab', { name: 'Đã ngừng' }).click();
    await expect(rowOf(page, /Phạm Đức Long/)).toContainText('Đã ngừng');
  });

  test('còn số dư cá nhân: hộp thoại nêu lý do và khóa nút ngừng (OQ-15)', async ({ page }) => {
    const api = await open(page);
    api.impact.set('ha.le@truong.edu.vn', { balance: 150_000 });

    await rowOf(page, /Lê Thu Hà/).getByRole('button', { name: 'Ngừng' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('Số dư phải về 0 (rút tiền) trước khi ngừng.');
    await expect(dialog.getByRole('button', { name: 'Ngừng thành viên' })).toBeDisabled();
    await dialog.getByRole('button', { name: 'Hủy bỏ' }).click();
    await expect(rowOf(page, /Lê Thu Hà/)).toBeVisible();
  });

  test('Hủy bỏ ở hộp thoại ngừng thì không ngừng ai', async ({ page }) => {
    const api = await open(page);

    await rowOf(page, /Lê Thu Hà/).getByRole('button', { name: 'Ngừng' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Hủy bỏ' }).click();

    expect(api.last('POST', '/m-ha.le/remove')).toBeUndefined();
    await expect(rowOf(page, /Lê Thu Hà/)).toBeVisible();
  });
});
