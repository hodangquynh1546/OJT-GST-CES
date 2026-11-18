# Danh mục API (dự kiến)

> Hợp đồng **dự kiến** để hai developer làm song song. Module làm xong thì cập nhật cột "Trạng thái" và chi tiết request/response thật.
> Tiền tố `/api/v1`; `{f}` = `funds/{fundId}`. Permission theo `docs/business-analysis/roles-and-permissions.md`.

| Trạng thái | Ký hiệu |
|---|---|
| Foundation đã có | ✅ |
| Chưa làm | ⬜ |

## Foundation

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/auth/antiforgery` | Anonymous | – | ✅ |
| GET | `/api/health` (không version) | Anonymous | – | ✅ |

## M1 – Auth & tài khoản

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| POST | `/auth/register` `{ fullName, email, password }` → 201 `{ antiforgeryToken, user }` | Anonymous (rate limit) | FR-01 | ✅ |
| POST | `/auth/login` `{ email, password }` → `{ antiforgeryToken, user }` | Anonymous (rate limit) | FR-02 | ✅ |
| POST | `/auth/logout` → 204 | Đăng nhập, AllowTemporaryPassword | FR-02 | ✅ |
| GET | `/auth/me` | Đăng nhập, AllowTemporaryPassword | FR-03 | ✅ |
| PUT | `/auth/me` `{ fullName }` → user (còn mật khẩu tạm → 403 `password.change_required`) | Đăng nhập | FR-04 | ✅ |
| POST | `/auth/change-password` `{ currentPassword, newPassword }` → `{ antiforgeryToken, user }` | Đăng nhập, AllowTemporaryPassword | FR-03, FR-04 | ✅ |
| PUT | `/auth/me/avatar` (multipart `file`; ảnh JPG/PNG/WEBP ≤ 2 MB) → `{ attachmentId }`; thay ảnh cũ, xóa object cũ. 422 `file.invalid` | Đăng nhập | FR-04 | ✅ M5 (`AvatarController`) |
| GET | `/users/{userId}/avatar` (stream ảnh; chính mình hoặc người cùng quỹ đang hoạt động, không thì / chưa có ảnh → 404) | Đăng nhập | FR-04 | ✅ M5 (`AvatarController`) |

> Hai route ảnh đại diện do M5 cài trong `Features/Attachments/AvatarController` – `AuthController` của M1 không khai báo lại.

Mã lỗi M1: `auth.invalid_credentials` (401, dùng chung cho email lạ và mật khẩu sai), `auth.account_disabled` (401, chỉ báo sau khi mật khẩu đúng), `auth.email_exists` (409), `auth.password_policy` / `auth.invalid_email` / `auth.invalid_name` (422; form gửi sai thì 400 `validation_failed` kèm `errors`), `auth.current_password_incorrect` (**422**, không phải 401 để client không coi là hết phiên), `auth.password_unchanged` (422), `auth.rate_limited` (429).

`GET /auth/me` → `{ id, email, fullName, mustChangePassword, accountPermissions[], memberships[{ fundId, fundName, role, permissions[] }] }` (khớp `core/auth/auth.models.ts`).

## M2 – Quỹ

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/funds` → `[{ id, name, schoolYear, status, myRole, memberCount, generalBalance, pendingPaymentCount? }]` (chỉ quỹ mình là thành viên Active; `generalBalance` chỉ tính EventId null; `pendingPaymentCount` chỉ có với người có `Payment.Confirm`) | Đăng nhập | FR-08 | ✅ |
| POST | `/funds` `{ name (≤100), schoolYear ("2026-2027") }` → 201 `FundDto`; người tạo là Owner + danh mục mặc định, audit `fund.create` | `Fund.Create` (cấp tài khoản) | FR-05 | ✅ |
| GET | `/{f}` → `{ id, name, schoolYear, status, createdAt, ownerName, memberCount, myRole }` | `Fund.View` | FR-07 | ✅ |
| PUT | `/{f}` `{ name, schoolYear }` → `FundDto`; audit `fund.update` (before/after); 422 `fund.archived` | `Fund.Manage` | FR-06 | ✅ |

## M3 – Học sinh

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/members?search=&role=&status=&page=&pageSize=` → `PagedResult<{ id, userId, fullName, email, role, status, mustChangePassword, joinedAt, removedAt, canManageAccount }>` (mặc định `status=Active`; thứ tự Owner → Treasurer → học sinh theo tên) | `Member.Manage` | FR-11 | ✅ |
| POST | `/{f}/members` `{ email, fullName }` → 201 `{ email, outcome, member, temporaryPassword?, message? }` (đã là thành viên → 409 `member.already_exists`; quỹ Archived → 422 `fund.archived`) | `Member.Manage` | FR-09 | ✅ |
| POST | `/{f}/members/bulk` `{ emails[≤50] }` → `{ results[{ email, outcome (`Created`/`ExistingAdded`/`Reactivated`/`AlreadyExists`/`Invalid`), member?, temporaryPassword?, message? }] }` (mỗi email độc lập; mật khẩu tạm trả một lần; tên tạm của tài khoản mới = phần trước `@`; rate limit 10 lần/phút/người dùng → 429) | `Member.Manage` | FR-09, OQ-20 | ✅ |
| PUT | `/{f}/members/{memberId}` `{ fullName }` → member (AZ-02, AZ-07) | `Member.Manage` | FR-11 | ✅ |
| POST | `/{f}/members/{memberId}/reset-password` → `{ temporaryPassword }` (đổi hash + SecurityStamp + MustChangePassword; AZ-02, AZ-07) | `Member.Manage` | FR-10 | ✅ |
| GET | `/{f}/members/{memberId}/removal-preview` → `{ unpaidCount, pendingPaymentCount, balance, canRemove, blockedReason }` (cảnh báo trước khi ngừng, OQ-15) | `Member.Manage` | FR-11 | ✅ |
| POST | `/{f}/members/{memberId}/remove` → `{ cancelledAssignments }` (422 `member.has_balance` / `member.pending_payments` / `member.cannot_remove_owner`; 409 `member.already_removed`) | `Member.Manage` | FR-11 | ✅ |

## M4 – Thủ quỹ

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/treasurers` | `Treasurer.Manage` | FR-12 | ⬜ |
| POST | `/{f}/treasurers` `{ memberId }` | `Treasurer.Manage` | FR-12 | ⬜ |
| POST | `/{f}/treasurers/{memberId}/revoke` | `Treasurer.Manage` | FR-12, FR-13 | ⬜ |

## M5 – Danh mục & chứng từ

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/categories?type=&activeOnly=` → `[{ id, name, type, isSystem, isActive, usageCount }]` | `Fund.View` | FR-14 | ✅ |
| POST | `/{f}/categories` `{ name (≤100), type }` → 201; 409 `category.duplicate_name` (trùng tên cùng loại, không phân biệt hoa thường); 422 `fund.archived` | `Category.Manage` | FR-14 | ✅ |
| PUT | `/{f}/categories/{id}` `{ name, isActive }` – đổi tên, ngừng dùng / dùng lại (không có xóa) | `Category.Manage` | FR-14 | ✅ |
| POST | `/{f}/attachments` (multipart: `file`, `kind` = `Receipt`/`PaymentProof`/`Document`) → 201 `{ id, kind, fileName, contentType, sizeBytes, uploadedAt }`; file lên Neon Object Storage, `OwnerId` null tới khi gắn. Kiểm loại + dung lượng + chữ ký đầu file → 422 `file.invalid`; quỹ Archived → 422 `fund.archived`. Đồng thời dọn file chưa gắn > 24h của quỹ | Route: `Fund.View`; service: `Ledger.Manage` (Receipt, Document) hoặc `Payment.Submit` (PaymentProof), thiếu → 403 | FR-15 | ✅ |
| GET | `/{f}/attachments/{id}` (stream file qua API, `nosniff`, `no-store`; kiểm AZ-05 – không được xem → 404; không trả URL bucket) | `Fund.View` | FR-17 | ✅ |

**Dùng trong module khác (không phải endpoint):** `IAttachmentsService.AttachToAsync(fundId, ids, ownerId, allowedKinds)` – gọi trong transaction của M6/M7 để gắn file (file phải thuộc quỹ, chưa gắn, do chính người dùng tải lên, đúng loại; không thì 422 `attachment.unusable`), `ListByOwnerAsync`; `ICategoriesService.EnsureUsableAsync` – 422 `category.unusable`.

## M6 – Khoản thu & thanh toán

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/charges?scope=&eventId=&status=` | `Fund.View` (học sinh: chỉ phạm vi được thấy) | FR-18, FR-19 | ⬜ |
| POST | `/{f}/charges` `{ scope, eventId?, title, purpose, amountPerPerson, dueDate }` | `Charge.Manage` | FR-18, FR-19 | ⬜ |
| GET | `/{f}/charges/{id}/assignments` (đã đóng/chưa đóng) | `Charge.Manage` | FR-24 | ⬜ |
| POST | `/{f}/charges/{id}/close` · `/cancel` | `Charge.Manage` | – | ⬜ |
| GET | `/{f}/me/assignments` | `Payment.Submit` | FR-20, FR-23 | ⬜ |
| POST | `/{f}/me/assignments/{id}/payments` `{ method: Cash \| BankTransfer \| PersonalBalance, transferCode?, attachmentId? }` – `PersonalBalance` dùng cho khoản chung và sự kiện (OQ-10) | `Payment.Submit` | FR-20, FR-22, FR-25 | ⬜ |
| GET | `/{f}/payments?status=Submitted&scope=` | `Payment.Confirm` | FR-21 | ⬜ |
| POST | `/{f}/payments/{id}/confirm` | `Payment.Confirm` | FR-21, FR-22 | ⬜ |
| POST | `/{f}/payments/{id}/reject` `{ reason }` | `Payment.Confirm` | FR-21 | ⬜ |

## M7 – Thu chi

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/ledger-entries?scope=&eventId=&type=&categoryId=&from=&to=` | `Fund.View` | FR-28 | ⬜ |
| POST | `/{f}/ledger-entries` `{ scope, eventId?, categoryId, title, purpose, amount, transactionDate, note?, attachmentIds[] }` – **chỉ ghi chi** (OQ-18): ghi thu = tạo khoản thu (`POST /{f}/charges`). 422 `ledger.exceeds_balance` khi chi vượt số dư phạm vi (BR-38). | `Ledger.Manage` | FR-26, FR-27, FR-16 | ⬜ |
| GET | `/{f}/ledger-entries/{id}` | `Fund.View` | FR-27 | ⬜ |
| POST | `/{f}/ledger-entries/{id}/reverse` `{ reason }` | `Ledger.Manage` | FR-29 | ⬜ |

## M8 – Sự kiện

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/events` | `Event.Participate` | FR-30 | ⬜ |
| POST | `/{f}/events` | `Event.Manage` | FR-30 | ⬜ |
| GET | `/{f}/events/{eventId}` | `Event.Participate` | FR-34 | ⬜ |
| PUT | `/{f}/events/{eventId}` | `Event.Manage` | FR-30 | ⬜ |
| POST | `/{f}/events/{eventId}/cancel` · `/complete` (kết thúc: loại người chưa đóng khoản nào, BR-19; bỏ `/lock-list`, OQ-18) | `Event.Manage` | FR-30, FR-33 | ⬜ |
| GET | `/{f}/events/{eventId}/participants?paymentStatus=` | `Event.Participate` (thủ quỹ: chỉ người `Accepted` + đã đóng/phải đóng + tình trạng; học sinh: danh sách tên + trả lời) | FR-32, FR-34 | ⬜ |
| PUT | `/{f}/events/{eventId}/me/response` `{ response }` | `Event.Participate` | FR-31 | ⬜ |

## M9 – Quyết toán

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/events/{eventId}/settlement/preview?surplusMethod=` (luôn chia theo tỷ lệ đã đóng, OQ-18) | `Settlement.Execute` | FR-35, FR-36 | ⬜ |
| POST | `/{f}/events/{eventId}/settlement` | `Settlement.Execute` | FR-35–FR-39 | ⬜ |
| GET | `/{f}/events/{eventId}/settlement` | `Event.Participate` | FR-50 | ⬜ |
| POST | `/{f}/events/{eventId}/settlement/lines/{lineId}/refunded` | `Settlement.Execute` | FR-37 | ⬜ |

## M10 – Số dư cá nhân

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/me/balance` → `{ balance, pendingWithdrawal?, history[] }` | `Balance.ViewOwn` | FR-40, FR-42 | ⬜ |
| POST | `/{f}/me/withdrawal-requests` (không body – rút toàn bộ số dư, OQ-11) | `Balance.Withdraw` | FR-41 | ⬜ |
| GET | `/{f}/withdrawal-requests?status=` | `Balance.Handle` | FR-41 | ⬜ |
| POST | `/{f}/withdrawal-requests/{id}/paid` · `/reject` | `Balance.Handle` | FR-41 | ⬜ |

## M11 – Thông báo

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/me/notifications?unreadOnly=` | `Fund.View` | FR-44 | ⬜ |
| GET | `/{f}/me/notifications/unread-count` | `Fund.View` | FR-44 | ⬜ |
| POST | `/{f}/me/notifications/{id}/read` · `/me/notifications/read-all` | `Fund.View` | FR-44 | ⬜ |

## M12 – Audit log

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/audit-logs?from=&to=&actorId=&scope=&eventId=&action=&selfOnly=` | `Audit.View` | FR-45–FR-47 | ⬜ |

## M13 – Báo cáo

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/reports/general-overview` | `Fund.View` | FR-48 | ⬜ |
| GET | `/{f}/reports/general?from=&to=` | `Fund.View` (học sinh: phần công khai) | FR-49 | ⬜ |
| GET | `/{f}/reports/events/{eventId}` | `Event.Participate` | FR-50 | ⬜ |
| GET | `/{f}/reports/students` · `/students/{memberId}` | `Report.View` | FR-51 | ⬜ |
| GET | `/{f}/me/report` | `Fund.View` | FR-51 | ⬜ |
| GET | `/{f}/reports/year-end` | `Report.View` | FR-52 | ⬜ |

## M14 – Quyết toán cuối năm quỹ chung

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/year-end-settlement/preview?surplusMethod=&allocation=` | `Settlement.Execute` | FR-53 | ⬜ |
| POST | `/{f}/year-end-settlement` (một lần; quỹ → Archived) | `Settlement.Execute` | FR-53, FR-54 | ⬜ |
| GET | `/{f}/year-end-settlement` | `Fund.View` | FR-53 | ⬜ |
| POST | `/{f}/year-end-settlement/lines/{lineId}/refunded` | `Settlement.Execute` | FR-53 | ⬜ |
