import { defineConfig, devices } from '@playwright/test';

/**
 * Hai chế độ (docs/testing/test-strategy.md):
 * - mặc định `npm run e2e`: chỉ Angular dev server, API giả lập (`e2e/mock`); `e2e/full` bị skip.
 * - `npm run e2e:full` (E2E_FULL=1): thêm ClassFund.Api thật (Neon dùng chung, đã cấu hình user-secrets);
 *   chỉ dùng dữ liệu seed demo, không xóa dữ liệu.
 */
const full = process.env['E2E_FULL'] === '1';
// Cổng giao diện / API: mặc định 4200 / 5286; đặt E2E_UI_PORT / E2E_API_PORT để chạy bộ riêng (ví dụ DB Docker tạm).
const uiPort = process.env['E2E_UI_PORT'] ?? '4200';
const apiPort = process.env['E2E_API_PORT'] ?? '5286';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  reporter: 'list',
  use: {
    baseURL: `http://localhost:${uiPort}`,
    ...devices['Desktop Chrome'],
    viewport: { width: 1440, height: 900 },
    channel: 'chrome',
  },
  webServer: [
    ...(full
      ? [
          {
            command: 'dotnet run --project ../ClassFundAPI/src/ClassFund.Api',
            url: `http://localhost:${apiPort}/api/health`,
            reuseExistingServer: true,
            timeout: 180_000,
          },
        ]
      : []),
    {
      command: 'npm start',
      url: `http://localhost:${uiPort}`,
      reuseExistingServer: true,
      timeout: 180_000,
    },
  ],
});
