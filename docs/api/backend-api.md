# API Specification – Quỹ lớp (đặc tả 2.0)

> Cập nhật: 2026-11-05 · Mô hình **Giáo viên · Học sinh · Phụ huynh** (D-24 → D-33). Trạng thái triển khai: **Not Implemented** – mã nguồn cũ (từng cài đặt và kiểm thử đúng đặc tả này) đã xóa ngày 2026-11-05; đặc tả giữ nguyên làm Source of Truth ([implementation-status.md](../implementation/implementation-status.md)). Swagger đầy đủ: `/swagger` (Development).
> Thiết kế v1.1 / v1.2 (`/members`, đổi vai trò, phạm vi theo `FundMember`) **không triển khai** – xem Git history.

## Quy ước

| Mục | Quy ước |
|---|---|
| Prefix | `/api/v1/...` |
| Đăng nhập | Cookie `sf_session`; `login` / `register-teacher` / `change-password` trả `antiforgeryToken` → gửi header `X-XSRF-TOKEN` cho mọi POST / PUT |
| Phạm vi | Đường dẫn theo quỹ `/funds/{fundId}/…`. Vai trò (GVCN / Thủ quỹ / Học sinh / Phụ huynh) do backend suy ra từ phiên + dữ liệu; `fundId` chỉ chọn quỹ. Không có tham số `teacherId` / `role` |
| Lỗi | `application/problem+json` (`code`, `title`, `detail`, `traceId`, `correlationId`, `errors` khi 400). 400 `validation_failed` · 401 `unauthorized` / `auth.*` · 403 `forbidden` / `password.change_required` · 404 `not_found` (cả khi **không thuộc quỹ**) · 409 xung đột · 422 vi phạm quy tắc |
| Tiền / ngày | `long` VND > 0 · `yyyy-MM-dd` (giờ Việt Nam) |
| Chống lưu trùng | `POST …/transactions` bắt buộc `idempotencyKey`; gửi lại → 200, `replayed: true` |
| Bản công khai | Học sinh chưa phân công, phụ huynh: lọc `isPublic` trước tìm kiếm / đếm / phân trang; các trường `note`, `isPublic`, `cancelReason`, `updatedAt`, `createdByName`, `updatedByName`, `cancelledByName` **không có** trong JSON |
| Mật khẩu tạm | Chỉ có trong response của thao tác cấp (`Cache-Control: no-store`); không lưu dạng rõ, không xem lại được, không ghi log |

## Endpoint

### Auth

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| `POST` | `/auth/register-teacher` | Ẩn danh (rate limit) | `{ fullName, email, password, confirmPassword }` → 201 phiên (đăng nhập luôn). Luôn là giáo viên. 422 `user.email_taken`, `auth.password_policy` |
| `POST` | `/auth/login` | Ẩn danh (rate limit) | `{ login, password }` – `login` = email hoặc tên đăng nhập; **không có tham số vai trò** (D-34). Trường `email` cũ vẫn nhận. 401 `auth.invalid_credentials` / `auth.account_disabled` |
| `POST` | `/auth/logout` | Đăng nhập | 204 |
| `GET` | `/auth/me` | Đăng nhập | `{ id, kind, fullName, email, username, mustChangePassword, accountPermissions[], memberships[{ fundId, fundName, role, permissions[], linkedStudents[] }] }` |
| `PUT` | `/auth/me` | Đăng nhập | `{ fullName }` → me |
| `POST` | `/auth/change-password` | Đăng nhập | `{ currentPassword, newPassword }` → phiên mới; xóa cờ `mustChangePassword`. 422 `auth.wrong_password`, `auth.password_policy`, `auth.password_reused` |
| `GET` | `/auth/antiforgery` | Ẩn danh | Token mới |

### Funds

| Method | Path | Permission | Mô tả |
|---|---|---|---|
| `GET` | `/funds` | Đăng nhập (không còn mật khẩu tạm) | Quỹ tôi có vai trò: `{ id, name, role, permissions[], currentBalance, createdAt, treasurerStatus: Unassigned \| Assigned, studentCount (chỉ GVCN), treasurerNames[], linkedStudents[] (phụ huynh) }` |
| `POST` | `/funds` | `Fund.Create` (giáo viên) | `{ name }` → 201 `{ id }`; danh mục mặc định, chưa có thủ quỹ |
| `GET` | `/funds/{fundId}` | `Fund.View` | Tổng quan: `role`, `permissions`, `currentBalance`, `totalIncome`, `totalExpense`, `currentMonth`, `treasurerStatus`, `managers[{ fullName, role }]`, `recentTransactions` (theo phạm vi) |
| `PUT` | `/funds/{fundId}` | `Fund.Manage` (GVCN) | `{ name }` → 204 |
| `GET` | `/funds/{fundId}/reports/monthly?year&month` | `Fund.View` | Bản công khai: `transactions` chỉ khoản công khai; `byCategory` thêm `{ categoryId: null, categoryName: "Khoản khác", count: null }` theo loại (P1) |
| `GET` | `/funds/{fundId}/reports/yearly?year` | `Fund.View` | 12 tháng, số tổng |
| `GET` | `/funds/{fundId}/members` | `Member.View` (GVCN, thủ quỹ) | `[{ fullName, role }]` – không email / username |

### Transactions

| Method | Path | Permission | Mô tả |
|---|---|---|---|
| `GET` | `/funds/{fundId}/transactions` | `Fund.View` | Lọc `type`, `categoryId`, `from`, `to`, `search`, `status` (Active / Cancelled / All), `page`, `pageSize` |
| `GET` | `/funds/{fundId}/transactions/{id}` | `Fund.View` | Khoản không công khai với người xem công khai → 404 |
| `GET` | `/funds/{fundId}/transactions/{id}/history` | `Ledger.ViewFull` | `[{ occurredAt, action, actorName, oldValues, newValues }]` |
| `POST` | `/funds/{fundId}/transactions` | `Transaction.Manage` (thủ quỹ) | `{ type, amount, occurredOn, categoryId, description, note, isPublic = true, idempotencyKey }` → 201 / 200 replay |
| `PUT` | `/funds/{fundId}/transactions/{id}` | `Transaction.Manage` | `{ amount, occurredOn, categoryId, description, note, isPublic }`; ghi `UpdatedBy` |
| `POST` | `/funds/{fundId}/transactions/{id}/cancel` | `Transaction.Manage` | `{ reason }` **bắt buộc** (1–500); ghi `CancelledBy`. 422 `transaction.cancelled` |

### Categories

| Method | Path | Permission | Mô tả |
|---|---|---|---|
| `GET` | `/funds/{fundId}/categories?type&includeHidden` | `Fund.View` | GVCN / thủ quỹ: mọi danh mục + `transactionCount`. Công khai: chỉ danh mục đang hiện có ≥ 1 khoản công khai (X-01), không `transactionCount` |
| `POST` | `/funds/{fundId}/categories` | `Category.Manage` (thủ quỹ) | `{ type, name }` → 201. 422 `category.name_taken` |
| `PUT` | `/funds/{fundId}/categories/{id}` | `Category.Manage` | `{ name, isHidden }` → 204 |

### Students (chỉ GVCN – `Student.Manage`)

| Method | Path | Mô tả |
|---|---|---|
| `GET` | `/funds/{fundId}/students` | `[{ id, fullName, username, accountStatus: None \| PendingActivation \| Active \| Disabled, isTreasurer, parent { fullName, email, pendingActivation }, createdAt }]` |
| `POST` | `/funds/{fundId}/students` | `{ fullName, parentEmail?, parentFullName? }` → 201 `{ id, parent? }` (xem liên kết phụ huynh) |
| `GET` / `PUT` | `/funds/{fundId}/students/{id}` | Chi tiết / `{ fullName }` → 204 |
| `POST` | `/funds/{fundId}/students/{id}/account` | `{ username? }` → `{ login, temporaryPassword }`. 422 `student.account_exists`, `user.username`, `user.username_taken` |
| `POST` | `/funds/{fundId}/students/{id}/reset-password` | → `{ login, temporaryPassword }`; phiên cũ hết hiệu lực. 422 `student.no_account` |
| `POST` | `/funds/{fundId}/students/{id}/parent` | `{ email, fullName? }` → `{ accountCreated, credential: { login, temporaryPassword } \| null, emailSent: false }`. 422 `parent_link.exists`, `parent.email_in_use` |
| `POST` | `/funds/{fundId}/students/{id}/parent/unlink` | 204; 404 nếu chưa có liên kết |

### Treasurers (chỉ GVCN – `Treasurer.Assign`)

| Method | Path | Mô tả |
|---|---|---|
| `GET` | `/funds/{fundId}/treasurers` | Đang hiệu lực + lịch sử: `[{ id, studentId, studentName, isActive, assignedAt, assignedByName, revokedAt, revokedByName }]` |
| `POST` | `/funds/{fundId}/treasurers` | `{ studentId }` → 201 `{ id }`. 404 học sinh ngoài quỹ; 422 `treasurer.no_account`, `treasurer.account_disabled`, `treasurer.account_not_activated`, `treasurer.already_assigned` |
| `POST` | `/funds/{fundId}/treasurers/{assignmentId}/revoke` | 204. 422 `treasurer.last`; 409 `treasurer.already_revoked` |

`GET /api/health` – ẩn danh – 200 Healthy / 503 Unhealthy.

## Đánh giá thay đổi API so với R1 (*historical* – mã R1 / R3 đã xóa 2026-11-05)

| API cũ (R1) | API mới | Consumer hiện có | Kết luận |
|---|---|---|---|
| `GET/PUT /fund`, `/fund/reports/*` | `/funds/{fundId}`, `/funds/{fundId}/reports/*` | Không (chưa có frontend; Canvas là prototype tĩnh) | Bỏ đường dẫn cũ – một quỹ / người dùng không còn đúng |
| `/transactions…`, `/categories…` | `/funds/{fundId}/transactions…`, `/funds/{fundId}/categories…` | Không (chỉ Integration Test – đã cập nhật) | Như trên; body giữ nguyên + `isPublic` |
| `POST /auth/login { email }` | `{ login }` | Không | Vẫn nhận `email` (tương thích) |
| `GET /auth/me` (`fundName`, `permissions`) | `kind`, `accountPermissions`, `memberships[]` | Không | Đổi cấu trúc |
| `POST …/cancel { reason? }` | `reason` bắt buộc | Không | D-14 |
| CLI `create-treasurer` | `create-teacher` (`SCHOOLFUND_FUND_NAME` tùy chọn); `reset-password` / `*-account` nhận `SCHOOLFUND_LOGIN` (hoặc `SCHOOLFUND_EMAIL` cũ) | Người vận hành | Cập nhật hướng dẫn vận hành |
