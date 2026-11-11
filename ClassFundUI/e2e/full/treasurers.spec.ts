import { expect, test } from '@playwright/test';

import { RealApi, isFullE2E, registerTeacher } from '../support/real-api';

/**
 * M4 – luồng thật FE + BE + PostgreSQL (module-plan.md M4).
 * Phần cần học sinh có tài khoản đã kích hoạt (phân công → thủ quỹ thấy menu Danh mục → đổi người → người cũ mất menu)
 * cần API Học sinh của M3: bổ sung sau khi M3 merge (module-plan.md mục 1, đợt 2).
 */
test.skip(!isFullE2E, 'Chỉ chạy khi E2E_FULL=1 (cần backend + DB, xem docs/architecture/foundation-guide.md)');

test('GVCN tạo quỹ → G-01 mở G-03 → banner "Quỹ chưa phân công thủ quỹ", chưa có thủ quỹ và lịch sử', async ({ page }) => {
  const api = new RealApi(page.request);
  await registerTeacher(api, 'Thầy Hùng M4');
  const { id: fundId } = await api.post<{ id: string }>('/funds', { name: 'Quỹ lớp 5C' });

  await page.goto('/funds');
  await page.getByRole('link', { name: /Quỹ lớp 5C/ }).click();
  await expect(page).toHaveURL(new RegExp(`/funds/${fundId}/treasurers$`));

  await expect(page.getByRole('heading', { name: 'Phân công thủ quỹ', level: 1 })).toBeVisible();
  await expect(page.getByRole('status').filter({ hasText: 'Quỹ chưa phân công thủ quỹ.' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Đang là thủ quỹ (0)' })).toBeVisible();
  await expect(page.getByText('Chưa có lịch sử phân công.')).toBeVisible();
});
