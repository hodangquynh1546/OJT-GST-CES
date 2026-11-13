import { Browser, Page, expect, test } from '@playwright/test';

import {
  RealApi,
  RealStudent,
  assignTreasurer,
  createActiveStudent,
  createTransaction,
  isFullE2E,
  login,
  registerTeacher,
  unique,
} from '../support/real-api';

/**
 * M7b – luồng thật FE + BE + PostgreSQL (module-plan.md M7): dữ liệu thu chi của M6 → số liệu S-03 khớp ở mọi vai trò.
 * Thủ quỹ, GVCN: bản đầy đủ; học sinh chưa phân công (cùng phạm vi phụ huynh – BR-34): bản công khai, phần không công khai
 * gộp "Khoản khác". Khoản đã hủy không tính; đầu tháng = cuối tháng trước.
 */
test.skip(!isFullE2E, 'Chỉ chạy khi E2E_FULL=1 (cần backend + DB, xem docs/architecture/foundation-guide.md)');

const TODAY = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date());
const YEAR = Number(TODAY.slice(0, 4));
const MONTH = Number(TODAY.slice(5, 7));
const LAST_MONTH = MONTH === 1 ? `${YEAR - 1}-12-01` : `${YEAR}-${String(MONTH - 1).padStart(2, '0')}-01`;
const MM = String(MONTH).padStart(2, '0');

async function openReportsAs(browser: Browser, account: RealStudent, fundId: string): Promise<Page> {
  const context = await browser.newContext({ baseURL: 'http://localhost:4200' });
  const page = await context.newPage();
  await page.goto('/login');
  await page.getByLabel('Email hoặc tên đăng nhập').fill(account.login);
  await page.getByLabel('Mật khẩu').fill(account.password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page).not.toHaveURL(/\/login$/);
  await page.goto(`/funds/${fundId}/reports`);
  return page;
}

const kpi = (page: Page, label: string) => page.locator('.kpi').filter({ hasText: label }).locator('b');

/** 5 số liệu tháng hiện tại – giống nhau ở mọi vai trò (BR-15). */
async function expectMonthTotals(page: Page): Promise<void> {
  await expect(kpi(page, 'Số dư đầu tháng')).toHaveText('2.450.000 đ');
  await expect(kpi(page, 'Tổng thu')).toHaveText('+3.500.000 đ');
  await expect(kpi(page, 'Tổng chi')).toHaveText('−680.000 đ');
  await expect(kpi(page, 'Chênh lệch')).toHaveText('+2.820.000 đ');
  await expect(kpi(page, 'Số dư cuối tháng')).toHaveText('5.270.000 đ');
}

test('số liệu báo cáo tháng / 12 tháng khớp dữ liệu thu chi ở thủ quỹ, GVCN, học sinh (bản công khai)', async ({ page, browser }) => {
  const api = new RealApi(page.request);
  await registerTeacher(api, 'Cô Hạnh M7b');
  const { id: fundId } = await api.post<{ id: string }>('/funds', { name: unique('Quỹ lớp 5R ') });
  const khang = await createActiveStudent(api, fundId, 'Nguyễn Minh Khang');
  const bao = await createActiveStudent(api, fundId, 'Lê Gia Bảo');
  await assignTreasurer(api, fundId, khang.studentId);

  // Dữ liệu M6 (thủ quỹ ghi qua API): tháng trước 2.450.000; tháng này thu 3.200.000 + 300.000 (không công khai),
  // chi 180.000 + 500.000 (không công khai) + 90.000 đã hủy.
  const seedContext = await browser.newContext({ baseURL: 'http://localhost:4200' });
  const treasurerApi = new RealApi(seedContext.request);
  await login(treasurerApi, khang);
  await createTransaction(treasurerApi, fundId, { type: 'Income', amount: 2_450_000, description: 'Đóng quỹ tháng trước', category: 'Đóng quỹ', occurredOn: LAST_MONTH });
  await createTransaction(treasurerApi, fundId, { type: 'Income', amount: 3_200_000, description: 'Đóng quỹ tháng này', category: 'Đóng quỹ' });
  await createTransaction(treasurerApi, fundId, { type: 'Income', amount: 300_000, description: 'Ủng hộ riêng', category: 'Thu khác', isPublic: false });
  await createTransaction(treasurerApi, fundId, { type: 'Expense', amount: 180_000, description: 'Mua giấy A4', category: 'Văn phòng phẩm' });
  await createTransaction(treasurerApi, fundId, { type: 'Expense', amount: 500_000, description: 'Quà tặng cô giáo', category: 'Khen thưởng', isPublic: false });
  const wrong = await createTransaction(treasurerApi, fundId, { type: 'Expense', amount: 90_000, description: 'Ghi nhầm', category: 'Chi khác' });
  await treasurerApi.post(`/funds/${fundId}/transactions/${wrong.id}/cancel`, { reason: 'Ghi trùng' });
  await seedContext.close();

  // GVCN: bản đầy đủ, theo danh mục có số khoản, danh sách 4 khoản đang hiệu lực.
  await page.goto(`/funds/${fundId}/reports`);
  await expect(page.getByText('Đầy đủ · GVCN chỉ xem')).toBeVisible();
  await expectMonthTotals(page);
  const expense = page.getByRole('region', { name: 'Chi theo danh mục' });
  await expect(expense.locator('tbody tr')).toHaveText([/Khen thưởng\s*1\s*−500\.000 đ/, /Văn phòng phẩm\s*1\s*−180\.000 đ/]);
  await expect(page.getByRole('region', { name: /Giao dịch trong tháng \(4\)/ }).locator('tbody tr')).toHaveCount(4);
  await expect(page.getByText('Ghi nhầm')).toHaveCount(0);

  // 12 tháng: dòng tháng hiện tại khớp báo cáo tháng; đầu tháng = cuối tháng trước.
  await page.getByRole('button', { name: '12 tháng' }).click();
  const rows = page.getByRole('region', { name: /Bảng 12 tháng/ }).locator('tbody tr');
  await expect(rows).toHaveCount(12);
  await expect(rows.nth(MONTH - 1)).toHaveText(
    new RegExp(`T${MONTH}/${YEAR}\\s*2\\.450\\.000\\s*\\+3\\.500\\.000\\s*−680\\.000\\s*\\+2\\.820\\.000\\s*5\\.270\\.000`),
  );

  // Thủ quỹ: cùng số liệu, bản đầy đủ.
  const treasurer = await openReportsAs(browser, khang, fundId);
  await expect(treasurer.getByText('Đầy đủ, tự tính từ giao dịch đang hiệu lực')).toBeVisible();
  await expectMonthTotals(treasurer);
  await expect(treasurer.getByRole('region', { name: 'Thu theo danh mục' }).locator('tbody tr')).toHaveText([
    /Đóng quỹ\s*1\s*\+3\.200\.000 đ/,
    /Thu khác\s*1\s*\+300\.000 đ/,
  ]);
  await treasurer.context().close();

  // Học sinh chưa phân công: bản công khai – số tổng giữ nguyên, "Khoản khác" không số lượng, không lộ khoản ẩn.
  const student = await openReportsAs(browser, bao, fundId);
  await expect(student.getByText('Bản công khai cho học sinh · mọi tháng')).toBeVisible();
  await expect(student.locator('kendo-dropdownlist')).toContainText(`Tháng ${MM}/${YEAR}`);
  await expectMonthTotals(student);
  await expect(student.getByRole('region', { name: 'Thu theo danh mục' }).locator('tbody tr')).toHaveText([
    /Đóng quỹ\s*1\s*\+3\.200\.000 đ/,
    /Khoản khác \(không công khai chi tiết\)\s*—\s*\+300\.000 đ/,
  ]);
  await expect(student.getByRole('region', { name: 'Chi theo danh mục' }).locator('tbody tr')).toHaveText([
    /Văn phòng phẩm\s*1\s*−180\.000 đ/,
    /Khoản khác \(không công khai chi tiết\)\s*—\s*−500\.000 đ/,
  ]);
  await expect(student.getByRole('region', { name: /Khoản công khai trong tháng \(2\)/ }).locator('tbody tr')).toHaveCount(2);
  for (const hidden of ['Khen thưởng', 'Quà tặng cô giáo', 'Ủng hộ riêng', 'Thu khác']) {
    await expect(student.getByText(hidden)).toHaveCount(0);
  }
  await student.getByRole('button', { name: '12 tháng' }).click();
  await expect(student.getByRole('region', { name: /Bảng 12 tháng/ }).locator('tbody tr').nth(MONTH - 1)).toContainText('5.270.000');
  await student.context().close();
});
