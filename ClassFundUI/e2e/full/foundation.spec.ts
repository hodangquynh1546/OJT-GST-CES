import { expect, test } from '@playwright/test';

import { RealApi, isFullE2E } from '../support/real-api';

/** Kiểm tra hạ tầng e2e thật: SPA → proxy → ClassFund.Api → PostgreSQL. Module thêm file `e2e/full/<module>.spec.ts`. */
test.skip(!isFullE2E, 'Chỉ chạy khi E2E_FULL=1 (cần backend + DB, xem docs/architecture/foundation-guide.md)');

test('API thật sống và DB kết nối được', async ({ request }) => {
  const response = await request.get('/api/health');
  expect(response.status()).toBe(200);
});

test('API thật trả ProblemDetails cho route không tồn tại và cấp token antiforgery', async ({ request }) => {
  const response = await request.get('/api/v1/khong-ton-tai');
  expect(response.status()).toBe(404);
  expect((await response.json()).code).toBe('not_found');
  await new RealApi(request).get<{ antiforgeryToken: string }>('/auth/antiforgery');
});

test('chưa đăng nhập: SPA chuyển về /login', async ({ page }) => {
  await page.goto('/funds');
  await expect(page).toHaveURL(/\/login$/);
});
