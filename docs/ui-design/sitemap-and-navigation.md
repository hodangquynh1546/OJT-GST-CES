# Sitemap & Điều hướng

> v3.1 · 2026-11-05 · Canvas: trang "00 · Thiết kế & phân quyền" → D-02 (4 tab vai trò). Trạng thái triển khai: **Not Implemented**.

## 1. Sitemap

| Mã | Màn hình | Route SPA | GVCN | Thủ quỹ | Học sinh | Phụ huynh | API chính |
|---|---|---|---|---|---|---|---|
| A-01 | Đăng ký giáo viên | `/register-teacher` | ẩn danh | – | – | – | `POST /auth/register-teacher` |
| A-02 | Đăng nhập (một ô email / tên đăng nhập, không chọn vai trò) | `/login` | ✔ | ✔ | ✔ | ✔ | `POST /auth/login` |
| A-03 | Đặt mật khẩu mới (bắt buộc khi còn mật khẩu tạm) | `/change-password` | – | ✔ | ✔ | ✔ | `POST /auth/change-password` |
| A-04 | Tài khoản (sửa họ tên, đổi mật khẩu, đăng xuất) | `/account` | ✔ | ✔ | ✔ | ✔ | `GET/PUT /auth/me`, `POST /auth/logout` |
| G-01 | Quỹ của tôi (+ Tạo quỹ) / Quỹ của con | `/funds` | ✔ + tạo | – | – | ✔ | `GET/POST /funds` |
| S-01 | Tổng quan | `/funds/:id` | đầy đủ | đầy đủ + nút thêm | công khai | công khai | `GET /funds/{id}` |
| S-02 | Thu chi + chi tiết + lịch sử thay đổi | `/funds/:id/transactions(/:txId)` | đầy đủ, không ghi | đầy đủ + sửa / hủy | công khai | công khai | `/funds/{id}/transactions…` |
| S-03 | Báo cáo (tháng / 12 tháng) | `/funds/:id/reports` | đầy đủ | đầy đủ | công khai + "Khoản khác" | công khai + "Khoản khác" | `/funds/{id}/reports/monthly`, `/yearly` |
| T-01 | Thêm / sửa / hủy giao dịch (dialog) | trên S-01, S-02 | – | ✔ | – | – | `POST/PUT …/transactions`, `…/cancel` |
| G-02 | Học sinh & phụ huynh | `/funds/:id/students` | ✔ | – | – | – | `/funds/{id}/students…` |
| G-03 | Phân công thủ quỹ | `/funds/:id/treasurers` | ✔ | – | – | – | `/funds/{id}/treasurers…` |
| G-04 | Thông tin quỹ | `/funds/:id/settings` | ✔ | – | – | – | `PUT /funds/{id}` |
| T-02 | Danh mục thu chi | `/funds/:id/categories` | – | ✔ | – | – | `/funds/{id}/categories` |
| T-03 | Thành viên (chỉ xem) | `/funds/:id/members` | – (xem ở G-02) | ✔ | – | – | `GET /funds/{id}/members` |
| E-xx | 403 / 404 / 401 / 409 / mật khẩu tạm / quỹ chưa có thủ quỹ | – | | | | | `States`, `MobileStates` |

- Sau đăng nhập (`/auth/me`): giáo viên → `/funds`; học sinh / thủ quỹ → `/funds/:id` (quỹ duy nhất); phụ huynh → `/funds` (Quỹ của con); `mustChangePassword` → `/change-password` cho mọi route.
- Route guard đọc `permissions` của quỹ trong `memberships`; truy cập URL không có quyền → E-403; quỹ không thuộc mình → E-404. Backend tự kiểm tra lại.

## 2. Điều hướng theo vai trò

| Mục menu | GVCN | Thủ quỹ | Học sinh | Phụ huynh | Mobile |
|---|---|---|---|---|---|
| Quỹ của tôi / Quỹ của con / Đổi quỹ | ✔ | – | – | ✔ | GVCN: tab "Quỹ"; phụ huynh: "Quỹ của con" |
| Tổng quan | ✔ | ✔ | ✔ | ✔ | Tab 1 |
| Thu chi | ✔ (không ghi) | ✔ + thêm / sửa / hủy | "Thu chi công khai" | "Thu chi công khai" | Tab 2 |
| Báo cáo | ✔ | ✔ | ✔ | ✔ | Tab 3 |
| Học sinh & phụ huynh | ✔ | – | – | – | GVCN: tab "Học sinh" |
| Phân công thủ quỹ | ✔ | – | – | – | GVCN: tab "Thủ quỹ" |
| Thông tin quỹ | ✔ | – | – | – | GVCN: trong "Quỹ" |
| Danh mục | – | ✔ | – | – | Thủ quỹ: "Khác" |
| Thành viên | – | ✔ chỉ xem | – | – | Thủ quỹ: "Khác" |
| Tài khoản, Đăng xuất | avatar | avatar | avatar | avatar | "Khác" / "Tài khoản" |
| + Thêm khoản | – | nút chính | – | – | Nút nổi (FAB) |

Menu sinh từ permission (xem [roles-and-permissions.md](../business-analysis/roles-and-permissions.md)):
- `Fund.Create` (cấp tài khoản) → nút "+ Tạo quỹ".
- `Fund.View` → Tổng quan, Thu chi, Báo cáo. `Ledger.ViewFull` → chi tiết đầy đủ, lịch sử thay đổi.
- `Student.Manage` → Học sinh & phụ huynh. `Treasurer.Assign` → Phân công thủ quỹ. `Fund.Manage` → Thông tin quỹ.
- `Category.Manage` → Danh mục. `Member.View` → Thành viên (thủ quỹ). `Transaction.Manage` → nút thêm, sửa, hủy.
