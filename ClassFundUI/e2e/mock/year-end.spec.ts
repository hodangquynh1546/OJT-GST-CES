import { Page, Route, expect, test } from '@playwright/test';

import { AN, FakeAuthApi, FakeUser } from '../support/fake-auth-api';
import { FUND_ID } from '../support/fake-charges-api';

/** M14-T3: S-05 Tổng kết cuối năm và banner "Quỹ đã tổng kết" với API giả lập. */

const STUDENT_PERMISSIONS = ['Fund.View', 'Event.Participate', 'Payment.Submit', 'Balance.ViewOwn', 'Balance.Withdraw'];
const TREASURER_PERMISSIONS = [...STUDENT_PERMISSIONS, 'Event.Manage', 'Charge.Manage', 'Ledger.Manage', 'Payment.Confirm', 'Settlement.Execute', 'Report.View'];

function userAs(role: 'Treasurer' | 'Student'): FakeUser {
  return {
    ...AN,
    fullName: 'Nguyễn Minh Anh',
    memberships: [{ fundId: FUND_ID, fundName: 'Lớp 10A1', role, permissions: role === 'Treasurer' ? TREASURER_PERMISSIONS : STUDENT_PERMISSIONS }],
  };
}

const preview = (partial: Record<string, unknown> = {}) => ({
  fundName: 'Lớp 10A1',
  schoolYear: '2026-2027',
  status: 'Active',
  memberCount: 3,
  totalCollected: 200000,
  totalSpent: 120000,
  surplus: 80000,
  outcome: 'Surplus',
  unsettledEventCount: 0,
  pendingPaymentCount: 0,
  openChargeCount: 1,
  unpaidObligationCount: 1,
  canExecute: true,
  lines: [
    { id: null, userId: 'u1', fullName: 'Phạm Đức Long', contributed: 100000, amount: 40000, status: null },
    { id: null, userId: 'u2', fullName: 'Lê Thu Hà', contributed: 100000, amount: 40000, status: null },
    { id: null, userId: 'u3', fullName: 'Nguyễn Minh Anh', contributed: 0, amount: 0, status: null },
  ],
  ...partial,
});

const settled = (method: string | null, status: string) => ({
  id: 's1',
  fundName: 'Lớp 10A1',
  schoolYear: '2026-2027',
  outcome: 'Surplus',
  surplusMethod: method,
  memberCount: 3,
  totalCollected: 200000,
  totalSpent: 120000,
  surplus: 80000,
  settledAt: '2026-11-21T03:00:00Z',
  settledByName: 'Nguyễn Minh Anh',
  lines: [
    { id: 'l1', userId: 'u1', fullName: 'Phạm Đức Long', contributed: 100000, amount: 40000, status },
    { id: 'l2', userId: 'u2', fullName: 'Lê Thu Hà', contributed: 100000, amount: 40000, status },
  ],
});

interface Recorded {
  method: string;
  path: string;
  body: unknown;
}

async function open(page: Page, role: 'Treasurer' | 'Student', state: { result: unknown | null; preview: unknown; archived?: boolean }, path = 'reports/year-end'): Promise<Recorded[]> {
  const requests: Recorded[] = [];
  const auth = new FakeAuthApi(page);
  auth.addAccount(userAs(role), 'Passw0rd1');
  auth.signIn(userAs(role));
  await auth.install();

  const json = (route: Route, body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
  const fund = (status: string) => ({ id: FUND_ID, name: 'Lớp 10A1', schoolYear: '2026-2027', status, createdAt: '2026-09-01T00:00:00Z', ownerName: 'Nguyễn Minh Anh', memberCount: 3, myRole: role });
  await page.route(new RegExp(`/api/v1/funds/${FUND_ID}$`), (route) => (route.request().method() === 'GET' ? json(route, fund(state.archived || state.result ? 'Archived' : 'Active')) : route.fallback()));
  await page.route(new RegExp(`/api/v1/funds/${FUND_ID}/year-end-settlement`), (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const sub = url.pathname.replace(`/api/v1/funds/${FUND_ID}/year-end-settlement`, '');
    const body = request.postData() ? JSON.parse(request.postData() as string) : null;
    requests.push({ method: request.method(), path: sub, body });

    if (request.method() === 'GET' && sub === '/preview') {
      return json(route, state.preview);
    }
    if (request.method() === 'GET' && sub === '') {
      return state.result
        ? json(route, state.result)
        : route.fulfill({ status: 404, contentType: 'application/problem+json', body: JSON.stringify({ status: 404, code: 'not_found', title: 'x' }) });
    }
    if (request.method() === 'POST' && sub === '') {
      state.result = settled(body.surplusMethod, body.surplusMethod === 'PersonalBalance' ? 'CreditedToBalance' : 'PendingRefund');
      return json(route, state.result, 201);
    }
    if (request.method() === 'POST' && sub.endsWith('/refunded')) {
      const result = state.result as ReturnType<typeof settled>;
      result.lines[0].status = 'Refunded';
      return json(route, result.lines[0]);
    }
    return route.fulfill({ status: 404, body: '{}' });
  });
  await page.goto(`/funds/${FUND_ID}/${path}`);
  return requests;
}

test.describe('S-05 Tổng kết cuối năm', () => {
  test('xem trước: tổng hợp, cảnh báo chỉ làm một lần và bảng chia cho cả lớp', async ({ page }) => {
    await open(page, 'Treasurer', { result: null, preview: preview() });

    await expect(page.getByRole('heading', { name: 'Tổng kết cuối năm' })).toBeVisible();
    await expect(page.getByText('Năm học 2026-2027')).toBeVisible();
    await expect(page.getByText('Chỉ làm một lần.').first()).toBeVisible();
    await expect(page.getByText('1 khoản chưa đóng', { exact: false })).toBeVisible();
    const table = page.getByRole('region', { name: 'Bảng chia tiền thừa' });
    await expect(table.getByRole('row', { name: /Phạm Đức Long/ })).toContainText('50%');
    await expect(table.getByRole('row', { name: /Nguyễn Minh Anh/ })).toContainText('0 đ');
    await expect(page.getByRole('button', { name: 'Tổng kết cuối năm' })).toBeDisabled();
  });

  test('chọn cách xử lý rồi tổng kết: gọi đúng API, hiện kết quả và banner quỹ đã đóng', async ({ page }) => {
    const requests = await open(page, 'Treasurer', { result: null, preview: preview() });

    await page.getByRole('radio', { name: /Cộng vào số dư cá nhân/ }).check();
    await page.getByRole('button', { name: 'Tổng kết cuối năm' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Tổng kết và đóng quỹ' }).click();

    await expect(page.getByText('Đã tổng kết lúc')).toBeVisible();
    await expect(page.getByRole('row', { name: /Lê Thu Hà/ })).toContainText('Đã cộng số dư');
    expect(requests.find((item) => item.method === 'POST' && item.path === '')?.body).toEqual({ surplusMethod: 'PersonalBalance' });
    await expect(page.getByRole('status').filter({ hasText: 'Quỹ đã tổng kết cuối năm – chỉ xem.' })).toBeVisible();
  });

  test('còn sự kiện chưa xong hoặc thanh toán chờ: chặn nút tổng kết', async ({ page }) => {
    await open(page, 'Treasurer', { result: null, preview: preview({ unsettledEventCount: 2, pendingPaymentCount: 1, canExecute: false }) });

    await expect(page.getByText('Còn 2 sự kiện chưa quyết toán hoặc chưa hủy')).toBeVisible();
    await expect(page.getByText('Còn 1 thanh toán chờ xác nhận')).toBeVisible();
    await page.getByRole('radio', { name: /Hoàn tiền trực tiếp/ }).check();
    await expect(page.getByRole('button', { name: 'Tổng kết cuối năm' })).toBeDisabled();
  });

  test('hoàn trực tiếp: đánh dấu Đã hoàn từng người khi quỹ đã đóng', async ({ page }) => {
    const requests = await open(page, 'Treasurer', { result: settled('DirectRefund', 'PendingRefund'), preview: null });

    await expect(page.getByText('Hoàn tiền trực tiếp').first()).toBeVisible();
    await page.getByRole('row', { name: /Phạm Đức Long/ }).getByRole('button', { name: 'Đã hoàn' }).click();

    await expect(page.getByText('Đã ghi nhận hoàn Phạm Đức Long.')).toBeVisible();
    expect(requests.some((item) => item.method === 'POST' && item.path === '/lines/l1/refunded')).toBe(true);
    await expect(page.getByRole('row', { name: /Phạm Đức Long/ })).toContainText('Đã hoàn');
  });
});

test.describe('Banner quỹ đã tổng kết', () => {
  test('hiện trên các trang khác của quỹ và nói rõ những gì vẫn làm được', async ({ page }) => {
    await open(page, 'Student', { result: null, preview: null, archived: true }, 'balance');

    const banner = page.getByRole('status').filter({ hasText: 'Quỹ đã tổng kết cuối năm – chỉ xem.' });
    await expect(banner).toBeVisible();
    await expect(banner).toContainText('rút số dư cá nhân');
  });

  test('quỹ đang hoạt động thì không có banner', async ({ page }) => {
    await open(page, 'Student', { result: null, preview: null }, 'balance');

    await expect(page.getByRole('navigation', { name: 'Điều hướng chính' })).toBeVisible();
    await expect(page.getByText('Quỹ đã tổng kết cuối năm – chỉ xem.')).toHaveCount(0);
  });
});
