import { Browser, Page, expect, test } from '@playwright/test';

import {
  E2E_PASSWORD,
  RealAccount,
  RealApi,
  assignTreasurer,
  createActiveStudent,
  createTransaction,
  isFullE2E,
  login,
  registerTeacher,
  unique,
} from '../support/real-api';

/**
 * M7a – luồng thật FE + BE + PostgreSQL (module-plan.md M7; FR-08, BR-02, BR-15, BR-36): S-01 Tổng quan cho bốn vai trò.
 * Số tổng giống nhau ở GVCN, thủ quỹ, học sinh, phụ huynh và bỏ khoản đã hủy; khoản không công khai chỉ GVCN / thủ quỹ thấy.
 * Bước 3 (sau khi M7b merge): số liệu S-01 khớp S-03 Báo cáo (tháng và 12 tháng) ở cả bốn vai trò.
 */
test.skip(!isFullE2E, 'Chỉ chạy khi E2E_FULL=1 (cần backend + DB, xem docs/architecture/foundation-guide.md)');

const TODAY = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date());
const MONTH = Number(TODAY.slice(5, 7));
const LAST_MONTH = MONTH === 1 ? `${Number(TODAY.slice(0, 4)) - 1}-12-01` : `${TODAY.slice(0, 5)}${String(MONTH - 1).padStart(2, '0')}-01`;

const kpi = (page: Page, label: string) => page.locator('.kpi').filter({ hasText: label }).locator('b');
/** Bỏ dấu +/− và đơn vị: bảng 12 tháng chỉ hiện số. */
const digits = (text: string) => text.replace(/[+−]/g, '').replace(' đ', '').trim();

const vnd = (value: number): string => `${new Intl.NumberFormat('vi-VN').format(value)} đ`;

async function openAs(browser: Browser, account: RealAccount, path: string): Promise<Page> {
  const context = await browser.newContext({ baseURL: 'http://localhost:4200' });
  const page = await context.newPage();
  await page.goto('/login');
  await page.getByLabel('Email hoặc tên đăng nhập').fill(account.login);
  await page.getByLabel('Mật khẩu').fill(account.password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page).not.toHaveURL(/\/login$/);
  await page.goto(path);
  return page;
}

/** GVCN thêm học sinh kèm phụ huynh mới rồi phụ huynh đổi mật khẩu tạm (điều kiện để vào quỹ). */
async function createActiveParent(browser: Browser, api: RealApi, fundId: string, childName: string): Promise<RealAccount> {
  const email = `${unique('ph')}@example.vn`;
  const created = await api.post<{ parent: { credential: { login: string; temporaryPassword: string } } }>(`/funds/${fundId}/students`, {
    fullName: childName,
    parentEmail: email,
    parentFullName: 'Phụ huynh e2e',
  });
  const { login: parentLogin, temporaryPassword } = created.parent.credential;
  const context = await browser.newContext({ baseURL: 'http://localhost:4200' });
  try {
    const parentApi = new RealApi(context.request);
    await login(parentApi, { login: parentLogin, password: temporaryPassword });
    await parentApi.post('/auth/change-password', { currentPassword: temporaryPassword, newPassword: E2E_PASSWORD });
  } finally {
    await context.close();
  }
  return { login: parentLogin, password: E2E_PASSWORD, fullName: 'Phụ huynh e2e' };
}

test('S-01: cùng số tổng ở bốn vai trò, khoản đã hủy không tính, khoản không công khai chỉ GVCN / thủ quỹ thấy', async ({ page, browser }) => {
  const teacher = await registerTeacher(new RealApi(page.request), 'Cô Hạnh M7a');
  const api = new RealApi(page.request);
  await login(api, teacher);
  const { id: fundId } = await api.post<{ id: string }>('/funds', { name: unique('Quỹ lớp 5E ') });
  const khang = await createActiveStudent(api, fundId, 'Nguyễn Minh Khang');
  const bao = await createActiveStudent(api, fundId, 'Lê Gia Bảo');
  await assignTreasurer(api, fundId, khang.studentId);
  const parent = await createActiveParent(browser, api, fundId, 'Phạm Gia Huy');

  // Thủ quỹ seed: tháng trước thu 2.450.000; tháng này thu 2.000.000 và chi 300.000 (công khai), chi 100.000 (không công khai),
  // thu 999.000 rồi hủy.
  const treasurerPage = await openAs(browser, khang, `/funds/${fundId}`);
  const treasurerApi = new RealApi(treasurerPage.request);
  await login(treasurerApi, khang);
  await createTransaction(treasurerApi, fundId, { type: 'Income', amount: 2_450_000, description: 'Đóng quỹ tháng trước', occurredOn: LAST_MONTH });
  await createTransaction(treasurerApi, fundId, { type: 'Income', amount: 2_000_000, description: 'Đóng quỹ đầu kỳ' });
  await createTransaction(treasurerApi, fundId, { type: 'Expense', amount: 300_000, description: 'Mua giấy A4', category: 'Văn phòng phẩm' });
  await createTransaction(treasurerApi, fundId, { type: 'Expense', amount: 100_000, description: 'Chi nội bộ', isPublic: false });
  const wrong = await createTransaction(treasurerApi, fundId, { type: 'Income', amount: 999_000, description: 'Ghi nhầm', category: 'Thu khác' });
  await treasurerApi.post(`/funds/${fundId}/transactions/${wrong.id}/cancel`, { reason: 'Ghi nhầm số tiền' });

  const expected = { balance: 4_050_000, income: 4_450_000, expense: 400_000 };
  const roles: [string, Page, number][] = [
    ['thủ quỹ', treasurerPage, 4],
    ['GVCN', page, 4],
    ['học sinh', await openAs(browser, bao, `/funds/${fundId}`), 3],
    ['phụ huynh', await openAs(browser, parent, `/funds/${fundId}`), 3],
  ];
  await page.goto(`/funds/${fundId}`);
  await treasurerPage.reload();

  for (const [role, view, recentCount] of roles) {
    await expect(view.getByRole('heading', { name: 'Tổng quan', level: 1 }), role).toBeVisible();
    await expect(view.getByTestId('kpi-balance'), role).toHaveText(vnd(expected.balance));
    await expect(view.getByTestId('kpi-income'), role).toHaveText(vnd(expected.income));
    await expect(view.getByTestId('kpi-expense'), role).toHaveText(vnd(expected.expense));
    await expect(view.getByTestId('recent').getByRole('listitem'), role).toHaveCount(recentCount);
    await expect(view.getByTestId('managers'), role).toContainText('Nguyễn Minh Khang');
    await expect(view.getByTestId('trend-chart'), role).toBeVisible();
    await expect(view.getByTestId('negative-balance'), role).toBeHidden();
    await expect(view.getByTestId('no-treasurer'), role).toBeHidden();
    // Khoản đã hủy không bao giờ hiện ở danh sách gần đây.
    await expect(view.getByTestId('recent'), role).not.toContainText('Ghi nhầm');
  }

  // Bước 3: S-01 khớp S-03 ở cả bốn vai trò (số liệu tháng, đầu / cuối tháng, biểu đồ và dòng tháng của bảng 12 tháng).
  for (const [role, view] of roles) {
    const balance = await view.getByTestId('kpi-balance').innerText();
    const monthNet = await view.getByTestId('kpi-month').innerText();
    const chartMonth = await view.getByTestId('trend-chart').locator('xpath=following-sibling::table//tbody/tr[last()]/td').allInnerTexts();
    await view.goto(`/funds/${fundId}/reports`);
    await expect(kpi(view, 'Số dư cuối tháng'), role).toHaveText(balance);
    await expect(kpi(view, 'Chênh lệch'), role).toHaveText(monthNet);
    await expect(kpi(view, 'Số dư đầu tháng'), role).toHaveText(vnd(2_450_000));
    await expect(kpi(view, 'Tổng thu'), role).toHaveText(`+${chartMonth[0]}`);
    await expect(kpi(view, 'Tổng chi'), role).toHaveText(`−${chartMonth[1]}`);
    await view.getByRole('button', { name: '12 tháng' }).click();
    const monthRow = view.getByRole('region', { name: /Bảng 12 tháng/ }).locator('tbody tr').nth(MONTH - 1);
    await expect(monthRow, role).toContainText(digits(balance));
    await expect(monthRow, role).toContainText(digits(chartMonth[0]));
    await view.goto(`/funds/${fundId}`);
  }

  // Khoản không công khai: chỉ GVCN / thủ quỹ.
  await expect(page.getByTestId('recent')).toContainText('Chi nội bộ');
  await expect(treasurerPage.getByTestId('recent')).toContainText('Chi nội bộ');
  await expect(roles[2][1].getByTestId('recent')).not.toContainText('Chi nội bộ');
  await expect(roles[3][1].getByTestId('recent')).not.toContainText('Chi nội bộ');
  await expect(roles[2][1].getByTestId('public-note')).toBeVisible();
  await expect(treasurerPage.getByRole('button', { name: '+ Khoản chi' })).toBeVisible();
  await expect(page.getByRole('button', { name: '+ Khoản chi' })).toBeHidden();

  // Thủ quỹ thêm một khoản chi lớn bằng nút ở S-01 → số dư âm, cảnh báo hiện, số tổng cập nhật.
  await treasurerPage.getByRole('button', { name: '+ Khoản chi' }).click();
  const dialog = treasurerPage.getByRole('dialog', { name: 'Thêm khoản chi' });
  await dialog.getByRole('textbox', { name: 'Nội dung *' }).fill('Chi vượt quỹ');
  await dialog.getByRole('spinbutton', { name: 'Số tiền (đ) *' }).fill('5000000');
  await dialog.getByRole('combobox', { name: 'Danh mục *' }).click();
  await treasurerPage.getByRole('option', { name: 'Khen thưởng' }).click();
  await dialog.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(treasurerPage.getByTestId('kpi-balance')).toHaveText('−950.000 đ');
  await expect(treasurerPage.getByTestId('kpi-expense')).toHaveText(vnd(5_400_000));
  await expect(treasurerPage.getByTestId('negative-balance')).toBeVisible();
  await expect(treasurerPage.getByTestId('recent').getByRole('listitem').first()).toContainText('Chi vượt quỹ');
});

test('S-01: quỹ mới chưa có thủ quỹ và chưa có giao dịch – số liệu 0, cảnh báo và link phân công', async ({ page }) => {
  const api = new RealApi(page.request);
  const teacher = await registerTeacher(api, 'Thầy Hùng M7a');
  await login(api, teacher);
  const { id: fundId } = await api.post<{ id: string }>('/funds', { name: unique('Quỹ lớp 5F ') });

  await page.goto(`/funds/${fundId}`);

  await expect(page.getByTestId('kpi-balance')).toHaveText(vnd(0));
  await expect(page.getByTestId('no-treasurer')).toContainText('Chưa phân công thủ quỹ');
  await expect(page.getByText('Chưa có khoản thu chi nào.')).toBeVisible();
  await expect(page.getByTestId('managers')).toContainText('Thầy Hùng M7a');
  await page.getByTestId('no-treasurer').getByRole('link', { name: 'Phân công thủ quỹ' }).click();
  await expect(page).toHaveURL(new RegExp(`/funds/${fundId}/treasurers$`));
});
