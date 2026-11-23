import { Page, Route, expect, test } from '@playwright/test';

import { AN, FakeAuthApi, FakeUser } from '../support/fake-auth-api';
import { FUND_ID } from '../support/fake-charges-api';

/** M9-T3: E-04 Quyết toán sự kiện với API giả lập (xem trước, chọn cách trả, quyết toán, đánh dấu đã hoàn, học sinh). */

const STUDENT_PERMISSIONS = ['Fund.View', 'Event.Participate', 'Balance.ViewOwn', 'Balance.Withdraw'];
const TREASURER_PERMISSIONS = [...STUDENT_PERMISSIONS, 'Event.Manage', 'Ledger.Manage', 'Payment.Confirm', 'Settlement.Execute', 'Report.View'];
const EVENT_ID = 'e1';

function userAs(role: 'Treasurer' | 'Student'): FakeUser {
  return {
    ...AN,
    fullName: 'Nguyễn Minh Anh',
    memberships: [{ fundId: FUND_ID, fundName: 'Lớp 10A1', role, permissions: role === 'Treasurer' ? TREASURER_PERMISSIONS : STUDENT_PERMISSIONS }],
  };
}

const preview = (partial: Record<string, unknown> = {}) => ({
  eventId: EVENT_ID,
  eventName: 'Dã ngoại Ba Vì',
  status: 'Completed',
  participantCount: 2,
  totalCollected: 200000,
  totalSpent: 120000,
  surplus: 80000,
  outcome: 'Surplus',
  unpaidObligationCount: 1,
  canExecute: true,
  lines: [
    { id: null, userId: 'u1', fullName: 'Phạm Đức Long', contributed: 100000, amount: 40000, status: null },
    { id: null, userId: 'u2', fullName: 'Lê Thu Hà', contributed: 100000, amount: 40000, status: null },
  ],
  ...partial,
});

const settled = (method: string | null, status: string) => ({
  id: 's1',
  eventId: EVENT_ID,
  eventName: 'Dã ngoại Ba Vì',
  outcome: 'Surplus',
  surplusMethod: method,
  participantCount: 2,
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

async function open(page: Page, role: 'Treasurer' | 'Student', state: { result: unknown | null; preview: unknown }): Promise<Recorded[]> {
  const requests: Recorded[] = [];
  const auth = new FakeAuthApi(page);
  auth.addAccount(userAs(role), 'Passw0rd1');
  auth.signIn(userAs(role));
  await auth.install();

  const json = (route: Route, body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
  await page.route(new RegExp(`/api/v1/funds/${FUND_ID}/events/${EVENT_ID}/settlement`), (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(`/api/v1/funds/${FUND_ID}/events/${EVENT_ID}`, '');
    const body = request.postData() ? JSON.parse(request.postData() as string) : null;
    requests.push({ method: request.method(), path, body });

    if (request.method() === 'GET' && path === '/settlement/preview') {
      return json(route, state.preview);
    }
    if (request.method() === 'GET' && path === '/settlement') {
      return state.result
        ? json(route, state.result)
        : route.fulfill({ status: 404, contentType: 'application/problem+json', body: JSON.stringify({ status: 404, code: 'not_found', title: 'x' }) });
    }
    if (request.method() === 'POST' && path === '/settlement') {
      state.result = settled(body.surplusMethod, body.surplusMethod === 'PersonalBalance' ? 'CreditedToBalance' : 'PendingRefund');
      return json(route, state.result, 201);
    }
    if (request.method() === 'POST' && path.endsWith('/refunded')) {
      const result = state.result as ReturnType<typeof settled>;
      result.lines[0].status = 'Refunded';
      return json(route, result.lines[0]);
    }
    return route.fulfill({ status: 404, body: '{}' });
  });
  await page.goto(`/funds/${FUND_ID}/events/${EVENT_ID}/settlement`);
  return requests;
}

test.describe('E-04 Quyết toán sự kiện', () => {
  test('xem trước: tổng hợp, bảng chia đều theo đầu người và cảnh báo khoản chưa nộp', async ({ page }) => {
    await open(page, 'Treasurer', { result: null, preview: preview() });

    await expect(page.getByRole('heading', { name: 'Quyết toán sự kiện' })).toBeVisible();
    await expect(page.getByText('200.000 đ').first()).toBeVisible();
    await expect(page.getByText('80.000 đ').first()).toBeVisible();
    await expect(page.getByText('1 khoản chưa nộp trong danh sách đã chốt', { exact: false })).toBeVisible();
    const table = page.getByRole('region', { name: 'Bảng chia tiền thừa' });
    await expect(table.getByRole('row', { name: /Phạm Đức Long/ })).not.toContainText('50%');
    await expect(table.getByRole('row', { name: /Phạm Đức Long/ })).toContainText('40.000 đ');
    await expect(page.getByRole('button', { name: 'Quyết toán sự kiện' })).toBeDisabled();
  });

  test('chọn cộng vào số dư rồi quyết toán: gọi đúng API và hiện kết quả', async ({ page }) => {
    const requests = await open(page, 'Treasurer', { result: null, preview: preview() });

    await page.getByRole('radio', { name: /Cộng vào số dư cá nhân/ }).check();
    await page.getByRole('button', { name: 'Quyết toán sự kiện' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Quyết toán' }).click();

    await expect(page.getByText('Đã quyết toán lúc')).toBeVisible();
    await expect(page.getByRole('row', { name: /Lê Thu Hà/ })).toContainText('Đã cộng số dư');
    expect(requests.find((item) => item.method === 'POST' && item.path === '/settlement')?.body).toEqual({ surplusMethod: 'PersonalBalance' });
  });

  test('còn khoản chưa nộp: chặn nút quyết toán', async ({ page }) => {
    await open(page, 'Treasurer', { result: null, preview: preview({ unpaidObligationCount: 2, canExecute: false }) });

    await expect(page.getByText('Còn 2 khoản chưa nộp trong danh sách đã chốt')).toBeVisible();
    await page.getByRole('radio', { name: /Hoàn tiền trực tiếp/ }).check();
    await expect(page.getByRole('button', { name: 'Quyết toán sự kiện' })).toBeDisabled();
  });

  test('hoàn trực tiếp: đánh dấu Đã hoàn từng người', async ({ page }) => {
    const requests = await open(page, 'Treasurer', { result: settled('DirectRefund', 'PendingRefund'), preview: null });

    await expect(page.getByText('Hoàn tiền trực tiếp')).toBeVisible();
    await page.getByRole('row', { name: /Phạm Đức Long/ }).getByRole('button', { name: 'Đã hoàn' }).click();

    await expect(page.getByText('Đã ghi nhận hoàn Phạm Đức Long.')).toBeVisible();
    expect(requests.some((item) => item.method === 'POST' && item.path === '/settlement/lines/l1/refunded')).toBe(true);
    await expect(page.getByRole('row', { name: /Phạm Đức Long/ })).toContainText('Đã hoàn');
  });

  test('học sinh chỉ thấy phần của mình sau khi quyết toán', async ({ page }) => {
    const result = settled('PersonalBalance', 'CreditedToBalance');
    result.lines = [result.lines[0]];
    await open(page, 'Student', { result, preview: null });

    await expect(page.getByRole('heading', { name: 'Phần của bạn' })).toBeVisible();
    await expect(page.getByRole('row', { name: /Phạm Đức Long/ })).toContainText('40.000 đ');
    await expect(page.getByRole('button', { name: 'Đã hoàn' })).toHaveCount(0);
  });
});
