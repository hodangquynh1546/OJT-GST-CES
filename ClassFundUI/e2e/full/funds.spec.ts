import { expect, test } from '@playwright/test';

import { RealApi, isFullE2E, registerTeacher } from '../support/real-api';

/** M2 – luồng thật FE + BE + PostgreSQL (module-plan.md M2): GVCN đăng ký (M1) → tạo 2 quỹ trên G-01 → đổi tên ở G-04. */
test.skip(!isFullE2E, 'Chỉ chạy khi E2E_FULL=1 (cần backend + DB, xem docs/architecture/foundation-guide.md)');

test('GVCN đăng ký → tạo 2 quỹ trên G-01 → đổi tên ở G-04 → header và danh sách cập nhật', async ({ page }) => {
  // Cookie phiên dùng chung giữa page.request và trang → trang đã đăng nhập bằng GVCN mới.
  await registerTeacher(new RealApi(page.request), 'Cô Hạnh M2');

  await page.goto('/funds');
  await expect(page.getByText('Bạn chưa có quỹ nào')).toBeVisible();

  for (const name of ['Quỹ lớp 5A', 'Quỹ lớp 5B']) {
    await page.getByRole('button', { name: /^\+ Tạo quỹ/ }).first().click();
    const dialog = page.getByRole('dialog', { name: 'Tạo quỹ mới' });
    await dialog.getByRole('textbox', { name: 'Tên quỹ *' }).fill(name);
    await dialog.getByRole('button', { name: 'Tạo quỹ', exact: true }).click();
    await expect(dialog).toBeHidden();
    const card = page.getByRole('link', { name: new RegExp(name) });
    await expect(card).toContainText('Chưa phân công thủ quỹ');
    await expect(card).toContainText('0 học sinh');
    await expect(card).toContainText('0 đ');
  }
  await expect(page.getByText('Các quỹ bạn là giáo viên chủ nhiệm · 2 quỹ')).toBeVisible();

  const fundId = (await page.getByRole('link', { name: /Quỹ lớp 5A/ }).getAttribute('href'))!.split('/')[2];
  await page.goto(`/funds/${fundId}/settings`);
  const form = page.getByRole('form', { name: 'Thông tin quỹ' });
  await expect(form).toContainText('Cô Hạnh M2 (bạn)');
  await form.getByRole('textbox', { name: 'Tên quỹ *' }).fill('Quỹ lớp 5A – 2026');
  await form.getByRole('button', { name: 'Lưu' }).click();
  await expect(page.getByText('Đã đổi tên quỹ.')).toBeVisible();
  await expect(page.locator('app-header')).toContainText('Quỹ lớp 5A – 2026');

  await page.goto('/funds');
  await expect(page.getByRole('link', { name: /Quỹ lớp 5A – 2026/ })).toBeVisible();
});
