import { Browser, Page, expect, test } from '@playwright/test';

import { E2E_PASSWORD, RealApi, createActiveStudent, createStudent, isFullE2E, registerTeacher, unique } from '../support/real-api';

/**
 * M3 – luồng thật FE + BE + PostgreSQL (module-plan.md M3): GVCN thêm học sinh kèm email phụ huynh → cấp tài khoản →
 * học sinh đăng nhập bằng mật khẩu tạm → A-03 → "Đang hoạt động"; phụ huynh đăng nhập → thấy "Quỹ của con".
 */
test.skip(!isFullE2E, 'Chỉ chạy khi E2E_FULL=1 (cần backend + DB, xem docs/architecture/foundation-guide.md)');

/** Đọc thông tin đăng nhập trong hộp thoại mật khẩu tạm rồi tick xác nhận và đóng. */
async function readAndCloseCredential(page: Page, title: string | RegExp): Promise<{ login: string; password: string }> {
  const dialog = page.getByRole('dialog', { name: title });
  await expect(dialog).toBeVisible();
  const login = (await dialog.getByTestId('credential-login').textContent())!.trim();
  const password = (await dialog.getByTestId('credential-password').textContent())!.trim();
  await dialog.getByLabel('Tôi đã lưu / chuyển thông tin này').check();
  await dialog.getByRole('button', { name: 'Đóng', exact: true }).click();
  await expect(dialog).toBeHidden();
  return { login, password };
}

async function signInAndSetPassword(browser: Browser, credential: { login: string; password: string }): Promise<Page> {
  const context = await browser.newContext({ baseURL: 'http://localhost:4200' });
  const page = await context.newPage();
  await page.goto('/login');
  await page.getByLabel('Email hoặc tên đăng nhập').fill(credential.login);
  await page.getByLabel('Mật khẩu').fill(credential.password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  // Mật khẩu tạm → buộc đổi mật khẩu (A-03) trước khi dùng bất kỳ trang nào khác.
  await expect(page).toHaveURL(/\/change-password$/);
  await page.getByLabel('Mật khẩu tạm').fill(credential.password);
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill(E2E_PASSWORD);
  await page.getByLabel('Nhập lại mật khẩu mới').fill(E2E_PASSWORD);
  await page.getByRole('button', { name: 'Lưu và tiếp tục' }).click();
  await expect(page).not.toHaveURL(/\/change-password$/);
  return page;
}

test('GVCN thêm học sinh + email phụ huynh → cấp tài khoản → học sinh và phụ huynh đăng nhập, đổi mật khẩu tạm', async ({ page, browser }) => {
  const api = new RealApi(page.request);
  await registerTeacher(api, 'Cô Hạnh M3');
  const fund = await api.post<{ id: string }>('/funds', { name: unique('Quỹ lớp 5A ') });
  const parentEmail = `${unique('ph')}@example.vn`;

  await page.goto(`/funds/${fund.id}/students`);
  await expect(page.getByText('Chưa có học sinh nào.')).toBeVisible();

  // Thêm học sinh kèm email phụ huynh mới → tài khoản phụ huynh + mật khẩu tạm một lần.
  await page.getByRole('button', { name: '+ Thêm học sinh' }).click();
  const add = page.getByRole('dialog', { name: 'Thêm học sinh' });
  await add.getByRole('textbox', { name: 'Họ tên học sinh *' }).fill('Phạm Đức Huy');
  await add.getByRole('textbox', { name: 'Email phụ huynh (tùy chọn)' }).fill(parentEmail);
  await add.getByRole('textbox', { name: 'Họ tên phụ huynh (tùy chọn)' }).fill('Phạm Văn Long');
  await add.getByRole('button', { name: 'Lưu học sinh' }).click();
  const parent = await readAndCloseCredential(page, 'Tài khoản phụ huynh mới');
  expect(parent.login).toBe(parentEmail);
  expect(parent.password).toMatch(/^[A-HJ-NP-Z][a-km-np-z]{3}-[a-km-np-z]{4}-[2-9]{4}-[a-km-np-z]{4}$/);

  const row = page.getByRole('row', { name: /Phạm Đức Huy/ });
  await expect(row).toContainText('Chưa cấp');
  await expect(row).toContainText('Phạm Văn Long');
  await expect(row).toContainText(`${parentEmail} · chờ kích hoạt`);

  // Cấp tài khoản: username gợi ý dạng huy.pham.NNN.
  await page.getByRole('button', { name: 'Mở Phạm Đức Huy' }).click();
  const drawer = page.getByRole('dialog', { name: 'Phạm Đức Huy' });
  await drawer.getByRole('button', { name: 'Cấp tài khoản & mật khẩu tạm' }).click();
  const student = await readAndCloseCredential(page, 'Tài khoản học sinh – Phạm Đức Huy');
  expect(student.login).toMatch(/^huy\.pham\.\d{3}$/);
  await expect(row).toContainText('Chờ kích hoạt');
  await expect(row).toContainText(student.login);

  // Học sinh đăng nhập bằng mật khẩu tạm → A-03 → vào quỹ; GVCN thấy "Đang hoạt động".
  const studentPage = await signInAndSetPassword(browser, student);
  await studentPage.goto(`/funds/${fund.id}/students`);
  await expect(studentPage.getByText('Trang này không dành cho vai trò của bạn')).toBeVisible();
  await studentPage.context().close();
  await page.reload();
  await expect(page.getByRole('row', { name: /Phạm Đức Huy/ })).toContainText('Đang hoạt động');

  // Phụ huynh đăng nhập → đổi mật khẩu tạm → "Quỹ của con".
  const parentPage = await signInAndSetPassword(browser, parent);
  await parentPage.goto('/funds');
  await expect(parentPage.getByRole('heading', { name: 'Quỹ của con' })).toBeVisible();
  await expect(parentPage.getByText('Phạm Đức Huy')).toBeVisible();
  await parentPage.context().close();
  await page.reload();
  await expect(page.getByRole('row', { name: /Phạm Đức Huy/ })).toContainText(`${parentEmail}`);
  await expect(page.getByRole('row', { name: /Phạm Đức Huy/ })).not.toContainText('chờ kích hoạt');

  // Cấp lại mật khẩu tạm → học sinh phải đổi mật khẩu lại; gỡ liên kết phụ huynh → phụ huynh mất quyền xem quỹ.
  await page.getByRole('button', { name: 'Mở Phạm Đức Huy' }).click();
  await drawer.getByRole('button', { name: 'Cấp lại mật khẩu tạm' }).click();
  await drawer.getByRole('button', { name: 'Cấp lại', exact: true }).click();
  const reset = await readAndCloseCredential(page, 'Mật khẩu tạm mới – Phạm Đức Huy');
  expect(reset.login).toBe(student.login);
  expect(reset.password).not.toBe(student.password);
  await expect(page.getByRole('row', { name: /Phạm Đức Huy/ })).toContainText('Chờ kích hoạt');

  await drawer.getByRole('button', { name: 'Gỡ liên kết' }).click();
  await drawer.getByRole('button', { name: 'Gỡ liên kết' }).last().click();
  await expect(page.getByRole('row', { name: /Phạm Đức Huy/ })).toContainText('— Chưa liên kết');
});

test('helper seed: createStudent / createActiveStudent dùng được cho module khác (M4)', async ({ page }) => {
  const api = new RealApi(page.request);
  await registerTeacher(api, 'Cô Hạnh M3 seed');
  const fund = await api.post<{ id: string }>('/funds', { name: unique('Quỹ lớp 5B ') });
  const none = await createStudent(api, fund.id, 'Lê Gia Bảo');
  const active = await createActiveStudent(api, fund.id, 'Nguyễn Minh Khang');

  await page.goto(`/funds/${fund.id}/students`);
  await expect(page.getByRole('row', { name: /Lê Gia Bảo/ })).toContainText('Chưa cấp');
  await expect(page.getByRole('row', { name: /Nguyễn Minh Khang/ })).toContainText('Đang hoạt động');
  expect(none.studentId).not.toBe(active.studentId);
});
