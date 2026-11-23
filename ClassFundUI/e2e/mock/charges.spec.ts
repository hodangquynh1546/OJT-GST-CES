import { Page, expect, test } from '@playwright/test';

import { AN, FakeAuthApi, FakeUser } from '../support/fake-auth-api';
import { FUND_ID, FakeCharge, FakeChargesApi, FakeMine, FakeRow } from '../support/fake-charges-api';

/** Các màn H-01, H-01b, H-02, H-03 với API giả lập: học sinh chỉ xem, thủ quỹ tích "đã thu" (docs/testing/test-strategy.md). */

const STUDENT_PERMISSIONS = ['Fund.View', 'Event.Participate', 'Balance.ViewOwn'];
const TREASURER_PERMISSIONS = [
  ...STUDENT_PERMISSIONS,
  'Fund.Manage',
  'Member.Manage',
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

function userAs(role: 'Treasurer' | 'Student'): FakeUser {
  return {
    ...AN,
    fullName: 'Nguyễn Minh Anh',
    memberships: [{ fundId: FUND_ID, fundName: 'Lớp 10A1', role, permissions: role === 'Treasurer' ? TREASURER_PERMISSIONS : STUDENT_PERMISSIONS }],
  };
}

const charge = (partial: Partial<FakeCharge> & Pick<FakeCharge, 'id' | 'title'>): FakeCharge => ({
  purpose: 'Chi tiêu chung học kỳ 1',
  amountPerPerson: 200000,
  dueDate: '2026-12-30',
  status: 'Open',
  createdAt: '2026-11-10T01:00:00Z',
  categoryId: 'cat1',
  categoryName: 'Quỹ lớp',
  progress: { assignedCount: 32, paidCount: 26, collectedAmount: 5200000 },
  mine: null,
  ...partial,
});

const mine = (partial: Partial<FakeMine> & Pick<FakeMine, 'assignmentId' | 'title'>): FakeMine => ({
  chargeId: 'c1',
  purpose: null,
  amountDue: 200000,
  dueDate: '2026-12-30',
  chargeStatus: 'Open',
  status: 'Unpaid',
  method: null,
  paidAt: null,
  isSelfDeclared: false,
  ...partial,
});

const row = (id: string, name: string, partial: Partial<FakeRow> = {}): FakeRow => ({
  assignmentId: `a-${id}`,
  userId: `u-${id}`,
  fullName: name,
  email: `${id}@truong.edu.vn`,
  role: 'Student',
  amountDue: 200000,
  status: 'Unpaid',
  isSelfDeclared: false,
  paymentId: null,
  method: null,
  paymentAt: null,
  hasProof: false,
  rejectionReason: null,
  ...partial,
});

async function openPage(page: Page, role: 'Treasurer' | 'Student', path: string, api: (page: Page) => FakeChargesApi): Promise<FakeChargesApi> {
  const auth = new FakeAuthApi(page);
  auth.addAccount(userAs(role), 'Passw0rd1');
  auth.signIn(userAs(role));
  await auth.install();
  const fake = api(page);
  await fake.install();
  await page.goto(`/funds/${FUND_ID}/${path}`);
  return fake;
}

test.describe('H-01 Khoản thu chung', () => {
  test('thủ quỹ thấy tiến độ thu, không có hạn và nút Theo dõi; tab Đã đóng tách riêng', async ({ page }) => {
    await openPage(page, 'Treasurer', 'charges', (p) => new FakeChargesApi(p, [charge({ id: 'c1', title: 'Quỹ lớp HK1', dueDate: null }), charge({ id: 'c2', title: 'Quỹ cũ', status: 'Closed', dueDate: null })]));

    await expect(page.getByRole('heading', { name: 'Khoản thu chung' })).toBeVisible();
    const item = page.getByRole('article').filter({ hasText: 'Quỹ lớp HK1' });
    await expect(item).toContainText('200.000 đ/người');
    await expect(item).toContainText('không có hạn');
    await expect(item).toContainText('26/32 đã nộp');
    await expect(item).not.toContainText('chờ xác nhận');
    await expect(item.getByRole('link', { name: 'Theo dõi' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'Đang mở 1' })).toBeVisible();

    await page.getByRole('tab', { name: 'Đã đóng 1' }).click();
    await expect(page.getByRole('article').filter({ hasText: 'Quỹ cũ' })).toBeVisible();
    await expect(page.getByText('Quỹ lớp HK1')).toHaveCount(0);
  });

  test('học sinh chỉ xem: không có Tạo khoản thu / Theo dõi / Nộp tiền', async ({ page }) => {
    await openPage(page, 'Student', 'charges', (p) =>
      new FakeChargesApi(p, [charge({ id: 'c1', title: 'Quỹ lớp HK1', progress: null, mine: { assignmentId: 'a1', status: 'Unpaid', amountDue: 200000 } })], [mine({ assignmentId: 'a1', title: 'Quỹ lớp HK1' })]),
    );

    await expect(page.getByRole('button', { name: '+ Tạo khoản thu' })).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Theo dõi' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Nộp tiền' })).toHaveCount(0);
    const item = page.getByRole('article').filter({ hasText: 'Quỹ lớp HK1' });
    await expect(item).toContainText('Của tôi: Chưa nộp');
    await expect(item).not.toContainText('đã nộp'); // không lộ tiến độ thu cho học sinh
  });

  test('H-02: kiểm tra ô trống, không có ô hạn đóng, rồi tạo khoản thu không hạn', async ({ page }) => {
    const api = await openPage(page, 'Treasurer', 'charges', (p) => new FakeChargesApi(p, [], [], [], 32));

    await page.getByRole('button', { name: '+ Tạo khoản thu' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Tạo khoản thu' }).click();
    await expect(dialog.getByText('Vui lòng nhập tên khoản thu.')).toBeVisible();
    await expect(dialog.getByText('Vui lòng chọn danh mục thu.')).toBeVisible();
    await expect(dialog.getByText('Số tiền mỗi người phải lớn hơn 0.')).toBeVisible();
    await expect(dialog.getByLabel(/Hạn đóng/)).toHaveCount(0);
    expect(api.count('POST', '/charges')).toBe(0);

    await dialog.getByLabel(/Tên khoản thu/).fill('Quỹ vệ sinh lớp HK1');
    await dialog.getByLabel(/Danh mục thu/).selectOption({ label: 'Quỹ lớp' });
    await dialog.getByLabel(/Số tiền mỗi người/).fill('20000');
    await expect(dialog).toContainText('Áp cho 32 thành viên → tổng phải thu 640.000 đ');
    await dialog.getByRole('button', { name: 'Tạo khoản thu' }).click();

    await expect(page.getByText('Đã tạo khoản thu “Quỹ vệ sinh lớp HK1”.')).toBeVisible();
    await expect(page.getByRole('article').filter({ hasText: 'Quỹ vệ sinh lớp HK1' })).toBeVisible();
    expect(api.last('POST', '/charges')?.body).toEqual({ title: 'Quỹ vệ sinh lớp HK1', purpose: null, amountPerPerson: 20000, dueDate: null, categoryId: 'cat1' });
  });
});

test.describe('H-01b Theo dõi khoản thu (thủ quỹ tích "đã thu")', () => {
  const open = (p: Page) =>
    new FakeChargesApi(p, [charge({ id: 'c1', title: 'Quỹ lớp HK1', dueDate: null })], [], [row('1', 'Phạm Đức Long'), row('2', 'Lê Thu Hà', { status: 'Paid', method: 'Cash', paymentAt: '2026-11-18T01:00:00Z' })]);

  test('tích đã thu chỉ có Tiền mặt / Chuyển khoản, rồi dòng chuyển sang Đã nộp', async ({ page }) => {
    const api = await openPage(page, 'Treasurer', 'charges/c1', open);

    const dong = page.getByRole('row').filter({ hasText: 'Phạm Đức Long' });
    await expect(dong).toContainText('Chưa nộp');
    await dong.getByRole('button', { name: 'Đã thu' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText('Tiền mặt')).toBeVisible();
    await expect(dialog.getByText('Chuyển khoản', { exact: true })).toBeVisible();
    await expect(dialog).not.toContainText('Dùng số dư');
    await dialog.getByText('Chuyển khoản', { exact: true }).click();
    await dialog.getByLabel(/Mã giao dịch/).fill('FT123');
    await dialog.getByRole('button', { name: 'Xác nhận đã thu' }).click();

    await expect(page.getByText('Đã ghi nhận Phạm Đức Long đã nộp.')).toBeVisible();
    expect(api.last('POST', '/assignments/a-1/collect')?.body).toEqual({ method: 'BankTransfer', transferCode: 'FT123' });
    await expect(page.getByRole('row').filter({ hasText: 'Phạm Đức Long' })).toContainText('Đã nộp');
  });

  test('bỏ xác nhận thu bắt buộc có lý do', async ({ page }) => {
    const api = await openPage(page, 'Treasurer', 'charges/c1', open);

    await page.getByRole('row').filter({ hasText: 'Lê Thu Hà' }).getByRole('button', { name: 'Bỏ xác nhận thu' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('button', { name: 'Bỏ xác nhận thu' })).toBeDisabled();
    await dialog.getByLabel('Lý do').fill('Tích nhầm người');
    await dialog.getByRole('button', { name: 'Bỏ xác nhận thu' }).click();

    await expect(page.getByText('Đã bỏ xác nhận thu của Lê Thu Hà.')).toBeVisible();
    expect(api.last('POST', '/assignments/a-2/uncollect')?.body).toEqual({ reason: 'Tích nhầm người' });
    await expect(page.getByRole('row').filter({ hasText: 'Lê Thu Hà' })).toContainText('Chưa nộp');
  });

  test('"Nhắc đóng" từng người và lỗi 409 nhắc quá sớm hiện thông báo tiếng Việt', async ({ page }) => {
    const api = await openPage(page, 'Treasurer', 'charges/c1', open);
    const dong = page.getByRole('row').filter({ hasText: 'Phạm Đức Long' });

    await dong.getByRole('button', { name: 'Nhắc đóng' }).click();
    await expect(page.getByText('Đã nhắc Phạm Đức Long đóng khoản này.')).toBeVisible();
    expect(api.last('POST', '/assignments/a-1/remind')).toBeTruthy();

    api.failNext = { status: 409, code: 'charge.remind_too_soon', title: 'x' };
    await dong.getByRole('button', { name: 'Nhắc đóng' }).click();
    await expect(page.getByText('Vừa nhắc xong. Vui lòng chờ ít nhất 1 giờ rồi nhắc lại.')).toBeVisible();
  });
});

test.describe('H-03 Khoản cần đóng của tôi (chỉ xem)', () => {
  test('tách Chưa nộp / Đã nộp, không có nút nộp tiền', async ({ page }) => {
    await openPage(page, 'Student', 'my-payments', (p) =>
      new FakeChargesApi(p, [], [
        mine({ assignmentId: 'a1', title: 'Quỹ photo HK1', amountDue: 30000, dueDate: null }),
        mine({ assignmentId: 'a2', title: 'Áo lớp', amountDue: 200000 }),
        mine({ assignmentId: 'a4', title: 'Quỹ đầu năm', amountDue: 400000, status: 'Paid', method: 'BankTransfer' }),
      ]),
    );

    await expect(page.locator('.stat').filter({ hasText: 'Chưa nộp' })).toContainText('230.000 đ');
    await expect(page.locator('.stat').filter({ hasText: 'Đã nộp' })).toContainText('400.000 đ');
    await expect(page.locator('.stat').filter({ hasText: 'Chờ thủ quỹ xác nhận' })).toHaveCount(0);
    const pending = page.getByRole('region', { name: 'Chưa nộp' });
    await expect(pending.getByText('Quỹ photo HK1')).toBeVisible();
    await expect(pending.getByText('Không có hạn')).toBeVisible();
    await expect(page.getByRole('button', { name: /Nộp tiền|Gửi lại/ })).toHaveCount(0);
    await expect(page.getByRole('region', { name: 'Đã nộp' }).getByText('Quỹ đầu năm')).toBeVisible();
  });
});

test.describe('Phân quyền', () => {
  test('trang Xác nhận thanh toán không còn: đường dẫn /payments không tồn tại', async ({ page }) => {
    await openPage(page, 'Treasurer', 'charges', (p) => new FakeChargesApi(p, []));
    await expect(page.getByRole('link', { name: 'Xác nhận thanh toán' })).toHaveCount(0);
  });
});
