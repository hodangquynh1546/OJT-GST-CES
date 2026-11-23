import { Page, Route, expect, test } from '@playwright/test';

import { AN, FakeAuthApi, FakeUser } from '../support/fake-auth-api';
import { FUND_ID } from '../support/fake-charges-api';

/** M11-T3 (FE): N-01 Thông báo và chuông ở header với API giả lập. */

const PERMISSIONS = ['Fund.View', 'Event.Participate', 'Payment.Submit', 'Balance.ViewOwn', 'Balance.Withdraw'];

const user: FakeUser = {
  ...AN,
  fullName: 'Nguyễn Minh Anh',
  memberships: [{ fundId: FUND_ID, fundName: 'Lớp 10A1', role: 'Student', permissions: PERMISSIONS }],
};

interface Item {
  id: string;
  scope: 'General' | 'Event';
  eventId: string | null;
  type: string;
  title: string;
  body: string;
  referenceType: string | null;
  referenceId: string | null;
  isRead: boolean;
  createdAt: string;
}

const item = (partial: Partial<Item> & Pick<Item, 'id' | 'title'>): Item => ({
  scope: 'General',
  eventId: null,
  type: 'GeneralChargeCreated',
  body: 'Hạn đóng 30/12/2026',
  referenceType: null,
  referenceId: null,
  isRead: false,
  createdAt: '2026-11-20T03:00:00Z',
  ...partial,
});

async function open(page: Page, items: Item[]): Promise<{ requests: string[]; items: Item[] }> {
  const requests: string[] = [];
  const auth = new FakeAuthApi(page);
  auth.addAccount(user, 'Passw0rd1');
  auth.signIn(user);
  await auth.install();

  const unread = () => items.filter((entry) => !entry.isRead).length;
  const json = (route: Route, body: unknown) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  await page.route(new RegExp(`/api/v1/funds/${FUND_ID}/me/notifications`), (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(`/api/v1/funds/${FUND_ID}/me/notifications`, '');
    requests.push(`${request.method()} ${path}${url.search}`);

    if (request.method() === 'GET' && path === '/unread-count') {
      return json(route, { count: unread() });
    }
    if (request.method() === 'GET' && path === '') {
      const rows = url.searchParams.get('unreadOnly') === 'true' ? items.filter((entry) => !entry.isRead) : items;
      return json(route, { items: rows, total: rows.length, page: 1, pageSize: 20, unreadCount: unread() });
    }
    if (request.method() === 'POST' && path === '/read-all') {
      const marked = unread();
      items.forEach((entry) => (entry.isRead = true));
      return json(route, { marked });
    }
    const read = /^\/([^/]+)\/read$/.exec(path);
    if (request.method() === 'POST' && read) {
      const found = items.find((entry) => entry.id === read[1]);
      if (found) {
        found.isRead = true;
      }
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ status: 404, body: '{}' });
  });
  await page.goto(`/funds/${FUND_ID}/notifications`);
  return { requests, items };
}

test.describe('N-01 Thông báo', () => {
  test('danh sách ghi rõ phạm vi, chưa đọc nổi bật và chuông hiện số chưa đọc', async ({ page }) => {
    await open(page, [
      item({ id: 'n1', title: '[Quỹ chung] Khoản thu mới: Quỹ HK1' }),
      item({ id: 'n2', scope: 'Event', eventId: 'e1', type: 'EventChargeCreated', title: '[Sự kiện: Dã ngoại Ba Vì] Khoản thu mới: Tiền xe' }),
      item({ id: 'n3', title: '[Quỹ chung] Đã xác nhận khoản đóng: Quỹ cũ', isRead: true, type: 'PaymentConfirmed' }),
    ]);

    await expect(page.getByRole('heading', { name: 'Thông báo' })).toBeVisible();
    const list = page.getByRole('list', { name: 'Danh sách thông báo' });
    await expect(list.getByRole('listitem')).toHaveCount(3);
    await expect(list.getByRole('listitem').nth(0)).toContainText('Quỹ chung');
    await expect(list.getByRole('listitem').nth(0)).toContainText('Khoản thu mới: Quỹ HK1');
    await expect(list.getByRole('listitem').nth(1)).toContainText('Sự kiện: Dã ngoại Ba Vì');
    await expect(list.getByRole('listitem').nth(1)).toContainText('Khoản thu mới: Tiền xe');
    await expect(page.getByRole('link', { name: 'Thông báo, 2 chưa đọc' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'Chưa đọc 2' })).toBeVisible();
  });

  test('lọc chưa đọc gọi đúng tham số', async ({ page }) => {
    const { requests } = await open(page, [item({ id: 'n1', title: '[Quỹ chung] A' }), item({ id: 'n2', title: '[Quỹ chung] B', isRead: true })]);

    await page.getByRole('tab', { name: /Chưa đọc/ }).click();

    await expect(page.getByRole('list', { name: 'Danh sách thông báo' }).getByRole('listitem')).toHaveCount(1);
    expect(requests.some((entry) => entry.includes('unreadOnly=true'))).toBe(true);
  });

  test('bấm một thông báo: đánh dấu đã đọc và mở nơi liên quan', async ({ page }) => {
    const { requests } = await open(page, [
      item({ id: 'n2', scope: 'Event', eventId: 'e1', type: 'SettlementResult', title: '[Sự kiện: Dã ngoại Ba Vì] Sự kiện đã quyết toán' }),
    ]);

    await page.getByRole('button', { name: /Sự kiện đã quyết toán/ }).click();

    await expect(page).toHaveURL(new RegExp(`/funds/${FUND_ID}/events/e1/settlement`));
    expect(requests).toContain('POST /n2/read');
  });

  test('đánh dấu tất cả đã đọc', async ({ page }) => {
    const { requests } = await open(page, [item({ id: 'n1', title: '[Quỹ chung] A' }), item({ id: 'n2', title: '[Quỹ chung] B' })]);

    await page.getByRole('button', { name: 'Đánh dấu tất cả đã đọc' }).click();

    await expect(page.getByText('Đã đánh dấu 2 thông báo là đã đọc.')).toBeVisible();
    await expect(page.getByRole('tab', { name: 'Chưa đọc 0' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Đánh dấu tất cả đã đọc' })).toBeDisabled();
    expect(requests).toContain('POST /read-all');
  });

  test('chưa có thông báo nào hiện trạng thái trống', async ({ page }) => {
    await open(page, []);

    await expect(page.getByText('Chưa có thông báo nào')).toBeVisible();
  });
});
