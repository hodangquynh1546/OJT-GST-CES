# SchoolFundUI

Workspace Angular của Quỹ Lớp. Cấu trúc và phiên bản theo `training-doc.md`; thiết kế theo Canvas (`docs/ui-design`).

| Thư mục | Nội dung |
|---|---|
| `src/app/core/` | `auth`, `config`, `interceptors`, `navigation`, `services` |
| `src/app/shared/` | `components`, `directives`, `models`, `pipes`, `utils` |
| `src/app/features/` | Mỗi feature một thư mục, route lazy-load |
| `e2e/` | Playwright |

| Lệnh | Tác dụng |
|---|---|
| `npm start` | Chạy dev ở cổng 4200, `/api` chuyển tới `http://localhost:5286` (`proxy.conf.json`) |
| `npm run build` | Build production vào `wwwroot` của `SchoolFund.Api` |
| `npm test` | Unit test (Karma, Chrome headless) |
| `npm run e2e` | Playwright, dùng Chrome đã cài trên máy |
| `npm run license` | Kích hoạt Kendo từ `KENDO_UI_LICENSE` hoặc `kendo-ui-license.txt` (không commit file này) |
