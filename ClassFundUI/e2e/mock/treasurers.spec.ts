import { Page, expect, test } from '@playwright/test';

import { AN, FakeAuthApi, FakeUser } from '../support/fake-auth-api';
import { FUND_ID, FakePerson, FakeTreasurersApi, person } from '../support/fake-treasurers-api';

/** M4-T3: màn T-01 Thủ quỹ với API giả lập (docs/testing/test-strategy.md). */

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
  'Payment.Confirm',
  'Settlement.Execute',
  'Balance.ViewOwn',
  'Balance.Handle',
  'Report.View',
  'Audit.View',
];

function userAs(role: 'Owner' | 'Treasurer'): FakeUser {
  const permissions = role === 'Owner' ? OWNER_PERMISSIONS : OWNER_PERMISSIONS.filter((item) => item !== 'Treasurer.Manage');
  return { ...AN, fullName: 'Nguyễn Minh Anh', memberships: [{ fundId: FUND_ID, fundName: 'Lớp 10A1', role, permissions }] };
}

const BASE = (): FakePerson[] => [
  person({ fullName: 'Nguyễn Minh Anh', email: 'minhanh@truong.edu.vn', role: 'Owner' }),
  person({ fullName: 'Trần Quang Huy', email: 'huy.tran@truong.edu.vn', role: 'Treasurer', grantedAt: '2026-09-01T03:00:00Z' }),
  person({ fullName: 'Lê Thu Hà', email: 'ha.le@truong.edu.vn' }),
  person({ fullName: 'Bùi Thảo My', email: 'my.bui@truong.edu.vn' }),
  person({ fullName: 'Phạm Đức Long', email: 'long.pham@truong.edu.vn' }),
];

async function open(page: Page, role: 'Owner' | 'Treasurer' = 'Owner', people = BASE()): Promise<FakeTreasurersApi> {
  const auth = new FakeAuthApi(page);
  auth.addAccount(userAs(role), 'Passw0rd1');
  auth.signIn(userAs(role));
  await auth.install();
  const api = new FakeTreasurersApi(page, people);
  await api.install();
  await page.goto(`/funds/${FUND_ID}/treasurers`);
  return api;
}

const teamRow = (page: Page, name: string | RegExp) => page.getByRole('listitem').filter({ hasText: name });

/** Mở danh sách chọn học sinh; chờ tới khi thật sự mở (sau khi đóng hộp thoại, cú nhấp đầu có thể chỉ trả focus). */
async function openList(page: Page): Promise<void> {
  const listbox = page.getByRole('listbox');
  await expect(async () => {
    if (!(await listbox.isVisible())) {
      await page.getByRole('combobox').click();
    }
    await expect(listbox).toBeVisible({ timeout: 800 });
  }).toPass();
}

async function choose(page: Page, search: string, optionName: RegExp): Promise<void> {
  await openList(page);
  if (search) {
    await page.getByRole('combobox').fill(search);
  }
  await page.getByRole('option', { name: optionName }).click();
}

test('hiện đội thủ quỹ: chủ quỹ không thu hồi được, thủ quỹ có nút Thu hồi và ngày phân quyền', async ({ page }) => {
  await open(page);

  await expect(page.getByRole('heading', { name: 'Thủ quỹ', exact: true })).toBeVisible();
  await expect(page.getByText('Chỉ thủ quỹ chính phân quyền / thu hồi.')).toBeVisible();
  const owner = teamRow(page, 'Nguyễn Minh Anh');
  await expect(owner).toContainText('Người tạo quỹ · không thể thu hồi');
  await expect(owner).toContainText('Thủ quỹ chính');
  await expect(owner.getByRole('button')).toHaveCount(0); // AZ-03

  const treasurer = teamRow(page, 'Trần Quang Huy');
  await expect(treasurer).toContainText('Phân quyền 01/09/2026');
  await expect(treasurer.getByRole('button', { name: 'Thu hồi Trần Quang Huy' })).toBeVisible();
  await expect(teamRow(page, 'Lê Thu Hà')).toHaveCount(0); // học sinh không có trong đội

  await expect(page.getByRole('heading', { name: 'Quyền của thủ quỹ' })).toBeVisible();
});

test('nút Phân quyền bị khóa cho tới khi chọn một học sinh', async ({ page }) => {
  await open(page);

  await expect(page.getByRole('button', { name: 'Phân quyền thủ quỹ' })).toBeDisabled();
  await choose(page, '', /Lê Thu Hà/);
  await expect(page.getByRole('button', { name: 'Phân quyền thủ quỹ' })).toBeEnabled();
});

test('chọn học sinh (lọc theo tên) rồi phân quyền: vào đội thủ quỹ, hiện thông báo, hết trong danh sách chọn', async ({ page }) => {
  const api = await open(page);

  await openList(page);
  await page.getByRole('combobox').fill('hà');
  await expect.poll(() => api.last('GET', '/members')?.query.get('search')).toBe('hà'); // có debounce, lọc phía server
  expect(api.last('GET', '/members')?.query.get('role')).toBe('Student');
  await expect(page.getByRole('option')).toHaveCount(1);
  await page.getByRole('option', { name: /Lê Thu Hà – ha\.le@truong\.edu\.vn/ }).click();
  await page.getByRole('button', { name: 'Phân quyền thủ quỹ' }).click();

  await expect(page.getByText('Đã phân quyền thủ quỹ cho Lê Thu Hà.')).toBeVisible();
  expect(api.last('POST', '/treasurers')?.body).toEqual({ memberId: 'm-ha.le' });
  await expect(teamRow(page, 'Lê Thu Hà')).toContainText('Thủ quỹ');
  await expect(teamRow(page, 'Lê Thu Hà').getByRole('button', { name: 'Thu hồi Lê Thu Hà' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Phân quyền thủ quỹ' })).toBeDisabled(); // đã xóa lựa chọn

  await openList(page);
  await expect(page.getByRole('option', { name: /Lê Thu Hà/ })).toHaveCount(0);
  await expect(page.getByRole('option', { name: /Bùi Thảo My/ })).toBeVisible();
});

test('thu hồi: hộp thoại xác nhận nêu hệ quả (nút đỏ); xác nhận thì người đó ra khỏi đội và về danh sách học sinh', async ({ page }) => {
  const api = await open(page);

  await page.getByRole('button', { name: 'Thu hồi Trần Quang Huy' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText('Thu hồi quyền thủ quỹ của Trần Quang Huy?');
  await expect(dialog).toContainText('mất quyền quản lý ngay lập tức');
  expect(api.last('POST', '/treasurers/m-huy.tran/revoke')).toBeUndefined();
  await dialog.getByRole('button', { name: 'Thu hồi' }).click();

  await expect(page.getByText('Đã thu hồi quyền thủ quỹ của Trần Quang Huy.')).toBeVisible();
  await expect(teamRow(page, 'Trần Quang Huy')).toHaveCount(0);
  await openList(page);
  await expect(page.getByRole('option', { name: /Trần Quang Huy/ })).toBeVisible(); // lại là học sinh, chọn được
});

test('Hủy bỏ ở hộp thoại thu hồi thì không thu hồi ai', async ({ page }) => {
  const api = await open(page);

  await page.getByRole('button', { name: 'Thu hồi Trần Quang Huy' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Hủy bỏ' }).click();

  expect(api.last('POST', '/treasurers/m-huy.tran/revoke')).toBeUndefined();
  await expect(teamRow(page, 'Trần Quang Huy')).toBeVisible();
});

test('lỗi từ backend (đã là thủ quỹ – 409) hiện thông báo tiếng Việt và danh sách được tải lại', async ({ page }) => {
  const api = await open(page);
  api.failGrant.set('ha.le@truong.edu.vn', { status: 409, code: 'treasurer.already_treasurer', title: 'x' });

  await choose(page, 'hà', /Lê Thu Hà/);
  await page.getByRole('button', { name: 'Phân quyền thủ quỹ' }).click();

  await expect(page.getByText('Người này đã là thủ quỹ của quỹ.')).toBeVisible();
});

test('quỹ chưa có học sinh nào: hiện gợi ý thay vì danh sách chọn trống', async ({ page }) => {
  await open(page, 'Owner', BASE().filter((item) => item.role !== 'Student'));

  await expect(page.getByText('Chưa có học sinh nào để phân quyền. Hãy thêm học sinh vào quỹ trước.')).toBeVisible();
});

test('thủ quỹ thường không có mục Thủ quỹ trong menu và vào thẳng đường dẫn thì bị chuyển tới trang 403 (FR-13)', async ({ page }) => {
  await open(page, 'Treasurer');

  await expect(page).toHaveURL(/\/forbidden$/);
  await expect(page.getByRole('navigation', { name: 'Điều hướng chính' }).getByRole('link', { name: 'Thủ quỹ' })).toHaveCount(0);
  await expect(page.getByRole('navigation', { name: 'Điều hướng chính' }).getByRole('link', { name: 'Học sinh' })).toBeVisible();
});

test('thủ quỹ chính có mục Thủ quỹ trong menu và mục đó đang được chọn', async ({ page }) => {
  await open(page);

  const item = page.getByRole('navigation', { name: 'Điều hướng chính' }).getByRole('link', { name: 'Thủ quỹ' });
  await expect(item).toHaveAttribute('aria-current', 'page');
  await expect(page.getByRole('banner').locator('[aria-current="page"]')).toHaveText('Thủ quỹ');
});
