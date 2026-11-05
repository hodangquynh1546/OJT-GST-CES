# Rà soát & kế hoạch điều chỉnh – 3 vai trò (2026-11-04)

> Task: "Điều chỉnh phạm vi nghiệp vụ, phân quyền và thiết kế lại UI/UX".
> Trạng thái: **thiết kế xong, chờ duyệt**. Chưa sửa code backend; chưa tạo migration.
> **2026-11-05:** kế hoạch R2 trong file này **không triển khai** – thay bằng R3 (mô hình Giáo viên · Học sinh · Phụ huynh, D-24 → D-33). Xem `backend-progress.md`, `backend-technical-decisions.md` (TD-80 → TD-88).

## 1. Hiểu nhầm đã sửa
- **v1.0** giả định hệ thống chỉ có thủ quỹ, và 1 tài khoản = 1 quỹ qua `Fund.OwnerUserId`.
- **Đúng**: một quỹ lớp, nhiều tài khoản với 3 vai trò:
  - Thủ quỹ quản lý.
  - GVCN chỉ xem toàn bộ.
  - Phụ huynh chỉ xem dữ liệu công khai.

## 2. Tài liệu – giữ / sửa / bỏ

| Nhóm | Giữ nguyên | Đã sửa (giả định chỉ có thủ quỹ) | Đã bỏ |
|---|---|---|---|
| Requirements | Mục tiêu ghi nhanh, NFR số dư / chống trùng | Phạm vi, người dùng, FR-01 → FR-13 có cột vai trò, NFR-04 / 07 / 08 | "Không có tài khoản phụ huynh, giáo viên" |
| Open questions | D-02 → D-08, Q-01 → Q-04, Q-06 → Q-10 | D-01 (thêm 3 vai trò). Thêm D-13 → D-15 (đã chốt), D-09 → D-12 (tạm), Q-11, Q-12 | Q-05 (đã trả lời) |
| Business rules | BR-01 → BR-04, BR-07 → BR-10, BR-12 | BR-05, BR-06 (lý do bắt buộc), BR-11, BR-13; thêm BR-14 → BR-20, P-07 → P-10 | – |
| Roles & permissions | Nguyên tắc permission-based, 404 ngoài phạm vi | Viết lại: 3 vai trò, 7 permission, phạm vi dữ liệu | "Một loại người dùng" |
| Database | 5 bảng, công thức số dư, trigger | `FundMember`, `IsPublic`, `MustChangePassword`, DD-09 → DD-13, bản công khai | – |
| API | 17 endpoint v1.0 | Mục "Thay đổi đã thiết kế cho v1.1" | `Report.View` (khi triển khai) |
| UI/UX | Nguyên tắc ghi nhanh, thu / chi rõ ràng | Viết lại toàn bộ v2.0 theo Canvas | S-01 → S-07 cũ (gộp / đổi mã) |

## 3. Backend / DB – giữ / sửa / thiếu / thừa

| Hạng mục | Quyết định | Chi tiết |
|---|---|---|
| Domain `User`, `Fund`, `Category`, `FundTransaction`, `MonthPeriod`, `MonthlyStatement`, `AuditLog` | **Giữ** | Thêm thuộc tính: `User.MustChangePassword`, `FundTransaction.IsPublic`; `Cancel(reason)` bắt buộc lý do |
| `Permissions.ForTreasurer` | **Sửa** | → `FundRole` (enum) + `RolePermissions.For(role)`; thêm `Ledger.ViewFull`, `Ledger.ViewPublic`, `Member.Manage`; bỏ `Report.View` |
| `FundContext.FundIdAsync()` (tìm theo `OwnerUserId`) | **Sửa** | → `CurrentMember` scoped: đọc `FundMember` một lần / request (FundId, Role, Permissions). Không có thành viên → 403 |
| `PermissionAuthorizationHandler` (luôn trả quyền thủ quỹ) | **Sửa** | Lấy permission từ `CurrentMember`; hỗ trợ "một trong các permission"; `MustChangePassword` → 403 `password.change_required` |
| `AuthService.GetMeAsync` (join theo owner) | **Sửa** | Join `FundMember`; trả `role`, `mustChangePassword` |
| `AuthService.ChangePasswordAsync` | **Sửa nhỏ** | Xóa `MustChangePassword` |
| `TransactionService` | **Sửa** | `isPublic` vào input / DTO / audit; projection công khai (`PublicTransactionDto`) và lọc `IsPublic && !IsCancelled` khi chỉ có `Ledger.ViewPublic`; endpoint lịch sử thay đổi |
| `FundService` | **Sửa** | Overview thêm tổng thu / chi + info; báo cáo tháng bản công khai (dòng "Khoản khác") |
| `AccountAdminService.CreateTreasurerAsync` (CLI) | **Sửa nhỏ** | Tạo thêm `FundMember(Treasurer)` |
| Quản lý thành viên | **Thiếu** | `MemberService` + `MembersController` (list, create, reset-password, disable, enable), sinh mật khẩu tạm bằng `RandomNumberGenerator` |
| Bảng `FundMember`, 2 cột mới | **Thiếu** | Migration bổ sung `AddFundMembers` (DD-13) |
| `Fund.OwnerUserId` + `UQ_Fund_OwnerUserId` | **Giữ** (không còn dùng để phân quyền) | Không xóa cột → migration không phá dữ liệu (DD-10) |
| Auth cookie, antiforgery, rate limit, ProblemDetails, Correlation ID, Serilog, Docker, 2 tài khoản DB, Testcontainers | **Giữ** | Không đổi cấu hình |
| Thừa | – | Không có code thừa cần xóa |

**Kiểm tra mô hình nhiều tài khoản / quỹ:**
- Hiện **chưa hỗ trợ**: `UQ_Fund_OwnerUserId` cùng `FundContext` theo owner nghĩa là chỉ chủ quỹ dùng được.
- Thiết kế mới: `FundMember` với `UQ(UserId)` → nhiều tài khoản / quỹ, mỗi tài khoản một quỹ.

## 4. Migration `AddFundMembers` – tương thích & rủi ro

```text
CREATE TABLE "FundMember" (Id, FundId FK, UserId FK UNIQUE, Role varchar(30) CHECK, CreatedBy FK, CreatedAt, UpdatedAt)
CREATE INDEX "IX_FundMember_FundId_Role"
ALTER TABLE "User" ADD "MustChangePassword" boolean NOT NULL DEFAULT false
ALTER TABLE "FundTransaction" ADD "IsPublic" boolean NOT NULL DEFAULT true
INSERT INTO "FundMember" SELECT uuidv7-like, f."Id", f."OwnerUserId", 'Treasurer', f."OwnerUserId", now(), NULL FROM "Fund" f
TRIGGER NoDelete ON "FundMember"; GRANT DML cho app_user (theo cơ chế default privileges đã có)
```

| Rủi ro | Đánh giá | Xử lý |
|---|---|---|
| Mất dữ liệu | Không: chỉ thêm, không UPDATE / DELETE dữ liệu cũ | – |
| Khóa bảng khi thêm cột | `ADD COLUMN … DEFAULT` hằng số trên PG 16 không ghi lại bảng; quy mô một lớp | – |
| Dữ liệu cũ hiện cho phụ huynh | Tất cả khoản cũ thành công khai (D-09) | Thủ quỹ tắt cờ nếu cần; trước v1.1 chưa có phụ huynh nào xem |
| Id cho bản ghi INSERT | Cần UUID trong SQL | Dùng `gen_random_uuid()` (PG 13+). Khác v7 nhưng không ảnh hưởng tính đúng |
| `Down()` | Xóa bảng / cột → mất dữ liệu v1.1 | Chỉ dùng ở dev; ghi trong DD |
| Test `DatabaseAccountTests` kiểm tra tên migration cuối | Sẽ đổi | Cập nhật kỳ vọng sang `_AddFundMembers` |

## 5. Kế hoạch triển khai backend (sau khi duyệt)

| Bước | Nội dung | Kiểm thử |
|---|---|---|
| B1 | Domain: `FundRole`, `FundMember`, `RolePermissions`, thuộc tính mới, `Cancel` bắt buộc lý do | Unit: ma trận vai trò → permission, lý do rỗng → lỗi |
| B2 | Infrastructure: cấu hình EF, migration `AddFundMembers` (+ SQL INSERT, trigger) | Integration: migration áp lên DB có dữ liệu v1.0 → mỗi quỹ có 1 thành viên Treasurer; DELETE FundMember bị chặn |
| B3 | `CurrentMember` + `PermissionAuthorizationHandler` + `password.change_required` | Integration: 3 vai trò × endpoint (bảng mục 6) |
| B4 | Phạm vi công khai trong `TransactionService` / `FundService`; endpoint lịch sử | Integration: phụ huynh không nhận `note`, khoản ẩn / hủy → 404, tổng khớp |
| B5 | `MemberService` + controller + audit (không ghi mật khẩu) | Integration: tạo, trùng email, đặt lại, khóa → phiên cũ 401, thủ quỹ cuối cùng |
| B6 | `/auth/me`, login, change-password; CLI tạo `FundMember` | Integration + smoke trên container tạm |
| B7 | Sinh lại `docs/api/backend-api.md`, cập nhật progress / checklist | Build 0 warning, toàn bộ test xanh |

## 6. Ma trận test phân quyền (sẽ viết thành integration test)

| Endpoint | Thủ quỹ | GVCN | Phụ huynh | Ẩn danh |
|---|---|---|---|---|
| `GET /fund`, `/fund/reports/yearly`, `/categories` | 200 | 200 | 200 | 401 |
| `GET /transactions`, `/transactions/{id}`, `/fund/reports/monthly` | 200 đầy đủ | 200 đầy đủ | 200 công khai (id ẩn / hủy → 404) | 401 |
| `GET /transactions/{id}/history` | 200 | 200 | 403 | 401 |
| `POST/PUT /transactions`, `/cancel` | 2xx | 403 | 403 | 401 |
| `POST/PUT /categories`, `PUT /fund` | 2xx | 403 | 403 | 401 |
| `/members*` | 2xx | 403 | 403 | 401 |
| Mọi endpoint trên khi `mustChangePassword` | 403 `password.change_required` | ← | ← | – |
| Đối tượng của quỹ khác | 404 | 404 | 404 | 401 |
| Gửi `fundId` / `role` trong query / body | bị bỏ qua | ← | ← | – |

## 7. Giới hạn đã tuân thủ
- Không sửa code backend hoặc frontend.
- Không tạo migration, không chạm DB / Docker.
- Không commit / push.
- Không dùng multi-agent.
- Không xóa file trong repo. Thiết kế cũ trên Canvas đã bị xóa theo yêu cầu trực tiếp của người dùng; nó vẫn còn trong lịch sử phiên bản của artifact.

---

## 8. Cập nhật 2026-11-04 (lượt 2): chốt thiết kế v1.2

**Đã duyệt:** D-10, D-11, D-12, Q-11 (→ D-16), Q-12 (→ D-17), B2 → B7 (→ D-18 → D-23).
**Còn chờ:** B1 (và D-09 đi kèm), X-01, X-02 – xem [open-questions.md](../requirements/open-questions.md) mục 4.

**Bổ sung vào kế hoạch B1 → B7 (mục 5)** – chưa triển khai:

| Bước | Bổ sung |
|---|---|
| B1 (Domain) | `FundMember.ChangeRole(newRole, actor, now)`; `FundTransaction.UpdatedBy`, `CancelledBy`; `Cancel` bắt buộc lý do |
| B2 (Migration `AddFundMembers`) | Thêm `UpdatedBy`, `CancelledBy`, `FundMember.UpdatedBy`; CHECK `CancelledBy` NOT VALID; backfill từ `AuditLog` |
| B4 (Phạm vi công khai) | Cho phép khoản công khai đã hủy (chỉ nhãn + ngày); lọc công khai trước đếm / phân trang; `/categories` bản công khai (D-22); "Khoản khác" không có số lượng |
| B5 (Thành viên) | `PUT /members/{id}/role` trong SERIALIZABLE, chặn self / last treasurer |
| B6 (Auth) | Giữ `auth.account_disabled` (D-23); `GET /fund` trả `cumulative` + `currentMonth` + `managers{fullName, role}` |
| B7 (Kiểm thử) | Theo [test-cases.md](../testing/test-cases.md): AUTHZ, ISO, LEAK, VIEW, TX, MEM, SES, CONC, MIG |

**Canvas v2.1:** 26 artboard. Đã kiểm tra bản hiển thị thật trên trình duyệt; chi tiết trong báo cáo lượt này.
