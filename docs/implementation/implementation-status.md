# Trạng thái triển khai

> Cập nhật: 2026-11-06 · Tài liệu này là nguồn duy nhất về **trạng thái triển khai**. Các tài liệu khác ghi trạng thái theo đây.

## 1. Hiện trạng

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Backend (.NET 10, Clean Architecture) | **Partially Implemented** – Foundation, Domain, AccessService, Auth, Categories, Transactions, Funds | `backend/SchoolFundAPI`: solution, 4 tầng, 8 entity, `AccessService`, pipeline request, xử lý lỗi, `[RequirePermission]`, `GET /api/health`. Feature đã có (mục 1a): **Auth & User Session**, **Danh mục** (Dev B), **Thu chi** (Dev B), **Fund Lifecycle**. Chưa có: Tổng quan (`GET /funds/{id}`), Học sinh & phụ huynh, Phân công thủ quỹ, Báo cáo, Thành viên, CLI vận hành |
| Frontend (Angular 20 + Kendo) | **Partially Implemented** – chỉ có Codebase Foundation | `frontend/SchoolFundUI`: `core` / `shared` / `features`, route theo sitemap, guard, interceptor, AppHeader, AppNav, AppState. Các màn hình đang là trang giữ chỗ |
| Database / migration | **Implemented** (schema) | Migration `InitialClassFund` + `AddDatabaseProtections`: 8 bảng, ràng buộc, index, trigger. Dev: `docker-compose.yml` (PostgreSQL 16) tạo `migrator_user` (DDL) / `app_user` (SELECT / INSERT / UPDATE, không đọc `__EFMigrationsHistory`) |
| Test tự động | **Partially Implemented** | Unit (xUnit + Moq) và Integration (`SchoolFund.IntegrationTests`, PostgreSQL thật qua Testcontainers) cho Foundation, `AccessService`, Auth, Danh mục, Thu chi, Funds; Frontend: Karma, Playwright cho Foundation. Test nghiệp vụ còn lại của `test-cases.md` mục A chưa viết |
| Tài liệu nghiệp vụ, DB, API, phân quyền, test case | Hoàn thiện – Source of Truth | `docs/requirements`, `docs/business-analysis`, `docs/database`, `docs/api`, `docs/testing` |
| UI/UX | Hoàn thiện – Source of Truth (prototype) | Canvas https://claude.ai/artifact/D5JVqKkBFqQhmcp5spfw6Y, `docs/ui-design` |

Giai đoạn hiện tại: triển khai các feature Backend theo Docs + Canvas; Frontend nghiệp vụ chưa bắt đầu.

## 1a. Trạng thái theo feature Backend

| Feature | Yêu cầu | Endpoint | Trạng thái | Nhánh / PR |
|---|---|---|---|---|
| Auth & User Session | FR-20, FR-13, FR-21, BR-17, BR-18, BR-26, BR-28, NFR-10, NFR-11 | `POST /auth/register-teacher`, `POST /auth/login`, `POST /auth/logout`, `GET/PUT /auth/me`, `POST /auth/change-password`, `GET /auth/antiforgery` | **Implemented** | `feature/devA/auth-session` – PR #5 (đã merge) |
| Fund Lifecycle | FR-22, FR-23, FR-11, BR-02, BR-23, BR-36 | `GET /funds`, `POST /funds`, `PUT /funds/{fundId}` | **Implemented** – chờ review / merge | `feature/devA/fund-lifecycle` |
| Danh mục | FR-10, BR-08, BR-25 | `GET/POST /funds/{fundId}/categories`, `PUT /funds/{fundId}/categories/{id}` | **Implemented** (Dev B) | `feature/be-categories` – PR #6 (đã merge) |
| Tổng quan quỹ | FR-01 | `GET /funds/{fundId}` | Not Implemented | – |
| Học sinh, phụ huynh | FR-24 → FR-26 | `/funds/{fundId}/students…` | Not Implemented | – |
| Phân công thủ quỹ | FR-27 | `/funds/{fundId}/treasurers…` | Not Implemented | – |
| Thu chi | FR-02 → FR-07 | `/funds/{fundId}/transactions…` (thêm, sửa, hủy, danh sách, chi tiết, lịch sử) | **Implemented** (Dev B) | `feature/be-transactions` – PR #7 (đã merge) |
| Báo cáo | FR-08, FR-09 | `/funds/{fundId}/reports/*` | Not Implemented | – |
| Thành viên | FR-12 | `GET /funds/{fundId}/members` | Not Implemented | – |
| CLI vận hành | TD-88, KI-03 | `create-teacher`, `reset-password`, `disable-account`, `enable-account` | Not Implemented | – |
| Khóa học sinh, rời lớp, chuyển quỹ | FR-28 | – | Pending Decision (PD-03 → PD-05) | – |

Chi tiết từng feature đã làm: [backend-progress.md](backend-progress.md).

## 2. Quy ước trạng thái trong tài liệu

| Trạng thái | Nghĩa |
|---|---|
| `Not Implemented` | Đã đặc tả / đã quyết định, chưa có mã nguồn (áp dụng cho các yêu cầu, quyết định, endpoint, test case chưa có mã nguồn) |
| `Partially Implemented` / `Implemented` | Đã có mã nguồn mới, được kiểm thử tự động |
| `Pending Decision` | Chờ PO quyết định (PD-01 → PD-06, UQ-23) |
| *Historical* | Kết quả / mô tả của mã nguồn đã xóa – chỉ để tham khảo, không phản ánh hiện trạng |

Đặc tả (nghiệp vụ, database, API, roles & permissions, validation, test case) **giữ nguyên** và là căn cứ cho lần triển khai tiếp theo.

## 3a. Kết quả kiểm chứng hiện tại

| Ngày | Phạm vi | Kết quả | Môi trường |
|---|---|---|---|
| 2026-11-05 | Backend Foundation + Domain + AccessService | Build 0 warning / 0 error · Unit 50 / 50 | .NET SDK 10.0.403 |
| 2026-11-05 | Frontend Foundation | Unit 29 / 29 · E2E 9 / 9 (`/auth/me` giả lập) | Chrome |
| 2026-11-05 | Migration trên DB thật | Áp, rollback về 0, áp lại đều đạt · 17 / 17 kịch bản. Bộ kịch bản này chạy tạm, không nằm trong repo | PostgreSQL 16.15 (container tạm, đã xóa) |
| 2026-11-05 | Auth & User Session (PR #5) | Build 0 / 0 · **Unit 89 / 89** · **Integration 18 / 18** · chạy tay API (`app_user`) + Swagger: register / login / me / logout đúng | .NET SDK 10.0.403, PostgreSQL 16 (Testcontainers + docker compose) |
| 2026-11-06 | Backend Auth + Categories + Transactions (PR #7) | Build 0 warning / 0 error · **Unit 206 / 206** · **Integration 44 / 44** · Swagger UI 35 / 35 bước với `app_user` | .NET SDK 10.0.403, PostgreSQL 16, Chrome |
| 2026-11-06 | Fund Lifecycle trên `main` có Auth + Danh mục + Thu chi (sau merge) | Build 0 / 0 · **Unit 216 / 216** · **Integration 52 / 52** | .NET SDK 10.0.403, PostgreSQL 16 (Testcontainers) |

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
| 2026-11-05 | Feature Auth & User Session; `IdentityStore` thay `NoIdentityStore`; project `SchoolFund.IntegrationTests`; `docker-compose.yml` cho DB dev (PR #5) |
| 2026-11-06 | Thêm feature Backend Auth, Categories, Transactions và project `SchoolFund.IntegrationTests` (Danh mục – PR #6, Thu chi – PR #7, Dev B) |
| 2026-11-06 | Feature Fund Lifecycle (nhánh `feature/devA/fund-lifecycle`) |
