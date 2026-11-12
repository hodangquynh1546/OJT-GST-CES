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
 * M6 – luồng thật FE + BE + PostgreSQL (module-plan.md M6): thủ quỹ thêm thu / chi → sửa → hủy; GVCN xem đủ chi tiết nhưng
 * không có nút ghi; học sinh chưa phân công (cùng phạm vi phụ huynh – BR-34) không thấy khoản không công khai.
 * Cần API danh mục (M5) để chọn danh mục: chưa có thì bỏ qua.
 */
test.skip(!isFullE2E, 'Chỉ chạy khi E2E_FULL=1 (cần backend + DB, xem docs/architecture/foundation-guide.md)');

async function openLedgerAs(browser: Browser, account: RealStudent, fundId: string): Promise<Page> {
  const context = await browser.newContext({ baseURL: 'http://localhost:4200' });
  const page = await context.newPage();
  await page.goto('/login');
  await page.getByLabel('Email hoặc tên đăng nhập').fill(account.login);
  await page.getByLabel('Mật khẩu').fill(account.password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page).not.toHaveURL(/\/login$/);
  await page.goto(`/funds/${fundId}/transactions`);
  return page;
}

async function addTransaction(page: Page, button: string, description: string, amount: string, category: string, isPublic = true): Promise<void> {
  await page.getByRole('button', { name: button }).click();
  const dialog = page.getByRole('dialog', { name: button === '+ Khoản thu' ? 'Thêm khoản thu' : 'Thêm khoản chi' });
  await dialog.getByRole('textbox', { name: 'Nội dung *' }).fill(description);
  await dialog.getByRole('spinbutton', { name: 'Số tiền (đ) *' }).fill(amount);
  await dialog.getByRole('combobox', { name: 'Danh mục *' }).click();
  await page.getByRole('option', { name: category }).click();
  if (!isPublic) {
    await dialog.getByRole('checkbox', { name: /Công khai cho học sinh và phụ huynh/ }).uncheck();
  }
  await dialog.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(dialog).toBeHidden();
}

test('thủ quỹ thêm thu / chi → sửa → hủy; GVCN chỉ xem; học sinh chỉ thấy khoản công khai', async ({ page, browser }) => {
  const api = new RealApi(page.request);
  await registerTeacher(api, 'Cô Hạnh M6');
  const { id: fundId } = await api.post<{ id: string }>('/funds', { name: unique('Quỹ lớp 5E ') });
  const categories = await page.request.get(`/api/v1/funds/${fundId}/categories`);
  test.skip(categories.status() === 404, 'Chờ API danh mục của M5 (GET /funds/{id}/categories) merge');

  const khang = await createActiveStudent(api, fundId, 'Nguyễn Minh Khang');
  const bao = await createActiveStudent(api, fundId, 'Lê Gia Bảo');
  await assignTreasurer(api, fundId, khang.studentId);

  // 1. Thủ quỹ ghi bằng giao diện: khoản thu công khai, khoản chi không công khai.
  const treasurer = await openLedgerAs(browser, khang, fundId);
  await expect(treasurer.getByRole('heading', { name: 'Thu chi', level: 1 })).toBeVisible();
  await addTransaction(treasurer, '+ Khoản thu', 'Đóng quỹ tháng 11', '3200000', 'Đóng quỹ');
  await expect(treasurer.getByText(/Đã lưu khoản thu 3\.200\.000 đ\. Số dư mới: 3\.200\.000 đ\./)).toBeVisible();
  await addTransaction(treasurer, '+ Khoản chi', 'Quà tặng cô giáo 20/11', '500000', 'Khen thưởng', false);

  // Helper real-api (seed thêm một khoản chi công khai bằng API, phiên riêng của thủ quỹ).
  const treasurerApi = new RealApi(treasurer.request);
  await login(treasurerApi, khang);
  const paper = await createTransaction(treasurerApi, fundId, { type: 'Expense', amount: 180_000, description: 'Mua giấy A4', category: 'Văn phòng phẩm' });
  expect(paper.isNegativeBalance).toBe(false);

  await treasurer.reload();
  await expect(treasurer.getByText('3 giao dịch · 20 / trang')).toBeVisible();
  await expect(treasurer.getByRole('row', { name: /Quà tặng cô giáo/ })).toContainText('Không công khai');

  // 2. Sửa → lịch sử ghi "Sửa"; 3. Hủy có lý do.
  await treasurer.getByRole('button', { name: 'Mua giấy A4' }).click();
  const detail = treasurer.getByRole('complementary', { name: 'Chi tiết giao dịch' });
  await detail.getByRole('button', { name: 'Sửa' }).click();
  const edit = treasurer.getByRole('dialog', { name: 'Sửa khoản chi' });
  await edit.getByRole('spinbutton', { name: 'Số tiền (đ) *' }).fill('200000');
  await edit.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(edit).toBeHidden();
  await expect(detail).toContainText('Sửa · ');
  await expect(detail).toContainText('Số tiền: 180.000 đ → 200.000 đ');

  await detail.getByRole('button', { name: 'Hủy giao dịch' }).click();
  const cancel = treasurer.getByRole('dialog', { name: 'Hủy giao dịch?' });
  await cancel.getByRole('textbox', { name: 'Lý do hủy *' }).fill('Ghi trùng');
  await cancel.getByRole('button', { name: 'Hủy giao dịch' }).click();
  await expect(cancel).toBeHidden();
  await expect(detail).toContainText('Lý do: Ghi trùng');
  await expect(treasurer.getByText('2 giao dịch · 20 / trang')).toBeVisible();
  await treasurer.context().close();

  // GVCN: xem đủ, không có nút ghi / sửa / hủy.
  await page.goto(`/funds/${fundId}/transactions`);
  await expect(page.getByRole('button', { name: '+ Khoản chi' })).toBeHidden();
  await page.getByRole('button', { name: 'Quà tặng cô giáo 20/11' }).click();
  const teacherDetail = page.getByRole('complementary', { name: 'Chi tiết giao dịch' });
  await expect(teacherDetail).toContainText('Người ghi');
  await expect(teacherDetail).toContainText('Nguyễn Minh Khang');
  await expect(teacherDetail).toContainText('Lịch sử thay đổi');
  await expect(teacherDetail.getByRole('button', { name: 'Sửa' })).toBeHidden();

  // Học sinh chưa phân công: bản công khai.
  const student = await openLedgerAs(browser, bao, fundId);
  await expect(student.getByRole('heading', { name: 'Thu chi công khai', level: 1 })).toBeVisible();
  await expect(student.getByText('1 giao dịch công khai · 20 / trang')).toBeVisible();
  await expect(student.getByText('Quà tặng cô giáo 20/11')).toBeHidden();
  await student.getByRole('combobox', { name: 'Trạng thái' }).click();
  await student.getByRole('option', { name: 'Tất cả' }).click();
  await expect(student.getByRole('row', { name: /Mua giấy A4/ })).toContainText('Đã hủy');
  await student.getByRole('button', { name: 'Mua giấy A4' }).click();
  await expect(student.getByRole('complementary', { name: 'Chi tiết giao dịch' })).not.toContainText('Ghi trùng');
  await student.context().close();
});
