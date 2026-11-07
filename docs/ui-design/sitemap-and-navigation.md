# Sitemap & Điều hướng

> v4.0 · 2026-11-07 · **Desktop Web only** · Canvas: trang "00 · Đọc trước" → D-02 (4 tab vai trò). API tính từ `/api/v1`. Backend: đã có; Frontend: shell + route (màn hình chưa làm).

## 1. Sitemap

| Mã | Màn hình | Route SPA | GVCN | Thủ quỹ | Học sinh | Phụ huynh | API chính |
|---|---|---|---|---|---|---|---|
| A-01 | Đăng ký giáo viên | `/register-teacher` | ẩn danh | – | – | – | `POST /auth/register-teacher` |
| A-02 | Đăng nhập (một ô email / tên đăng nhập, không chọn vai trò) | `/login` | ✔ | ✔ | ✔ | ✔ | `POST /auth/login` |
| A-03 | Đặt mật khẩu mới (bắt buộc khi còn mật khẩu tạm) | `/change-password` | – | ✔ | ✔ | ✔ | `POST /auth/change-password` |
| A-04 | Tài khoản (sửa họ tên, đổi mật khẩu, đăng xuất) | `/account` | ✔ | ✔ | ✔ | ✔ | `GET/PUT /auth/me`, `POST /auth/logout` |
| G-01 | Quỹ của tôi (+ Tạo quỹ) / Quỹ của con | `/funds` | ✔ + tạo | – | – | ✔ | `GET/POST /funds` |
| S-01 | Tổng quan | `/funds/:id` | đầy đủ | đầy đủ + nút thêm | công khai | công khai | `GET /funds/{id}` (overview) · `GET /funds/{id}/reports/yearly` (biểu đồ) |
| S-02 | Thu chi + chi tiết + lịch sử thay đổi | `/funds/:id/transactions(/:txId)` | đầy đủ, không ghi | đầy đủ + sửa / hủy | công khai | công khai | `GET /funds/{id}/transactions`, `GET …/{txId}`, `GET …/{txId}/history` |
| S-03 | Báo cáo (tháng / 12 tháng) | `/funds/:id/reports` | đầy đủ | đầy đủ | công khai + "Khoản khác" | công khai + "Khoản khác" | `/funds/{id}/reports/monthly`, `/yearly` |
| T-01 | Thêm / sửa / hủy giao dịch (dialog) | trên S-01, S-02 | – | ✔ | – | – | `POST/PUT …/transactions`, `…/cancel` |
| G-02 | Học sinh & phụ huynh | `/funds/:id/students` | ✔ | – | – | – | `GET/POST /funds/{id}/students`, `GET/PUT …/{sid}`, `POST …/{sid}/account`, `…/reset-password`, `…/parent`, `…/parent/unlink` |
| G-03 | Phân công thủ quỹ | `/funds/:id/treasurers` | ✔ | – | – | – | `GET/POST /funds/{id}/treasurers`, `POST …/{aid}/revoke` |
| G-04 | Thông tin quỹ | `/funds/:id/settings` | ✔ | – | – | – | `GET /funds` (đọc), `PUT /funds/{id}` |
| T-02 | Danh mục thu chi | `/funds/:id/categories` | – | ✔ | – | – | `GET/POST /funds/{id}/categories`, `PUT …/{cid}` |
| T-03 | Thành viên (chỉ xem) | `/funds/:id/members` | – (xem ở G-02) | ✔ | – | – | `GET /funds/{id}/members` |
| X-01 | 403 / 404 / 401 / 409 / 422 / mật khẩu tạm / quỹ chưa có thủ quỹ | `/forbidden`, `**` | ✔ | ✔ | ✔ | ✔ | board `States` |

- Sau đăng nhập (`/auth/me`): giáo viên → `/funds`; học sinh / thủ quỹ → `/funds/:id` (quỹ duy nhất); phụ huynh → `/funds` (Quỹ của con); `mustChangePassword` → `/change-password` cho mọi route.
- Route guard đọc `permissions` của quỹ trong `memberships`; truy cập URL không có quyền → E-403; quỹ không thuộc mình → E-404. Backend tự kiểm tra lại.

## 2. Điều hướng theo vai trò

| Mục menu | GVCN | Thủ quỹ | Học sinh | Phụ huynh |
|---|---|---|---|---|
| Quỹ của tôi / Quỹ của con / Đổi quỹ | ✔ | – | – | ✔ ("Quỹ của con") |
| Tổng quan | ✔ | ✔ | ✔ | ✔ |
| Thu chi | ✔ (không ghi) | ✔ + thêm / sửa / hủy | "Thu chi công khai" | "Thu chi công khai" |
| Báo cáo | ✔ | ✔ | ✔ | ✔ |
| Học sinh & phụ huynh | ✔ | – | – | – |
| Phân công thủ quỹ | ✔ | – | – | – |
| Thông tin quỹ | ✔ | – | – | – |
| Danh mục | – | ✔ | – | – |
| Thành viên | – | ✔ chỉ xem | – | – |
| Tài khoản, Đăng xuất | avatar | avatar | avatar | avatar |
| + Thêm khoản thu / chi | – | nút chính trên header trang S-01, S-02 | – | – |

Menu sinh từ permission (xem [roles-and-permissions.md](../business-analysis/roles-and-permissions.md)):
- `Fund.Create` (cấp tài khoản) → nút "+ Tạo quỹ".
- `Fund.View` → Tổng quan, Thu chi, Báo cáo. `Ledger.ViewFull` → chi tiết đầy đủ, lịch sử thay đổi.
- `Student.Manage` → Học sinh & phụ huynh. `Treasurer.Assign` → Phân công thủ quỹ. `Fund.Manage` → Thông tin quỹ.
- `Category.Manage` → Danh mục. `Member.View` → Thành viên (thủ quỹ). `Transaction.Manage` → nút thêm, sửa, hủy.

Bố cục điều hướng (desktop): AppHeader 56 px trên cùng + AppNav sidebar 232 px cố định bên trái, luôn hiện nhãn. Thứ tự mục menu theo bảng trên. Không có thanh dưới, không có chế độ thu gọn icon.
