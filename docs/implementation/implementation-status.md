# Trạng thái triển khai

> Cập nhật: 2026-11-10 · Tài liệu này là nguồn duy nhất về **trạng thái triển khai**. Các tài liệu khác ghi trạng thái theo đây. Chuẩn tài liệu: [docs/README.md](../README.md).

## 1. Hiện trạng

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| **Foundation Backend** | **Implemented** (nhánh `feature/foundation`) | `ClassFundAPI`: 4 tầng, pipeline (CorrelationId → xác thực → CurrentUser → `[RequirePermission]`), ProblemDetails + mã lỗi, validation, antiforgery, mật khẩu tạm, phân trang (`PageRequest`), `ApiControllerBase` / `ApiRoutes`, tự đăng ký service module (`FeatureRegistration`), Swagger hợp đồng chặt, `AccessService`, `AuditTrail`, Domain đầy đủ. Quy ước: [api-conventions.md](../api/api-conventions.md) |
| Backend nghiệp vụ (M1 → M7) | **M1 Implemented**; M2 → M7 Not Implemented – đưa về codebase 2026-11-09 | Mã cũ (đã code xong, 302 unit / 92 integration) bỏ khỏi nhánh, **tham khảo** tại commit `6b6645d` của `main`. Làm lại theo module: [module-plan.md](module-plan.md) |
| **Foundation Frontend** | **Implemented** | `ClassFundUI`: shell (header, nav 232 px), guard, interceptor, `ApiError`, `CurrentFund`, `ConfirmDialog`, `AppNotifications`, `app-state`, `page-header`, `applyServerErrors`, `LoadState`, tiện ích ngày / query / mã lỗi, token Design System → Kendo, route giữ chỗ cho mọi module. Màn hình nghiệp vụ là trang giữ chỗ |
| Database / migration | **Implemented** (schema, thuộc Foundation) | Migration `InitialClassFund` + `AddDatabaseProtections`: 8 bảng, ràng buộc, index, trigger. Dev: `docker-compose.yml` (PostgreSQL 16) tạo `migrator_user` / `app_user` |
| Test tự động | **Foundation** | Backend: Unit 102 · Integration 19 (`FoundationTests`, `ApiContractTests` – snapshot hợp đồng theo module). Frontend: Unit 42 · E2E mock 9 · E2E full 3 (FE + BE + DB thật). Test nghiệp vụ `test-cases.md` mục A: Not Implemented (viết lại theo module) |
| Tài liệu nghiệp vụ, DB, API, phân quyền, test case | Hoàn thiện – Source of Truth | `docs/requirements`, `docs/business-analysis`, `docs/database`, `docs/api`, `docs/testing` |
| UI/UX | Hoàn thiện – Source of Truth **v4.0 Desktop Web** (prototype) | Canvas https://claude.ai/artifact/D5JVqKkBFqQhmcp5spfw6Y (version `1794024869-668f`), `docs/ui-design` (kèm `design-system.md`) |

Giai đoạn hiện tại: Foundation xong → triển khai **từng module trọn luồng FE → BE → DB → Test** theo [module-plan.md](module-plan.md) và [foundation-guide.md](../architecture/foundation-guide.md). Phân công 2 dev: cột Module ở mục 1a và [module-plan.md](module-plan.md) mục 1. Không có deploy / CI/CD ở giai đoạn này.

## 1a. Trạng thái theo module

| Module | Màn hình | Yêu cầu | Trạng thái | Nhánh / PR |
|---|---|---|---|---|
| M0 Foundation | Khung ứng dụng, X-01 | NFR-01, NFR-04, NFR-07, NFR-10 | **Implemented** – chờ review / merge | `feature/foundation` |
| M1 Auth & Tài khoản (Quỳnh, đợt 1) | A-01 → A-04 | FR-20, FR-13, FR-21 | **Implemented** – chờ review / merge | `feature/m1-auth` |
| M2 Quỹ (Dev B, đợt 1) | G-01, G-04 | FR-22, FR-23, FR-11 | **Implemented** – đã merge (PR #19) | `main` |
| M3 Học sinh & Phụ huynh (Quỳnh, đợt 2) | G-02 | FR-24 → FR-26 | **Implemented** – chờ review / merge | `feature/m3-students` (hợp đồng + seed: PR #20 đã merge) |
| M4 Phân công thủ quỹ (Cường, đợt 2) | G-03 | FR-27 | **Implemented** – đã merge (PR #21; e2e full PR #23) | `main` |
| M5 Danh mục & Thành viên (Dev B, đợt 2) | T-02, T-03 | FR-10, FR-12 | Not Implemented | – |
| M6 Thu chi (Cường, đợt 3) | S-02, T-01 | FR-02 → FR-07 | **Implemented** – chờ review / merge; e2e full `e2e/full/transactions.spec.ts` tự bỏ qua tới khi có `GET /categories` (M5) | `feature/m6-transactions` |
| M7a Tổng quan (Dev A, đợt 4) | S-01 | FR-01 | Not Implemented | – |
| M7b Báo cáo (Dev B, đợt 4) | S-03 | FR-08, FR-09 | Not Implemented | – |
| M8 E2E toàn luồng (cả hai, đợt 5) | Toàn bộ | UF-01 → UF-13 | Not Implemented | – |
| CLI vận hành | – | TD-88, KI-03 | Not Implemented | – |
| ~~Khóa học sinh, rời lớp, chuyển quỹ~~ | – | FR-28 | Loại bỏ (D-41) – không có chức năng này | – |

Lịch sử triển khai backend trước khi đưa về codebase: [backend-progress.md](backend-progress.md) (*historical*).

## 2. Quy ước trạng thái trong tài liệu

| Trạng thái | Nghĩa |
|---|---|
| `Not Implemented` | Đã đặc tả / đã quyết định, chưa có mã nguồn (áp dụng cho các yêu cầu, quyết định, endpoint, test case chưa có mã nguồn) |
| `Partially Implemented` / `Implemented` | Đã có mã nguồn mới, được kiểm thử tự động |
| `Pending Decision` | Chờ PO quyết định (PD-01 → PD-06, UQ-23) |
| *Historical* | Kết quả / mô tả của mã nguồn đã xóa – chỉ để tham khảo, không phản ánh hiện trạng |

Đặc tả (nghiệp vụ, database, API, roles & permissions, validation, test case) **giữ nguyên** và là căn cứ cho lần triển khai tiếp theo.

## 3a. Kết quả kiểm chứng

| Ngày | Phạm vi | Kết quả | Môi trường |
|---|---|---|---|
| 2026-11-12 | **M6 Thu chi** (nhánh `feature/m6-transactions`, rebase trên `main` có M1 → M4 và hợp đồng M5 – PR #24): `GET/POST /funds/{id}/transactions`, `GET/PUT …/{tid}`, `GET …/{tid}/history`, `POST …/{tid}/cancel`, S-02, T-01 | Build 0 warning / 0 error · Backend **Unit 185 / 185** (M6: 19) · **Integration 96 / 96** (M6: 15; snapshot `Transactions.json`; chống trùng 5 request đồng thời; hủy đồng thời – bỏ khóa dòng thì test fail) · Frontend **Unit 109 / 109** (M6: 18) · **E2E mock 67 / 67** (M6: 7, danh mục giả lập bằng `mock-categories.ts` của M5) · **E2E full 11 / 11 + 1 bỏ qua** (M6 chờ `GET /categories` của M5; đã chạy thử luồng M6 với danh mục lấy từ DB: đạt) · `ng build` đạt | .NET SDK 10, PostgreSQL 16 (Testcontainers + docker compose), Chrome |
| 2026-11-11 | **M4 – E2E full** (nhánh `feature/m4-treasurers`, rebase trên `main` có M3 – PR #22): `e2e/full/treasurer-assignment.spec.ts` seed học sinh bằng helper M3 (`createStudent`, `grantAccount`, `createActiveStudent`) | **E2E full 11 / 11** hai lần liên tiếp (M4: 2 – quỹ chưa phân công; phân công → thủ quỹ thấy menu Danh mục → phân công người mới, thu hồi người cũ → người cũ mất menu, vào `/categories` → 403) · **E2E mock 58 / 58** | PostgreSQL 16 (docker compose), Chrome |
| 2026-11-11 | **M3 Học sinh & Phụ huynh** (nhánh `feature/m3-students`): `GET/POST /funds/{id}/students`, `GET/PUT …/{sid}`, `POST …/account`, `…/reset-password`, `…/parent`, `…/parent/unlink`, G-02 | Build 0 warning / 0 error · Sau khi gộp `main` có M4 (PR #21): Backend **Unit 166 / 166** (M3: 12) · **Integration 80 / 80** (M3: 15; snapshot `Students.json`) · Frontend **Unit 87 / 87** · **E2E mock 58 / 58** (M3: 12) · **E2E full 10 / 10** (M3: 2); chạy `E2E_FULL=1` toàn bộ thỉnh thoảng có 1–2 test mock của M1 / Shell rớt do chuyển hướng `/login` (chạy riêng chế độ mock: 58 / 58 hai lần liên tiếp) · `ng build` đạt | .NET SDK 10, PostgreSQL 16 (Testcontainers + docker compose), Chrome |
| 2026-11-11 | **M4 Phân công thủ quỹ** (nhánh `feature/m4-treasurers`, trên `main` có M1, M2, hợp đồng M3): `GET/POST /funds/{id}/treasurers`, `POST …/{aid}/revoke`, G-03 | Build 0 warning / 0 error · Backend **Unit 154 / 154** (M4: 17) · **Integration 66 / 66** (M4: 14; snapshot `Treasurers.json`; thu hồi đồng thời lặp 5 vòng – bỏ khóa dòng thì test fail) · Frontend **Unit 76 / 76** (M4: 10) · **E2E mock 46 / 46** (M4: 8, dùng `mockStudents`) · **E2E full 8 / 8** (M4: quỹ mới → G-03 chưa phân công) · `ng build` đạt | .NET SDK 10, PostgreSQL 16 (Testcontainers + docker compose), Chrome |
| 2026-11-10 | **M2 Quỹ** (nhánh `feature/m2-funds`, rebase trên `main` có M1): `GET/POST /funds`, `PUT /funds/{fundId}`, G-01, G-04 | Build 0 warning / 0 error · Backend **Unit 132 / 132** · **Integration 51 / 51** (M2: 16; snapshot `Funds.json`) · Frontend **Unit 66 / 66** · **E2E mock 38 / 38** (M2: 9) · **E2E full 7 / 7** (M2: đăng ký → tạo 2 quỹ → đổi tên) · `ng build` đạt | .NET SDK 10, PostgreSQL 16 (Testcontainers + docker compose), Chrome |
| 2026-11-10 | **M1 Auth & Tài khoản** (nhánh `feature/m1-auth`) | Build 0 warning / 0 error · Backend **Unit 116 / 116** · **Integration 35 / 35** (snapshot `Auth.json`) · Frontend **Unit 54 / 54** · **E2E mock 29 / 29** · **E2E full 6 / 6** · `ng build` đạt | .NET SDK 10, PostgreSQL 16 (Testcontainers + docker compose), Chrome |
| 2026-11-09 | **Foundation** sau khi đưa backend về codebase (nhánh `feature/foundation`) | Build 0 warning / 0 error · Backend **Unit 102 / 102** · **Integration 19 / 19** · Frontend **Unit 42 / 42** · **E2E mock 9 / 9** · **E2E full 3 / 3** (FE + BE + PostgreSQL docker) · `ng build` đạt | .NET SDK 10, PostgreSQL 16 (Testcontainers + docker compose), Chrome |
| 2026-11-05 | Backend Foundation + Domain + AccessService | Build 0 warning / 0 error · Unit 50 / 50 | .NET SDK 10.0.403 |
| 2026-11-05 | Frontend Foundation | Unit 29 / 29 · E2E 9 / 9 (`/auth/me` giả lập) | Chrome |
| 2026-11-05 | Migration trên DB thật | Áp, rollback về 0, áp lại đều đạt · 17 / 17 kịch bản. Bộ kịch bản này chạy tạm, không nằm trong repo | PostgreSQL 16.15 (container tạm, đã xóa) |
| 2026-11-05 | Auth & User Session (PR #5) | Build 0 / 0 · **Unit 89 / 89** · **Integration 18 / 18** · chạy tay API (`app_user`) + Swagger: register / login / me / logout đúng | .NET SDK 10.0.403, PostgreSQL 16 (Testcontainers + docker compose) |
| 2026-11-06 | Backend Auth + Categories + Transactions (PR #7) | Build 0 warning / 0 error · **Unit 206 / 206** · **Integration 44 / 44** · Swagger UI 35 / 35 bước với `app_user` | .NET SDK 10.0.403, PostgreSQL 16, Chrome |
| 2026-11-06 | Fund Lifecycle trên `main` có Auth + Danh mục + Thu chi (sau merge) | Build 0 / 0 · Unit 216 / 216 · Integration 52 / 52 | .NET SDK 10.0.403, PostgreSQL 16 (Testcontainers) |
| 2026-11-06 | Học sinh & phụ huynh (trên `main` có PR #5 → #8) | Build 0 / 0 · **Unit 242 / 242** · **Integration 65 / 65** | .NET SDK 10.0.403, PostgreSQL 16 (Testcontainers) |
| 2026-11-06 | Tổng quan, Báo cáo, Thành viên (trên `main` có PR #5 → #9) | Build 0 / 0 · **Unit 289 / 289** · **Integration 82 / 82** · `swagger.json` có đủ 4 endpoint mới | .NET SDK 10.0.403, PostgreSQL 16 (Testcontainers) |
| 2026-11-06 | Phân công thủ quỹ (trên `main` có PR #5 → #9); kiểm tra đột biến: bỏ khóa dòng quỹ → cả 3 test đồng thời thất bại (3 / 3 lần chạy) | Build 0 / 0 · Unit 255 / 255 · Integration 75 / 75 | .NET SDK 10.0.403, PostgreSQL 16 (Testcontainers) |
| 2026-11-06 | Phân công thủ quỹ sau merge `main` (PR #5 → #10: thêm Tổng quan, Báo cáo, Thành viên) | Build 0 / 0 · **Unit 302 / 302** · **Integration 92 / 92** | .NET SDK 10.0.403, PostgreSQL 16 (Testcontainers) |

## 3. Historical Test Results (mã nguồn đã xóa)

| Ngày | Phạm vi | Kết quả | Môi trường |
|---|---|---|---|
| 2026-11-04 | R1 (một thủ quỹ / quỹ) | Unit 45 / 45 · Integration 53 / 53 | .NET 10, PostgreSQL 16 (Testcontainers) |
| 2026-11-05 | R3 (Giáo viên · Học sinh · Phụ huynh, D-24 → D-33) | Build 0 warning / 0 error · **Unit 57 / 57** · **Integration 67 / 67** | .NET 10, PostgreSQL 16 (Testcontainers) |

Danh sách test case R3 và tên test tương ứng: [test-cases.md](../testing/test-cases.md) mục A (cột "Test (lịch sử)").

## 4. Lịch sử
| Ngày | Sự kiện |
|---|---|
| 2026-11-04 | R1 triển khai (một thủ quỹ / quỹ) |
| 2026-11-05 | R3 triển khai và kiểm thử (57 unit / 67 integration) |
| 2026-11-05 | PO xóa toàn bộ mã nguồn Backend / Frontend; mọi hạng mục chuyển về Not Implemented; Docs + Canvas là Source of Truth |
| 2026-11-05 | Dựng Codebase Foundation Backend / Frontend theo `training-doc.md` (nhánh `feature/codebase`) |
| 2026-11-05 | Thêm Domain Entities, `AppDbContext`, `AccessService`, migration `InitialClassFund` + `AddDatabaseProtections` |
| 2026-11-05 | Feature Auth & User Session; `IdentityStore` thay `NoIdentityStore`; project `ClassFund.IntegrationTests`; `docker-compose.yml` cho DB dev (PR #5) |
| 2026-11-06 | Thêm feature Backend Auth, Categories, Transactions và project `ClassFund.IntegrationTests` (Danh mục – PR #6, Thu chi – PR #7, Dev B) |
| 2026-11-06 | Feature Fund Lifecycle (PR #8) |
| 2026-11-06 | Feature Học sinh & phụ huynh (PR #9) |
| 2026-11-06 | Feature Phân công thủ quỹ & khóa dòng chống thu hồi thủ quỹ cuối cùng (nhánh `feature/devA/treasurers`) |
| 2026-11-06 | Đổi tên dự án SchoolFund → **ClassFund**: thư mục `backend/SchoolFundAPI` → `ClassFundAPI/`, `frontend/SchoolFundUI` → `ClassFundUI/` (theo `training-doc.md`); project / namespace `ClassFund.*`; cookie, CSS, DB dev đổi tên (TD-112, KI-21) |
| 2026-11-07 | UI/UX **v4.0 Desktop Web**: bỏ Mobile App / Mobile Responsive khỏi Canvas (16 artboard, trang Mobile, mọi `@media`), `docs/ui-design` và Frontend (`app-nav.ts`, `app-shell.ts`, `app-header.ts`, `nav-items.ts`, token `--cf-tabbar-height` / `--cf-nav-width-compact`, e2e mobile); thêm Design System (Canvas DS-01 / DS-02, `docs/ui-design/design-system.md`); màn hình G-01, G-04, S-01 khớp DTO backend |
| 2026-11-09 | **Đưa backend về codebase Foundation** (nhánh `feature/foundation`): bỏ code + test của Auth, Funds, Students, Treasurers, Categories, Transactions, Overview, Reports (tham khảo tại `6b6645d`); giữ Domain, schema, `AccessService`, pipeline. Hoàn thiện Foundation BE / FE, thêm `api-conventions.md`, `foundation-guide.md`, `module-plan.md` (M0 → M8) |
| 2026-11-09 | Bỏ chức năng khóa / mở khóa học sinh, rời lớp, chuyển quỹ (FR-28, D-41; đóng PD-03 → PD-05) |
| 2026-11-10 | Foundation UI khớp Canvas v4.0 (nhánh `feature/foundation-ui-polish`): AppHeader (logo, tên quỹ + Đổi quỹ, nhãn vai trò, dòng vai trò dưới tên, ▾); header / menu ở trang ngoài quỹ (Tài khoản, 403, 404) hiện quỹ mở gần nhất (`CurrentFund.context`); AppNav 44 px; control Kendo medium 40 px / small 32 px, nhãn 13 px đậm, gợi ý không nghiêng, nút phụ nền trắng; `.cf-alert` padding 10 / 12 px. E2E mock 14 / 14 |
| 2026-11-10 | Sửa Foundation UI (nhánh `fix/foundation-kendo-sizes`): Kendo 23 không gắn `.k-input-md` / `.k-button-md` / `.k-button-solid` / `.k-button-base` với giá trị mặc định nên style kích thước chưa áp vào component thật; chọn lại theo `.k-input`, `.k-picker`, `.k-button`. Thêm `shared/styles/kendo-sizes.spec.ts` dựng component Kendo thật. Frontend unit 45 / 45 · E2E mock 14 / 14 |
| 2026-11-10 | **M1 Auth & Tài khoản**: `POST /auth/register-teacher`, `/auth/login`, `/auth/logout`, `GET` / `PUT /auth/me`, `POST /auth/change-password`; màn A-01 → A-04; helper e2e `registerTeacher()` / `login()` |
| 2026-11-11 | **M3 Học sinh & Phụ huynh** (nhánh `feature/m3-students`): `StudentsController` / `StudentsService` (danh sách + `accountStatus` suy ra bằng `StudentAccount.StatusOf`, thêm học sinh kèm liên kết phụ huynh, cấp tài khoản + mật khẩu tạm một lần `Cache-Control: no-store`, cấp lại mật khẩu tạm, liên kết / gỡ phụ huynh; AuditLog không chứa mật khẩu); G-02 (Kendo Grid, drawer chi tiết 460 px dựng riêng thay `kendo-drawer`, hộp thoại mật khẩu tạm phải tick xác nhận mới đóng); helper e2e `createStudent()` / `grantAccount()` / `createActiveStudent()`; chuyển logic từ mã tham khảo `276177a` |
| 2026-11-10 | **M2 Quỹ** (nhánh `feature/m2-funds`): `FundsController` / `FundsService` (danh sách quỹ theo vai trò, tạo quỹ + 6 danh mục mặc định, đổi tên + AuditLog `Fund.Create` / `Fund.Rename`); G-01 Quỹ của tôi / Quỹ của con + dialog tạo quỹ, G-04 Thông tin quỹ (khớp Canvas, so bằng Playwright MCP); sau tạo / đổi tên gọi `restoreSession()` để header cập nhật |
| 2026-11-11 | **M4 Phân công thủ quỹ** (nhánh `feature/m4-treasurers`): `TreasurersController` / `TreasurersService` (phân công học sinh `Active` theo `StudentAccount.StatusOf`, thu hồi trong transaction + `SELECT … FOR UPDATE` dòng `Fund` chặn mất thủ quỹ cuối, lịch sử, AuditLog `Treasurer.Assign` / `Treasurer.Revoke`); G-03 + dialog chọn học sinh (khớp Canvas `Treasurers`, so bằng Playwright MCP); test M-02 xóa cứng `TreasurerAssignment` → `P0001` |
| 2026-11-12 | **M6 Thu chi** (nhánh `feature/m6-transactions`): `TransactionsController` / `TransactionsService` (lọc + phân trang server, bản công khai lọc `IsPublic` ngay trong truy vấn gốc và không chọn cột nội bộ, chống lưu trùng bằng `idempotencyKey` + UNIQUE, sửa / hủy khóa dòng `SELECT … FOR UPDATE`, cảnh báo `isNegativeBalance`, lịch sử từ AuditLog `Transaction.Create` / `Update` / `Cancel`); danh mục dùng chung `CategoriesApi` / `categories.models.ts` của M5; S-02 Ledger (Kendo Grid, panel chi tiết 340 px, URL `/transactions/:txId`) + T-01 thêm / sửa / hủy (khớp Canvas `Ledger`, `TxDialog`, so bằng Playwright MCP); helper e2e `createTransaction()`, `assignTreasurer()` |
