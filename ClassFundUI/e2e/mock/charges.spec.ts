import { Page, expect, test } from '@playwright/test';

import { AN, FakeAuthApi, FakeUser } from '../support/fake-auth-api';
import { FUND_ID, FakeCharge, FakeChargesApi, FakeMine, FakeQueueItem } from '../support/fake-charges-api';

/** M6a-T4: các màn H-01…H-04 với API giả lập (docs/testing/test-strategy.md). */

const STUDENT_PERMISSIONS = ['Fund.View', 'Event.Participate', 'Payment.Submit', 'Balance.ViewOwn', 'Balance.Withdraw'];
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
  progress: { assignedCount: 32, paidCount: 26, awaitingCount: 2, collectedAmount: 5200000 },
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
  paymentId: null,
  method: null,
  paymentStatus: null,
  rejectionReason: null,
  isSelfDeclared: false,
  ...partial,
});

const queueItem = (id: string, name: string): FakeQueueItem => ({
  id,
  assignmentId: `a-${id}`,
  chargeId: 'c1',
  chargeTitle: 'Quỹ lớp HK1',
  payerUserId: `u-${id}`,
  payerName: name,
  payerEmail: `${id}@truong.edu.vn`,
  amount: 200000,
  method: 'Cash',
  transferCode: null,
  status: 'Submitted',
  submittedAt: '2026-11-16T13:14:00Z',
  confirmedAt: null,
  isSelfDeclared: false,
  rejectionReason: null,
  attachments: [],
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
  test('thủ quỹ thấy tiến độ thu và nút Theo dõi; tab Đã đóng tách riêng', async ({ page }) => {
    await openPage(page, 'Treasurer', 'charges', (p) => new FakeChargesApi(p, [charge({ id: 'c1', title: 'Quỹ lớp HK1' }), charge({ id: 'c2', title: 'Quỹ cũ', status: 'Closed' })]));

    await expect(page.getByRole('heading', { name: 'Khoản thu chung' })).toBeVisible();
    const row = page.getByRole('article').filter({ hasText: 'Quỹ lớp HK1' });
    await expect(row).toContainText('200.000 đ/người');
    await expect(row).toContainText('26/32 đã đóng');
    await expect(row).toContainText('2 chờ xác nhận');
    await expect(row.getByRole('link', { name: 'Theo dõi' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'Đang mở 1' })).toBeVisible();

    await page.getByRole('tab', { name: 'Đã đóng 1' }).click();
    await expect(page.getByRole('article').filter({ hasText: 'Quỹ cũ' })).toBeVisible();
    await expect(page.getByText('Quỹ lớp HK1')).toHaveCount(0);
  });

  test('học sinh không có nút Tạo khoản thu / Theo dõi, nhưng nộp tiền được ngay trên dòng', async ({ page }) => {
    const api = await openPage(page, 'Student', 'charges', (p) =>
      new FakeChargesApi(p, [charge({ id: 'c1', title: 'Quỹ lớp HK1', progress: null, mine: { assignmentId: 'a1', status: 'Unpaid', amountDue: 200000 } })], [mine({ assignmentId: 'a1', title: 'Quỹ lớp HK1' })]),
    );

    await expect(page.getByRole('button', { name: '+ Tạo khoản thu' })).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Theo dõi' })).toHaveCount(0);
    const row = page.getByRole('article').filter({ hasText: 'Quỹ lớp HK1' });
    await expect(row).toContainText('Của tôi: Chưa đóng');
    await expect(row).not.toContainText('đã đóng'); // không lộ tiến độ thu cho học sinh

    await row.getByRole('button', { name: 'Nộp tiền' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận đã nộp' }).click();
    await expect(page.getByText('Đã gửi. Thủ quỹ sẽ xác nhận khoản đóng của bạn.')).toBeVisible();
    expect(api.last('POST', '/me/assignments/a1/payments')?.body).toEqual({ method: 'Cash', transferCode: null, attachmentId: null });
  });

  test('H-02: kiểm tra ô trống, hiện số thành viên và tổng phải thu, rồi tạo khoản thu', async ({ page }) => {
    const api = await openPage(page, 'Treasurer', 'charges', (p) => new FakeChargesApi(p, [], [], [], 32));

    await page.getByRole('button', { name: '+ Tạo khoản thu' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Tạo khoản thu' }).click();
    await expect(dialog.getByText('Vui lòng nhập tên khoản thu.')).toBeVisible();
    await expect(dialog.getByText('Vui lòng chọn danh mục thu.')).toBeVisible();
    await expect(dialog.getByText('Số tiền mỗi người phải lớn hơn 0.')).toBeVisible();
    await expect(dialog.getByText('Vui lòng chọn hạn đóng.')).toBeVisible();
    expect(api.count('POST', '/charges')).toBe(0);

    await dialog.getByLabel(/Tên khoản thu/).fill('Quỹ vệ sinh lớp HK1');
    await dialog.getByLabel(/Danh mục thu/).selectOption({ label: 'Quỹ lớp' });
    await dialog.getByLabel(/Số tiền mỗi người/).fill('20000');
    await dialog.getByLabel(/Hạn đóng/).fill('2099-12-10');
    await expect(dialog).toContainText('Áp cho 32 thành viên → tổng phải thu 640.000 đ');
    await dialog.getByRole('button', { name: 'Tạo khoản thu' }).click();

    await expect(page.getByText('Đã tạo khoản thu “Quỹ vệ sinh lớp HK1”.')).toBeVisible();
    await expect(page.getByRole('article').filter({ hasText: 'Quỹ vệ sinh lớp HK1' })).toBeVisible();
    expect(api.last('POST', '/charges')?.body).toEqual({ title: 'Quỹ vệ sinh lớp HK1', purpose: null, amountPerPerson: 20000, dueDate: '2099-12-10', categoryId: 'cat1' });
  });
});

test.describe('H-03 Khoản cần đóng của tôi', () => {
  test('tách Cần đóng / Lịch sử, nêu lý do bị từ chối và cho gửi lại', async ({ page }) => {
    await openPage(page, 'Student', 'my-payments', (p) =>
      new FakeChargesApi(p, [], [
        mine({ assignmentId: 'a1', title: 'Quỹ photo HK1', amountDue: 30000 }),
        mine({ assignmentId: 'a2', title: 'Áo lớp', status: 'Unpaid', paymentStatus: 'Rejected', rejectionReason: 'Ảnh bill không thấy số tiền' }),
        mine({ assignmentId: 'a3', title: 'Quỹ lớp HK1', status: 'AwaitingConfirmation', method: 'BankTransfer', paymentStatus: 'Submitted' }),
        mine({ assignmentId: 'a4', title: 'Quỹ đầu năm', amountDue: 400000, status: 'Paid', method: 'BankTransfer', paymentStatus: 'Confirmed' }),
      ]),
    );

    await expect(page.locator('.stat').filter({ hasText: 'Còn phải đóng' })).toContainText('230.000 đ');
    await expect(page.locator('.stat').filter({ hasText: 'Chờ thủ quỹ xác nhận' })).toContainText('200.000 đ');
    const open = page.getByRole('region', { name: 'Cần đóng' });
    await expect(open.getByText('Quỹ photo HK1')).toBeVisible();
    await expect(open.getByText('Bị từ chối: “Ảnh bill không thấy số tiền” – vui lòng gửi lại')).toBeVisible();
    await expect(open.getByRole('button', { name: 'Gửi lại' })).toHaveCount(1);
    await expect(open.getByRole('button', { name: 'Nộp tiền' })).toHaveCount(1);
    await expect(page.getByRole('region', { name: 'Lịch sử đã đóng' }).getByText('Quỹ đầu năm')).toBeVisible();
  });

  test('chuyển khoản: nút gửi bị khóa tới khi có ảnh minh chứng; Hủy không gọi API', async ({ page }) => {
    const api = await openPage(page, 'Student', 'my-payments', (p) => new FakeChargesApi(p, [], [mine({ assignmentId: 'a1', title: 'Quỹ photo HK1' })]));

    await page.getByRole('button', { name: 'Nộp tiền' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByText('Chuyển khoản', { exact: true }).click();
    await expect(dialog.getByRole('button', { name: 'Xác nhận đã nộp' })).toBeDisabled();
    await expect(dialog.getByText('bắt buộc')).toBeVisible();
    await dialog.getByRole('button', { name: 'Hủy' }).click();
    await expect(dialog).toHaveCount(0);
    expect(api.count('POST', '/payments')).toBe(0);
  });

  test('lỗi 409 từ backend hiện thông báo tiếng Việt ngay trong hộp thoại', async ({ page }) => {
    const api = await openPage(page, 'Student', 'my-payments', (p) => new FakeChargesApi(p, [], [mine({ assignmentId: 'a1', title: 'Quỹ photo HK1' })]));
    api.failNext = { status: 409, code: 'payment.not_payable', title: 'x' };

    await page.getByRole('button', { name: 'Nộp tiền' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận đã nộp' }).click();

    await expect(page.getByRole('dialog').getByRole('alert')).toContainText('Khoản này đã nộp hoặc đang chờ thủ quỹ xác nhận.');
  });
});

test.describe('H-04 Xác nhận thanh toán', () => {
  test('xác nhận: khoản rời hàng chờ và gọi đúng API', async ({ page }) => {
    const api = await openPage(page, 'Treasurer', 'payments', (p) => new FakeChargesApi(p, [], [], [queueItem('p1', 'Phạm Đức Long'), queueItem('p2', 'Lê Thu Hà')]));

    await expect(page.getByText('2 chờ')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Phạm Đức Long · Quỹ lớp HK1' })).toBeVisible();
    await page.getByRole('button', { name: 'Xác nhận đã nhận tiền' }).click();

    await expect(page.getByText('Đã xác nhận khoản đóng của Phạm Đức Long.')).toBeVisible();
    await expect(page.getByRole('button', { name: /Phạm Đức Long/ })).toHaveCount(0);
    expect(api.last('POST', '/payments/p1/confirm')).toBeTruthy();
    await expect(page.getByRole('heading', { name: 'Lê Thu Hà · Quỹ lớp HK1' })).toBeVisible();
  });

  test('từ chối bắt buộc có lý do và không gọi API khi để trống', async ({ page }) => {
    const api = await openPage(page, 'Treasurer', 'payments', (p) => new FakeChargesApi(p, [], [], [queueItem('p1', 'Phạm Đức Long')]));

    await page.getByRole('button', { name: 'Từ chối', exact: true }).click();
    await expect(page.getByText('Vui lòng nhập lý do từ chối.')).toBeVisible();
    expect(api.count('POST', '/reject')).toBe(0);

    await page.getByLabel(/Lý do từ chối/).fill('Sai số tiền');
    await page.getByRole('button', { name: 'Từ chối', exact: true }).click();
    await expect(page.getByText('Đã từ chối khoản đóng của Phạm Đức Long.')).toBeVisible();
    expect(api.last('POST', '/payments/p1/reject')?.body).toEqual({ reason: 'Sai số tiền' });
  });

  test('hàng chờ trống hiện hướng dẫn; học sinh vào thẳng đường dẫn bị chuyển tới trang "Không có quyền"', async ({ page }) => {
    await openPage(page, 'Treasurer', 'payments', (p) => new FakeChargesApi(p, [], [], []));
    await expect(page.getByText('Không có thanh toán nào đang chờ')).toBeVisible();

    const student = await page.context().newPage();
    const auth = new FakeAuthApi(student);
    auth.addAccount(userAs('Student'), 'Passw0rd1');
    auth.signIn(userAs('Student'));
    await auth.install();
    await student.goto(`/funds/${FUND_ID}/payments`);
    await expect(student).toHaveURL(/\/forbidden$/);
  });
});
