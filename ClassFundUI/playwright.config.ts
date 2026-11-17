import { defineConfig, devices } from '@playwright/test';

/**
 * Hai chế độ (docs/testing/test-strategy.md):
 * - mặc định `npm run e2e`: chỉ Angular dev server, API giả lập (`e2e/mock`); `e2e/full` bị skip.
 * - `npm run e2e:full` (E2E_FULL=1): thêm ClassFund.Api thật (Neon dùng chung, đã cấu hình user-secrets);
 *   chỉ dùng dữ liệu seed demo, không xóa dữ liệu.
 */
const full = process.env['E2E_FULL'] === '1';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:4200',
    ...devices['Desktop Chrome'],
    viewport: { width: 1440, height: 900 },
    channel: 'chrome',
  },
  webServer: [
    ...(full
      ? [
          {
            command: 'dotnet run --project ../ClassFundAPI/src/ClassFund.Api',
            url: 'http://localhost:5286/api/health',
            reuseExistingServer: true,
            timeout: 180_000,
          },
        ]
      : []),
    {
      command: 'npm start',
      url: 'http://localhost:4200',
      reuseExistingServer: true,
      timeout: 180_000,
    },
  ],
});
