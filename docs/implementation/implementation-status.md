# Trạng thái triển khai

> Cập nhật sau mỗi PR merge. Kế hoạch: `module-plan.md`.

## 1. Tổng quan (2026-11-16)

Nhánh `feature/rebuild-foundation`: xây lại từ đầu theo bản yêu cầu mới (quỹ chung / quỹ sự kiện độc lập, thủ quỹ chính & thủ quỹ, số dư cá nhân, quyết toán, thông báo, audit, Neon). Code và tài liệu cũ đã xóa; giữ `training-doc.md` và cấu trúc thư mục.

| Module | Trạng thái | Người làm | PR |
|---|---|---|---|
| M0 Foundation code | ✅ Xong, đã duyệt | Claude Code | – |
| M0-01 Duyệt tài liệu / chốt OQ | ✅ 2026-11-17 | Cả nhóm | |
| M0-02 Migration InitialSchema | ✅ 2026-11-17 – 9 test migration xanh trên PostgreSQL 16 (Testcontainers) | Claude Code | |
| M5-T0 Adapter Neon Object Storage | ✅ Code + 7 unit test (S3 client giả); chưa thử tay trên bucket thật | Claude Code | |
| M0-03 Neon roles + áp migration + seed | ✅ 2026-11-17 – đã áp `InitialSchema` lên Neon và chạy `--seed` thành công | hodangquynh1546 | |
| M1 Auth | ✅ 2026-11-18 – nhánh `feature/m1-auth`, chưa mở PR/chưa merge (105 unit + 29 integration + 50 FE spec + 39 e2e xanh) | Dev A | |
| M2 Quỹ | ⬜ | Dev B | |
| M3 Học sinh | ⬜ | Dev A | |
| M4 Thủ quỹ | ⬜ | Dev A | |
| M5 Danh mục & chứng từ | ⬜ | Dev B | |
| M6a Khoản thu chung & thanh toán | ⬜ | Dev A | |
| M6b Khoản thu sự kiện | ⬜ | Dev A | |
| M7 Thu chi | ⬜ | Dev B | |
| M8 Sự kiện | ⬜ | Dev B | |
| M9 Quyết toán | ⬜ | Dev A | |
| M10 Số dư cá nhân | ⬜ | Dev B | |
| M11 Thông báo | ⬜ | Dev A | |
| M12 Audit viewer | ⬜ | Dev B | |
| M13 Báo cáo | ⬜ | Dev B | |
| M14 Quyết toán cuối năm | ⬜ | Dev A | |

### Nhật ký quyết định

| Ngày | Quyết định | Thay đổi kèm theo |
|---|---|---|
| 2026-11-17 | OQ-10: số dư cá nhân đóng được cả khoản thu chung | FR-25, BR-15, M6b-T2 |
| 2026-11-17 | OQ-11: rút một lần toàn bộ số dư, thủ quỹ liên hệ trả rồi xác nhận | `WithdrawalRule`, unique `UX_WithdrawalRequest_OnePendingPerUser`, FR-41, BR-26, M10 |
| 2026-11-17 | OQ-13: quyết toán cuối năm quỹ chung như sự kiện, một lần, chia cả lớp, quỹ → Archived | `EventSettlement` → `Settlement` (thêm FundId, EventId nullable, 2 unique partial), `YearEndRule`, FR-53/54, BR-34…37, module M14 |
| 2026-11-17 | OQ-16: lưu file thực tế trên Neon Object Storage (hóa đơn, bill, ảnh đại diện, tài liệu) | `IEvidenceStorage` → `IFileStorage` + `StorageKeys` + `StorageOptions`; `FileRule` giới hạn theo loại; `AttachmentKind` thêm Avatar/Document; `Attachment.FundId` nullable; `User.AvatarAttachmentId`; task M5-T0 |
| 2026-11-17 | OQ-17: thêm package `AWSSDK.S3` (ngoài training-doc) để gọi Neon Object Storage | Cài trong M5-T0, chỉ ở `ClassFund.Infrastructure` |
| 2026-11-17 | M0-03 xong: áp `InitialSchema` + chạy `--seed` trên Neon; adapter Neon Object Storage chưa ghi nhận thử trên bucket thật | Bảng module, mục 3 |
| 2026-11-18 | OQ-20: thêm nhiều email một lần khi thêm thành viên; làm rõ BR-37: quỹ `Archived` vẫn cho học sinh gửi yêu cầu rút và thủ quỹ xác nhận; đổi cảnh báo/banner "chỉ xem" của M14-T3 | FR-09, BR-37, `api-catalogue.md` (`POST /members/bulk`), M3-T1/T2/T3, M14-T2/T3/T4, TC-M3-02/03, TC-M14-05 (chỉ tài liệu, chưa có code) |
| 2026-11-18 | M1 Auth làm xong (nhánh `feature/m1-auth`). Quyết định kỹ thuật: (1) đổi mật khẩu sai mật khẩu hiện tại trả **422** `auth.current_password_incorrect` (401 sẽ bị FE coi là hết phiên); (2) mật khẩu mới phải khác mật khẩu hiện tại (`auth.password_unchanged`); (3) `PUT /auth/me` bị chặn khi còn mật khẩu tạm (theo api-catalogue); (4) `auth.account_disabled` chỉ báo sau khi mật khẩu đúng; (5) sửa interceptor FE: 401 của `PUT /auth/me` là hết phiên (trước đây bị coi như `GET /auth/me`); (6) thêm nút Đăng xuất ở `app-header`, `AuthService.updateProfile` | `Features/Auth` (BE), `features/auth` + `features/account` (FE), `auth.service.ts`, `api-error.interceptor.ts`, `app-header.ts`, test case TC-M1-01…11 |
| 2026-11-18 | Chỉnh M1 theo canvas "ClassFund UI Design": A-01 chia đôi (nền tối + slogan), A-02 có dấu * / checklist mật khẩu / ô đồng ý trách nhiệm, A-03 "Đặt mật khẩu mới" + "Lưu và vào lớp", A-04 **ngoài khung menu quỹ** (thanh trên riêng) kèm đổi mật khẩu ngay trên trang; bỏ nút Hiện/Ẩn mật khẩu và mục "Quỹ của tôi" mà canvas không có | `app.routes.ts` (route `/account` ra khỏi `AppShell`), `styles.scss` (token `--cf-ink*`, `.cf-link-button`), `features/auth`, `features/account` |
| 2026-11-18 | Nhóm duyệt OQ-19 (tổng kết cuối năm chia tiền thừa theo tổng đã đóng cả năm; nghĩa vụ `Unpaid` → `Excluded`, không truy thu; còn thanh toán chờ thì chặn quyết toán) và TD-03 (đổi tên `ListLockedAt` → `CompletedAt`, xóa `SupplementaryChargeId`) | OQ-19, BR-23/36, FR-53, `database-design.md`, `Event`/`Settlement`, migration `DropObsoleteEventSettlementColumns` (chưa áp Neon) |
| 2026-11-18 | Duyệt thiết kế UI (canvas "ClassFund UI Design") → OQ-18: bỏ chốt danh sách, kết thúc loại người chưa đóng khoản nào, chi ≤ thu, quyết toán chỉ chia tiền thừa theo tỷ lệ đã đóng, form thu ≠ form chi, Ghi giao dịch ở Thu chi chung chỉ cho quỹ chung. OQ-19 chờ duyệt. | FR-26/27/32–38/43/50/53, BR-16…23/38/39; Domain: bỏ `EventStatus.ListLocked`, `ChargeKind.Supplementary`, `SettlementOutcome.Deficit`; `NotificationType` 5 → `EventParticipantExcluded`, thêm `EventUnpaidReminder`; `AcceptsNewMoney` → `AcceptsNewChargesAndExpenses` + `AcceptsPayments`; thêm `SpendingRule` (BR-38); FE `labels.ts`. Cột `Event.ListLockedAt`, `Settlement.SupplementaryChargeId` giữ lại, chờ migration (M8-T0) |

## 2. Foundation đã có

### Backend (`ClassFundAPI/`)

- Solution 4 project + 2 test project, `Directory.Build.props` (net10.0, Nullable, TreatWarningsAsErrors).
- Domain: 18 entity (`Entities/`), enum (`Enums/Enums.cs`), `Permissions` + `RolePermissions`, rule dùng chung (`Rules/Rules.cs`: `MoneyRule`, `EvidenceRule`, `FileRule`, `WithdrawalRule`, `YearEndRule`, `EventStatusRules`, `SpendingRule`, `PaymentConfirmationPolicy`, `PasswordPolicy`, `EmailAddress`, `VietnamTime`, `DefaultCategories`), `BusinessRuleException`.
- Application/Common: `IAppDb`, exception chuẩn, `PagedResult`, `AuditTrail` + `AuditEntry`, `Notifier`, `IFileStorage` + `StorageKeys`, `AccessService`, `ICurrentUser`, `IPasswordHasher`, `ISessionStore`, `Credentials`.
- Infrastructure: `AppDbContext` + cấu hình đủ 18 bảng (index, unique, CHECK, xmin), migration `InitialSchema` (+ trigger append-only), `AppDb`, `AspNetPasswordHasher` (PBKDF2 + pepper), `IdentityStore`, `NeonObjectStorage` (`AWSSDK.S3`) + `LocalFileStorage` + `StorageOptions`, `DatabaseSeeder`, `AppDbContextFactory` (migrator).
- Api: pipeline, cookie session, antiforgery, `RequirePermission`, filter mật khẩu tạm / validation / antiforgery, ProblemDetails, rate limit, Swagger, health, `--seed`.
- `database/neon-roles.sql`, `.env.example`.
- Test: 83 unit (kiến trúc, rule, permission, file storage, Neon adapter, AccessService, Audit, Notifier) – **xanh** (2026-11-18); 2 integration pipeline – **xanh**; 9 integration `InitialSchemaTests` (migration, trigger, CHECK, unique, xmin) – **xanh** (cần Docker Desktop đang chạy).

### Frontend (`ClassFundUI/`)

- Shell (header, menu theo permission), auth service/guard, interceptor, current fund, design token.
- Shared: `app-state`, `page-header`, `coming-soon`, `scope-badge`, `*appHasPermission`, pipes, `LoadState`, `applyServerErrors`, `ConfirmDialog`, `AppNotifications`, nhãn enum (`shared/models/labels.ts`).
- Route đầy đủ cho mọi màn hình (giữ chỗ `comingSoon`), sinh bởi `scripts/gen-routes.mjs`.
- 36 spec – **xanh**; `ng build` OK.

### Chưa có (cố ý)

- Không controller / service nghiệp vụ nào. Màn hình thật (đã có bản thiết kế UI, xem `ui-design/design-system.md` mục 6).

## 3. Người chạy migration trên Neon

| Migration | Người áp | Ngày |
|---|---|---|
| InitialSchema | hodangquynh1546 | 2026-11-17 |

## 4. Việc tồn đọng

| Mã | Mô tả | Module |
|---|---|---|
| TD-01 | Dọn file chưa gắn owner > 24h (đã đưa vào M5-T1) | M5 |
| TD-03 | Migration `DropObsoleteEventSettlementColumns` (đổi `Event.ListLockedAt` → `CompletedAt`, xóa `Settlement.SupplementaryChargeId`) – đã duyệt và tạo 2026-11-18, **chưa áp Neon**: người áp tạo branch Neon dự phòng, báo nhóm trước/sau, rồi ghi tên ở mục 3 | M8-T0 |
| TD-04 | Ảnh đại diện (FR-04, `PUT /auth/me/avatar`, `GET /users/{id}/avatar`) chưa làm: cần module M5 (`Attachment`) | M5 |
| TD-05 | Đã đối chiếu A-01…A-04 với canvas "ClassFund UI Design" (2026-11-18). Chưa làm: ảnh đại diện (nút "Đổi ảnh đại diện" đang tắt, chờ M5 – TD-04); canvas dùng nút đen kiểu shadcn nên M1 đặt token `--cf-ink*` và chỉ áp cho màn A-01…A-04 (các màn khác vẫn nút xanh theo `design-system.md`) – nhóm quyết định có đổi toàn hệ thống không | M1 |
| TD-06 | `e2e/full` tự đăng ký tài khoản nên ghi dữ liệu: chỉ chạy trên PostgreSQL tạm (xem `test-strategy.md`), không chạy với Neon dùng chung | – |
| TD-02 | Tiến trình `ClassFund.Api.exe` cũ trên máy dev giữ khóa thư mục khi xóa code cũ – tắt trước khi build | – |

## 5. Sự cố

_Chưa có._
