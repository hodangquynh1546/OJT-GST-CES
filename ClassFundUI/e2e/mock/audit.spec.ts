import { Page, Request, expect, test } from '@playwright/test';

import { AN, FakeAuthApi, FakeUser } from '../support/fake-auth-api';

/** M12-T3: U-01 Nhật ký hoạt động (API giả lập, dữ liệu như canvas Audit.dc.html). */

const FUND_ID = 'f1';
const OWNER_PERMISSIONS = [
  'Fund.View', 'Fund.Manage', 'Member.Manage', 'Treasurer.Manage', 'Category.Manage', 'Event.Manage', 'Event.Participate',
  'Charge.Manage', 'Ledger.Manage', 'Payment.Submit', 'Payment.Confirm', 'Settlement.Execute', 'Balance.ViewOwn',
  'Balance.Withdraw', 'Balance.Handle', 'Report.View', 'Audit.View',
];
const STUDENT_PERMISSIONS = ['Fund.View', 'Event.Participate', 'Payment.Submit', 'Balance.ViewOwn', 'Balance.Withdraw'];

const owner: FakeUser = {
  ...AN,
  fullName: 'Nguyễn Minh Anh',
  memberships: [{ fundId: FUND_ID, fundName: 'Lớp 10A1', role: 'Owner', permissions: OWNER_PERMISSIONS }],
};
const student: FakeUser = {
  ...AN,
  fullName: 'Lê Thu Hà',
  memberships: [{ fundId: FUND_ID, fundName: 'Lớp 10A1', role: 'Student', permissions: STUDENT_PERMISSIONS }],
};

const ANH = 'u-anh';
const HUY = 'u-huy';

function log(partial: Record<string, unknown>) {
  return {
    id: crypto.randomUUID(),
    actorUserId: ANH,
    actorName: 'Nguyễn Minh Anh',
    scope: 'General',
    eventId: null,
    eventName: null,
    entityType: null,
    entityId: null,
    isSelfAction: false,
    note: null,
    before: null,
    after: null,
    ipAddress: '113.190.xx.xx',
    ...partial,
  };
}

// Thời điểm UTC = giờ VN − 7.
const LOGS = [
  log({ at: '2026-11-17T02:40:00Z', action: 'ledger.reverse', summary: '+650.000 đ · lý do: ghi nhầm số tiền', before: '{"status":"Hiệu lực"}', after: '{"status":"Đã đảo","reversedBy":"LE-0192"}' }),
  log({ at: '2026-11-16T13:30:00Z', actorUserId: HUY, actorName: 'Trần Quang Huy', action: 'payment.confirm', scope: 'Event', eventId: 'e1', eventName: 'Dã ngoại Ba Vì', summary: 'Hoàng Gia Bảo · 350.000 đ · phương thức: Tiền mặt', before: '{"status":"Submitted"}', after: '{"status":"Confirmed","mode":"Verified"}' }),
  log({ at: '2026-11-16T11:42:00Z', actorUserId: HUY, actorName: 'Trần Quang Huy', action: 'category.create', summary: 'Văn nghệ · loại: Chi', after: '{"Name":"Văn nghệ","type":"Expense"}' }),
  log({ at: '2026-11-15T14:10:00Z', action: 'fund.update', summary: 'Lớp 10A1 → Lớp 10A1 – THPT A', before: '{"Name":"Lớp 10A1"}', after: '{"Name":"Lớp 10A1 – THPT A"}' }),
  log({ at: '2026-11-15T00:55:00Z', action: 'payment.self_declare', isSelfAction: true, summary: 'Quỹ lớp HK1 · 200.000 đ · phương thức: Tiền mặt', before: '{"status":"Unpaid"}', after: '{"status":"Paid","mode":"SelfDeclared"}' }),
  log({ at: '2026-11-14T11:00:00Z', action: 'treasurer.grant', summary: 'Trần Quang Huy · vai trò: Học sinh → Thủ quỹ', before: '{"role":"Student"}', after: '{"role":"Treasurer"}' }),
  log({ at: '2026-11-14T10:58:00Z', action: 'event.respond', scope: 'Event', eventId: 'e1', eventName: 'Dã ngoại Ba Vì', isSelfAction: true, summary: 'trả lời: Tham gia', before: '{"response":"Pending"}', after: '{"response":"Accepted"}' }),
];

async function open(page: Page, user: FakeUser = owner, logs = LOGS) {
  const auth = new FakeAuthApi(page);
  auth.addAccount(user, 'Passw0rd1');
  auth.signIn(user);
  await auth.install();
  const requests: URL[] = [];
  await page.route(`**/api/v1/funds/${FUND_ID}/audit-logs/actors`, (route) =>
    route.fulfill({ json: [{ userId: ANH, fullName: 'Nguyễn Minh Anh' }, { userId: HUY, fullName: 'Trần Quang Huy' }] }),
  );
  await page.route(`**/api/v1/funds/${FUND_ID}/audit-logs?**`, (route) => {
    const url = new URL(route.request().url());
    requests.push(url);
    const query = url.searchParams;
    let items = logs.filter(
      (item) =>
        (!query.get('scope') || item.scope === query.get('scope')) &&
        (!query.get('actorId') || item.actorUserId === query.get('actorId')) &&
        (query.get('selfOnly') !== 'true' || item.isSelfAction),
    );
    const total = items.length;
    const pageNumber = Number(query.get('page') ?? '1');
    const pageSize = Number(query.get('pageSize') ?? '20');
    items = items.slice((pageNumber - 1) * pageSize, pageNumber * pageSize);
    return route.fulfill({ json: { items, total, page: pageNumber, pageSize } });
  });
  await page.goto(`/funds/${FUND_ID}/audit`);
  return requests;
}

const lastQuery = (requests: URL[]) => requests[requests.length - 1].searchParams;

test('U-01 lists audit entries like the canvas and filters by scope, actor, self', async ({ page }) => {
  const requests = await open(page);

  await expect(page.getByRole('heading', { name: 'Nhật ký hoạt động' })).toBeVisible();
  const grid = page.getByRole('grid');
  await expect(grid.getByRole('row')).toHaveCount(8); // tiêu đề + 7
  const first = grid.getByRole('row').nth(1);
  await expect(first).toContainText('17/11 09:40');
  await expect(first).toContainText('Nguyễn Minh Anh');
  await expect(first).toContainText('Tạo bút toán đảo'); // nhãn M7 (TD-14)
  await expect(grid.getByRole('row', { name: /Phân quyền thủ quỹ/ })).toContainText('Học sinh → Thủ quỹ');
  await expect(grid.getByRole('row', { name: /payment\.confirm/ })).toContainText('Sự kiện: Dã ngoại Ba Vì');
  await expect(grid.getByText('Tự ghi nhận')).toHaveCount(2);
  await expect(page.getByText('7 bản ghi')).toBeVisible();
  await page.screenshot({ path: 'test-results/U-01.png', fullPage: true });

  // Mặc định: từ ngày 1 của tháng tới hôm nay.
  expect(lastQuery(requests).get('from')).toMatch(/^\d{4}-\d{2}-01$/);
  expect(lastQuery(requests).get('to')).toMatch(/^\d{4}-\d{2}-\d{2}$/);

  await page.getByRole('radio', { name: 'Sự kiện' }).click();
  await expect(page.getByText('2 bản ghi')).toBeVisible();
  expect(lastQuery(requests).get('scope')).toBe('Event');

  await page.getByRole('radio', { name: 'Tất cả' }).click();
  await page.getByLabel('Chỉ thao tác tự ghi nhận').check();
  await expect(page.getByText('2 bản ghi')).toBeVisible();
  expect(lastQuery(requests).get('selfOnly')).toBe('true');
  await page.getByLabel('Chỉ thao tác tự ghi nhận').uncheck();

  await page.getByRole('combobox', { name: 'Người thực hiện' }).click();
  await page.getByRole('option', { name: 'Trần Quang Huy' }).click();
  await expect(page.getByText('2 bản ghi')).toBeVisible();
  expect(lastQuery(requests).get('actorId')).toBe(HUY);
});

test('U-01 shows before / after in a dialog', async ({ page }) => {
  await open(page);

  await page.getByRole('row', { name: /payment\.confirm/ }).getByRole('button', { name: 'Trước / sau' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText('Trần Quang Huy · 16/11 20:30 · IP 113.190.xx.xx');
  const table = dialog.getByRole('table', { name: 'So sánh trước và sau' });
  await expect(table.getByRole('row', { name: /Trạng thái/ })).toContainText('Chờ xác nhận');
  await expect(table.getByRole('row', { name: /Trạng thái/ })).toContainText('Đã xác nhận');
  await expect(table.getByRole('row', { name: /Trạng thái/ })).toContainText('Đã đổi');
  await expect(table.getByRole('row', { name: /Cách xác nhận/ })).toContainText('Đã đối chiếu');
  await expect(dialog).not.toContainText('{');
  await page.screenshot({ path: 'test-results/U-01-diff.png' });
  await dialog.getByRole('button', { name: 'Đóng' }).click();

  // Tạo mới (không có "trước"): một cột giá trị ghi nhận, giá trị đã dịch.
  await page.getByRole('row', { name: /Thêm danh mục/ }).getByRole('button', { name: 'Trước / sau' }).click();
  await expect(page.getByRole('dialog').getByRole('columnheader', { name: 'Giá trị ghi nhận' })).toBeVisible();
  await expect(page.getByRole('dialog').getByRole('row', { name: /Loại/ })).toContainText('Chi');
  await dialog.getByRole('button', { name: 'Đóng' }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
});

test('U-01 pages with Trước / Sau and shows an empty state', async ({ page }) => {
  const many = Array.from({ length: 45 }, (_, index) =>
    log({ at: new Date(Date.UTC(2026, 10, 10, 1, index)).toISOString(), action: 'fund.update', summary: `Lần ${index + 1}` }),
  );
  const requests = await open(page, owner, many);

  await expect(page.getByText('45 bản ghi · trang 1/3')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Trước', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Sau', exact: true }).click();
  await expect(page.getByText('45 bản ghi · trang 2/3')).toBeVisible();
  expect(lastQuery(requests).get('page')).toBe('2');

  // Đổi bộ lọc → về trang 1; không có kết quả → trạng thái rỗng.
  await page.getByRole('radio', { name: 'Sự kiện' }).click();
  await expect(page.getByText('Không có thao tác phù hợp')).toBeVisible();
  expect(lastQuery(requests).get('page')).toBe('1');
});

test('U-01 is not available to students (TC-M12-01)', async ({ page }) => {
  const requests: Request[] = [];
  page.on('request', (request) => {
    if (request.url().includes('/audit-logs')) {
      requests.push(request);
    }
  });
  await open(page, student);

  await expect(page).toHaveURL(/\/forbidden$/);
  expect(requests).toHaveLength(0);
});
