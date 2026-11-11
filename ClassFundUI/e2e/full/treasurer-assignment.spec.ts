import { Browser, Page, expect, test } from '@playwright/test';

import {
  RealApi,
  RealStudent,
  createActiveStudent,
  createStudent,
  grantAccount,
  isFullE2E,
  registerTeacher,
  unique,
} from '../support/real-api';

/**
 * M4 – luồng thật FE + BE + PostgreSQL (module-plan.md M4; FR-27, BR-31 → BR-33, BR-36):
 * GVCN phân công → thủ quỹ thấy menu Danh mục → đổi người (phân công mới trước, thu hồi cũ sau) → người cũ mất menu.
 * Học sinh seed bằng API của M3 (`createStudent`, `grantAccount`, `createActiveStudent`).
 */
test.skip(!isFullE2E, 'Chỉ chạy khi E2E_FULL=1 (cần backend + DB, xem docs/architecture/foundation-guide.md)');

/** Học sinh đăng nhập trong một context trình duyệt riêng (phiên của GVCN ở `page` không bị đổi) và mở quỹ. */
async function openFundAs(browser: Browser, student: RealStudent, fundId: string): Promise<Page> {
  const context = await browser.newContext({ baseURL: 'http://localhost:4200' });
  const page = await context.newPage();
  await page.goto('/login');
  await page.getByLabel('Email hoặc tên đăng nhập').fill(student.login);
  await page.getByLabel('Mật khẩu').fill(student.password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page).not.toHaveURL(/\/login$/);
  await page.goto(`/funds/${fundId}`);
  return page;
}

const nav = (page: Page) => page.getByRole('navigation', { name: 'Điều hướng chính' });

test('GVCN tạo quỹ → G-01 mở G-03 → banner "Quỹ chưa phân công thủ quỹ", chưa có thủ quỹ và lịch sử', async ({ page }) => {
  const api = new RealApi(page.request);
  await registerTeacher(api, 'Thầy Hùng M4');
  const { id: fundId } = await api.post<{ id: string }>('/funds', { name: unique('Quỹ lớp 5C ') });

  await page.goto('/funds');
  await page.getByRole('link', { name: /Quỹ lớp 5C/ }).click();
  await expect(page).toHaveURL(new RegExp(`/funds/${fundId}/treasurers$`));

  await expect(page.getByRole('heading', { name: 'Phân công thủ quỹ', level: 1 })).toBeVisible();
  await expect(page.getByRole('status').filter({ hasText: 'Quỹ chưa phân công thủ quỹ.' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Đang là thủ quỹ (0)' })).toBeVisible();
  await expect(page.getByText('Chưa có lịch sử phân công.')).toBeVisible();
});

test('phân công → thủ quỹ thấy menu Danh mục → đổi người: phân công mới, thu hồi cũ → người cũ mất menu', async ({ page, browser }) => {
  const api = new RealApi(page.request);
  await registerTeacher(api, 'Thầy Hùng M4');
  const { id: fundId } = await api.post<{ id: string }>('/funds', { name: unique('Quỹ lớp 5D ') });
  const khang = await createActiveStudent(api, fundId, 'Nguyễn Minh Khang');
  const trang = await createActiveStudent(api, fundId, 'Phạm Thu Trang');
  await createStudent(api, fundId, 'Lê Gia Bảo');
  const huy = await createStudent(api, fundId, 'Phạm Gia Huy');
  await grantAccount(api, fundId, huy.studentId, huy.fullName);

  // 1. Dialog chỉ cho chọn học sinh đã kích hoạt (BR-31); lý do hiện cạnh tên.
  await page.goto(`/funds/${fundId}/treasurers`);
  await expect(page.getByRole('status').filter({ hasText: 'Quỹ chưa phân công thủ quỹ.' })).toBeVisible();
  await page.getByRole('button', { name: '+ Phân công thủ quỹ' }).click();
  const dialog = page.getByRole('dialog', { name: 'Phân công thủ quỹ' });
  await expect(dialog.getByRole('radio', { name: 'Nguyễn Minh Khang' })).toBeEnabled();
  await expect(dialog.getByRole('radio', { name: 'Phạm Thu Trang' })).toBeEnabled();
  await expect(dialog.getByRole('radio', { name: 'Lê Gia Bảo' })).toBeDisabled();
  await expect(dialog.getByRole('radio', { name: 'Phạm Gia Huy' })).toBeDisabled();
  await expect(dialog.getByText('Cấp tài khoản ở trang Học sinh trước')).toBeVisible();
  await expect(dialog.getByText(/chưa đổi mật khẩu tạm/)).toBeVisible();

  await dialog.getByRole('radio', { name: 'Nguyễn Minh Khang' }).check();
  await dialog.getByRole('button', { name: 'Phân công', exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByText('Đã phân công Nguyễn Minh Khang làm thủ quỹ.')).toBeVisible();
  await expect(page.getByRole('status').filter({ hasText: 'Quỹ chưa phân công thủ quỹ.' })).toBeHidden();
  await expect(page.getByRole('heading', { name: 'Đang là thủ quỹ (1)' })).toBeVisible();
  await expect(page.getByText(new RegExp(`bởi Thầy Hùng M4 · ${khang.login}`))).toBeVisible();

  // Thủ quỹ duy nhất: nút "Thu hồi" khóa kèm giải thích (BR-32).
  const lastRevoke = page.getByRole('button', { name: 'Thu hồi' });
  await expect(lastRevoke).toBeDisabled();
  await expect(lastRevoke).toHaveAccessibleDescription('Thủ quỹ cuối cùng – phân công người mới trước');

  // 2. Thủ quỹ đăng nhập → menu Danh mục (Category.Manage).
  const khangPage = await openFundAs(browser, khang, fundId);
  await expect(nav(khangPage).getByRole('link', { name: 'Danh mục' })).toBeVisible();

  // 3. Đổi người: phân công Trang trước (Khang hiện "Đang là thủ quỹ", không chọn được) …
  await page.getByRole('button', { name: '+ Phân công thủ quỹ' }).click();
  await expect(dialog.getByRole('radio', { name: 'Nguyễn Minh Khang' })).toBeDisabled();
  await expect(dialog.getByText('Đang là thủ quỹ')).toBeVisible();
  await dialog.getByRole('radio', { name: 'Phạm Thu Trang' }).check();
  await dialog.getByRole('button', { name: 'Phân công', exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole('heading', { name: 'Đang là thủ quỹ (2)' })).toBeVisible();

  // … rồi thu hồi Khang (có xác nhận).
  await page.getByRole('button', { name: 'Thu hồi Nguyễn Minh Khang' }).click();
  const confirm = page.getByRole('alert').filter({ hasText: 'Thu hồi quyền thủ quỹ của Nguyễn Minh Khang?' });
  await confirm.getByRole('button', { name: 'Thu hồi' }).click();
  await expect(page.getByText('Đã thu hồi quyền thủ quỹ của Nguyễn Minh Khang.')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Đang là thủ quỹ (1)' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Thu hồi' })).toBeDisabled();

  // Lịch sử giữ cả hai lần phân công; dòng của Khang có người / thời điểm thu hồi (BR-33).
  const khangRow = page.getByRole('row', { name: /Nguyễn Minh Khang/ });
  await expect(khangRow).toContainText('Đã thu hồi');
  await expect(khangRow.getByRole('cell').nth(2)).toContainText('Thầy Hùng M4');
  await expect(page.getByRole('row', { name: /Phạm Thu Trang/ })).toContainText('Đang hiệu lực');

  // 4. Người cũ mất quyền ngay ở request kế tiếp: tải lại → không còn menu Danh mục; người mới có.
  await khangPage.reload();
  await expect(nav(khangPage).getByRole('link', { name: 'Tổng quan' })).toBeVisible();
  await expect(nav(khangPage).getByRole('link', { name: 'Danh mục' })).toBeHidden();
  await khangPage.goto(`/funds/${fundId}/categories`);
  await expect(khangPage).toHaveURL(/\/forbidden$/);
  await khangPage.context().close();

  const trangPage = await openFundAs(browser, trang, fundId);
  await expect(nav(trangPage).getByRole('link', { name: 'Danh mục' })).toBeVisible();
  await trangPage.context().close();

  // G-01 của GVCN: quỹ "Đã có thủ quỹ" với tên người mới.
  await page.goto('/funds');
  const card = page.getByRole('link', { name: /Quỹ lớp 5D/ });
  await expect(card).toContainText('Đã có thủ quỹ');
  await expect(card).toContainText('Phạm Thu Trang');
});
