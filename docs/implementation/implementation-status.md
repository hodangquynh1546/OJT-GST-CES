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
| M1 Auth | ✅ 2026-11-18 – đã merge (105 unit + 29 integration + 50 FE spec + 41 e2e xanh) | Dev A | #32 |
| M2 Quỹ | ✅ 2026-11-18 – BE + FE (F-01, F-02, F-03) + test; nhánh `feature/m2-m5-fund-category-files` | Dev B (Claude Code) | |
| M3 Học sinh | ✅ 2026-11-18 – nhánh `feature/m3-members`, chưa mở PR (144 unit + 54 integration + 57 FE spec + 76 e2e xanh) | Dev A | |
| M4 Thủ quỹ | ✅ 2026-11-18 – nhánh `feature/m4-treasurers`, chưa mở PR (196 unit + 15 integration mới + 75 FE spec + e2e mock/full xanh) | Dev A | |
| M5 Danh mục & chứng từ | ✅ 2026-11-18 – M5-T1/T2/T3: danh mục (C-01), upload/tải chứng từ (AZ-05), `AttachToAsync`, ảnh đại diện API + `<app-avatar>`; nhánh `feature/m2-m5-fund-category-files` | Dev B (Claude Code) | |
| M6a Khoản thu chung & thanh toán | ✅ 2026-11-19 – nhánh `feature/m6a-charges-payments`, chưa mở PR (249 unit + 101 integration + 93 FE spec + e2e mock/full 119 xanh) | Dev A | |
| M6b Khoản thu sự kiện | ⬜ | Dev A | |
| M7 Thu chi | ⬜ | Dev B | |
| M8 Sự kiện | ⬜ | Dev B | |
| M9 Quyết toán | ⬜ | Dev A | |
| M10 Số dư cá nhân | ⬜ | Dev B | |
| M11 Thông báo | ⬜ | Dev A | |
| M12 Audit viewer | ✅ 2026-11-18 – M12-T1/T2/T3: `GET /audit-logs` (+ `/actors`), U-01 theo canvas, checklist audit M1–M5; nhánh `feature/m12-audit-viewer` | Dev B (Claude Code) | |
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
| 2026-11-18 | M4 Thủ quỹ làm xong (nhánh `feature/m4-treasurers`). Quyết định kỹ thuật (**đã duyệt**: (1), (2), (3); còn lại là chi tiết cài đặt): (1) cấp quyền **chặn khi quỹ Archived** (BR-37), thu hồi vẫn được; (2) không phân quyền cho thành viên đã bị ngừng (422 `treasurer.member_not_active`); (3) `grantedAt` lấy từ nhật ký `treasurer.grant` gần nhất (FundMember không có cột riêng, không đổi schema); (4) service kiểm lại `Treasurer.Manage` ngoài controller (phòng thủ nhiều lớp); (5) chọn học sinh bằng ComboBox lọc phía server qua `GET /members?role=Student`, danh sách tự mở khi bấm vào ô. Sau review của Cường: sửa test M3 chập chờn (`getByText` trùng tên với toast, tái hiện 2/15 lần dưới tải) và xóa các ghi đè `--kendo-color-primary*` thừa ở M1 (auth-card, login-page, account-page), M3 (members-page) và `auth-field` vì `styles.scss` đã đặt nút chính đen toàn app | `Features/Treasurers` (BE), `features/treasurers` (FE), TC-M4-01…05 |
| 2026-11-19 | M6a Khoản thu chung & thanh toán làm xong (nhánh `feature/m6a-charges-payments`). Quyết định kỹ thuật (**đề xuất, chờ nhóm xác nhận**): (1) chỉ phạm vi quỹ chung – khoản thu/nghĩa vụ có `EventId` bị từ chối (404) cho tới M6b, để không lách `EventStatusRules.AcceptsPayments`; (2) đóng bằng `PersonalBalance` trả 422 `payment.method_unavailable` tới khi có `BalanceService` (M10); (3) **đóng khoản thu** chỉ chặn nộp mới, khoản đang chờ vẫn xác nhận/từ chối được; **hủy** chỉ khi chưa ai nộp (422 `charge.has_payments`) và hủy mọi nghĩa vụ; (4) **đã duyệt**: thành viên thêm (hoặc thêm lại) vào quỹ SAU khi tạo khoản thu vẫn có nghĩa vụ của các khoản thu chung đang mở (khôi phục nghĩa vụ đã hủy khi ngừng), vì quyết toán sau đó chia theo tỷ lệ số đã đóng; (5) bút toán thu dùng danh mục mặc định "Đóng góp quỹ lớp" (null nếu quỹ chưa có); ngày giao dịch theo giờ Việt Nam; (6) `Notifier` chưa được đăng ký DI nên thêm một dòng `AddScoped<Notifier>()` vào `ServiceRegistration` (file Foundation, đúng phạm vi task); (7) bản thiết kế H-01b còn nút "Ghi nhận tiền mặt" (thủ quỹ ghi thay học sinh) và "Gửi nhắc người chưa đóng" – **chưa làm**: chưa có trong module-plan / FR (FR-22 chỉ cho thủ quỹ tự ghi cho mình); nhắc hạn thuộc M11 | `Features/Charges`, `Features/Payments` (BE), `features/charges` (FE), TC-M6-01…04, TC-M6a-01…10 |
| 2026-11-18 | Làm lại khung trong quỹ theo canvas S-01 (PR foundation riêng, nhánh `feature/shell-canvas`, xếp trên M3): sidebar 248 px có thẻ quỹ (bấm → Quỹ của tôi), nhóm **Quỹ chung · Cá nhân · Quản lý** (học sinh chỉ hai nhóm đầu), nhãn menu theo canvas ("Tổng quan", "Khoản cần đóng"), bỏ mục "Quỹ của tôi" khỏi menu và bỏ ghi chú vai trò cuối sidebar; thanh trên bỏ logo và nút Đăng xuất (đăng xuất ở A-04). `initialsOf` chuyển sang `shared/utils` | `core/navigation/*`, `current-fund.service.ts` (thêm `url`), `styles.scss` (`--cf-nav-width` 248), test `shell.spec.ts`, `nav-items.spec.ts` |
| 2026-11-18 | M3 Học sinh làm xong (nhánh `feature/m3-members`). Quyết định kỹ thuật (đề xuất, chờ nhóm xác nhận): (1) **AZ-07** – chỉ sửa tên / cấp lại mật khẩu cho tài khoản `Provisioned` chưa ở quỹ nào khác (chống chiếm tài khoản), thêm `canManageAccount` vào DTO; (2) ngừng thành viên bị chặn khi còn thanh toán chờ xác nhận (thêm vào OQ-15: còn số dư thì đã chặn); (3) kích hoạt lại luôn về vai trò học sinh; (4) thêm nhiều email: tên tạm = phần trước `@`; (5) endpoint `GET …/removal-preview` để cảnh báo trước khi ngừng; (6) rate limit `members-bulk` 10 lần/phút/người dùng. **Điều hướng (theo yêu cầu 2026-11-18, canvas):** sau đăng nhập luôn vào F-01 Quỹ của tôi (không menu), chọn quỹ mới có menu; F-01/F-02/A-04 ngoài `AppShell` với thanh trên riêng; F-01 tối thiểu (tên quỹ + vai trò từ `/auth/me`), số liệu thẻ và Tạo quỹ thuộc M2. Sửa Foundation: `app.ts` thêm `kendoDialogContainer` (ConfirmDialog chưa từng chạy được trong app), `AuthService.homeUrl`, `app.routes.ts`, `playwright.config.ts` đọc cổng từ `E2E_UI_PORT`/`E2E_API_PORT`. | `Features/Members` (BE), `features/members` + `features/funds/fund-list-page` + `features/account/top-bar` (FE), AZ-07, TC-M3-01…09 |
| 2026-11-18 | Chỉnh M1 theo canvas "ClassFund UI Design": A-01 chia đôi (nền tối + slogan), A-02 có dấu * / checklist mật khẩu / ô đồng ý trách nhiệm, A-03 "Đặt mật khẩu mới" + "Lưu và vào lớp", A-04 **ngoài khung menu quỹ** (thanh trên riêng) kèm đổi mật khẩu ngay trên trang; bỏ nút Hiện/Ẩn mật khẩu và mục "Quỹ của tôi" mà canvas không có | `app.routes.ts` (route `/account` ra khỏi `AppShell`), `styles.scss` (token `--cf-ink*`, `.cf-link-button`), `features/auth`, `features/account` |
| 2026-11-18 | Nhóm duyệt OQ-19 (tổng kết cuối năm chia tiền thừa theo tổng đã đóng cả năm; nghĩa vụ `Unpaid` → `Excluded`, không truy thu; còn thanh toán chờ thì chặn quyết toán) và TD-03 (đổi tên `ListLockedAt` → `CompletedAt`, xóa `SupplementaryChargeId`) | OQ-19, BR-23/36, FR-53, `database-design.md`, `Event`/`Settlement`, migration `DropObsoleteEventSettlementColumns` (chưa áp Neon) |
| 2026-11-18 | M2 + M5: file chưa gắn quá 24h / ảnh đại diện cũ được dọn bằng cách đánh dấu `Attachment.OwnerId = Guid.Empty` (`AttachmentOwners.Discarded`) rồi xóa object – không xóa dòng vì `app_user` không có quyền DELETE. Upload kiểm thêm chữ ký đầu file. Ô chọn file dùng `input type=file` (chưa cài `@progress/kendo-angular-upload`). Danh mục mặc định theo `DefaultCategories` trong code (canvas ghi tên khác). F-03: năm học chỉ xem theo bản thiết kế (API vẫn nhận). | `AttachmentsService`, `AvatarService`, `FileIntake`, `FileSignature`, `api-catalogue.md` |
| 2026-11-18 | M12 Audit viewer: chỉ đọc; cột "Chi tiết" dựng ở backend (`AuditSummary`) từ Before/After/Note theo tên trường chung, không cần biết từng hành động; IP che hai phần cuối; ngày lọc theo giờ VN quy về UTC (Npgsql chỉ nhận offset 0); thêm `GET /audit-logs/actors` cho ô "Người thực hiện". Style chung theo canvas mở rộng cho **Kendo Grid** (`.tbl`) và **Kendo Dialog** (`.dlg`), nhãn phạm vi "Sự kiện" có viền (`.b-ev`) | `Features/AuditLogs` (BE), `features/audit` (FE), `styles.scss`, `scope-badge.ts`, TC-M12-01…05 |
| 2026-11-18 | M2 gộp với khung mới của M1/M3: F-01 dùng `FundListPage` (ngoài khung menu, `app-top-bar`) và thêm số liệu thẻ từ `GET /funds`; F-02 cũng ngoài khung menu; F-03, C-01 trong khung. `kendoDialogContainer` dùng bản đã có trên main (`app.ts`) | `features/funds/fund-list-page.ts`, `fund-new`, `funds.routes.ts` |
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
- Sau M12 (2026-11-18, đã gồm M1–M5): **215 unit** và **85 integration** – **xanh**; checklist audit M1–M5 trong `Modules/AuditLogsApiTests`.
- Sau M2 + M5 (2026-11-18): **119 unit** (thêm `FundsServiceTests`, `CategoriesServiceTests`, `AttachmentsServiceTests`) và **24 integration** (thêm `Modules/FundsApiTests`, `Modules/CategoriesAndFilesApiTests`; `ApiTestHost` đăng nhập bằng endpoint chỉ có trong test `/__test/login/{userId}` vì M1 chưa có) – **xanh**.

### Frontend (`ClassFundUI/`)

- Shell (header, menu theo permission), auth service/guard, interceptor, current fund, design token.
- Shared: `app-state`, `page-header`, `coming-soon`, `scope-badge`, `*appHasPermission`, pipes, `LoadState`, `applyServerErrors`, `ConfirmDialog`, `AppNotifications`, nhãn enum (`shared/models/labels.ts`).
- Route đầy đủ cho mọi màn hình (giữ chỗ `comingSoon`), sinh bởi `scripts/gen-routes.mjs`.
- 36 spec – **xanh**; `ng build` OK.
- Sau M12 (2026-11-18, đã gồm M1–M5): **83 spec** và **86 E2E mock** (thêm `e2e/mock/audit.spec.ts`) – **xanh**.
- Sau M2 + M5 (2026-11-18): **48 spec** và **8 E2E mock** (thêm `e2e/mock/funds-categories.spec.ts`) – **xanh**; `ng build` không cảnh báo. Thành phần dùng chung cho module khác: `features/attachments/` (`<app-file-upload>`, `<app-avatar>`, `AttachmentsApi`), `CategoriesApi.list(fundId, { type, activeOnly })`.

### Chưa có (cố ý)

- Không controller / service nghiệp vụ nào. Màn hình thật (đã có bản thiết kế UI, xem `ui-design/design-system.md` mục 6).

## 3. Người chạy migration trên Neon

| Migration | Người áp | Ngày |
|---|---|---|
| InitialSchema | hodangquynh1546 | 2026-11-17 |

## 4. Việc tồn đọng

| Mã | Mô tả | Module |
|---|---|---|
| ~~TD-01~~ | ✅ Dọn file chưa gắn owner > 24h – làm trong M5-T1 (mỗi lần upload dọn tối đa 20 file của quỹ) | M5 |
| TD-03 | Migration `DropObsoleteEventSettlementColumns` (đổi `Event.ListLockedAt` → `CompletedAt`, xóa `Settlement.SupplementaryChargeId`) – đã duyệt và tạo 2026-11-18. Kiểm tra chỉ đọc ngày 2026-11-18 thấy Neon **đã có** cột `Event.CompletedAt` nhưng chưa ghi người áp: người áp ghi tên ở mục 3 | M8-T0 |
| ~~TD-04~~ | ✅ API ảnh đại diện (`PUT /auth/me/avatar`, `GET /users/{id}/avatar`) đã làm trong M5; việc gắn vào giao diện chuyển sang TD-11 | M5 |
| ~~TD-05~~ | ✅ Nút đen / kiểu canvas đã áp **toàn app** qua `styles.scss` (2026-11-18, `design-system.md` mục 6); phần ảnh đại diện ở A-04 chuyển sang TD-11 | M1 |
| TD-06 | `e2e/full` tự đăng ký tài khoản nên ghi dữ liệu: chỉ chạy trên PostgreSQL tạm (xem `test-strategy.md`), không chạy với Neon dùng chung | – |
| ~~TD-07~~ | ✅ 2026-11-18 – khung `AppShell` đã làm lại theo canvas S-01 (nhánh `feature/shell-canvas`): thẻ quỹ ở đầu sidebar, ba nhóm menu, thanh trên có breadcrumb + chuông + nhãn vai trò + ảnh đại diện. Chưa làm: chấm đỏ / số đếm trên chuông và menu (cần dữ liệu thông báo, M11) | Foundation |
| ~~TD-08~~ | ✅ F-01 đã có số liệu thẻ quỹ (M2 mở rộng `fund-list-page.ts`), F-02 Tạo quỹ đã làm | M2 |
| TD-09 | Kendo Grid cần license: chưa kích hoạt thì bảng hiện watermark "Invalid license" và toast (`npm run license` – không commit file license) | – |
| TD-10 | Mời thành viên vào sự kiện `Open` khi thêm vào quỹ (M3-T1) chưa làm vì M8 chưa có: cần gọi `EventsService.InviteMember…` khi có | M8 |
| TD-11 | Gắn `<app-avatar [editable]="true">` (M5, `features/attachments/avatar`) vào A-04 và avatar trên thanh trên / header (file Foundation); `AuthController` không khai báo lại `/auth/me/avatar`, `/users/{id}/avatar` | M1 |
| TD-12 | F-03 trên canvas có "Tài khoản nhận chuyển khoản" nhưng `Fund` chưa có cột – cần nhóm quyết định + migration nếu làm | M2 |
| TD-13 | Thẻ quỹ của học sinh ở F-01: canvas có "Bạn cần đóng", "Số dư cá nhân" – bổ sung vào `FundSummaryDto` khi có M6/M10 | M6, M10 |
| TD-14 | U-01: nhãn tiếng Việt cho mã audit, tên trường và giá trị enum mới trong Before/After của module mới phải thêm vào `features/audit/audit-format.ts` (`AUDIT_ACTION_LABEL`, `AUDIT_FIELD_LABEL`, `AUDIT_VALUE_LABEL`; chưa có nhãn thì hiện nguyên mã); bổ sung API ghi mới vào checklist `AuditLogsApiTests.Every_write_api_of_merged_modules_records_audit_checklist` | M6–M14 |
| TD-15 | U-01 theo canvas chỉ có bộ lọc ngày / người / phạm vi / tự ghi nhận; API đã hỗ trợ thêm `eventId` và `action` (FR-47) nhưng chưa có ô chọn – thêm khi M8 có danh sách sự kiện (nếu nhóm muốn, cần cập nhật canvas) | M8, M12 |
| TD-02 | Tiến trình `ClassFund.Api.exe` cũ trên máy dev giữ khóa thư mục khi xóa code cũ – tắt trước khi build | – |

## 5. Sự cố

_Chưa có._
