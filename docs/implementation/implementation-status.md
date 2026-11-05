# Trạng thái triển khai

> Cập nhật: 2026-11-05 · Tài liệu này là nguồn duy nhất về **trạng thái triển khai**. Các tài liệu khác ghi trạng thái theo đây.

## 1. Hiện trạng

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Backend (.NET 10, Clean Architecture) | **Partially Implemented** – Foundation + Domain + AccessService | `backend/SchoolFundAPI`: solution, 4 tầng, 8 entity, `AccessService`, pipeline request, xử lý lỗi, `[RequirePermission]`, `GET /auth/antiforgery`, `GET /api/health`. Chưa có feature nghiệp vụ; `NoIdentityStore` từ chối mọi phiên cho tới khi có feature Auth |
| Frontend (Angular 20 + Kendo) | **Partially Implemented** – chỉ có Codebase Foundation | `frontend/SchoolFundUI`: `core` / `shared` / `features`, route theo sitemap, guard, interceptor, AppHeader, AppNav, AppState. Các màn hình đang là trang giữ chỗ |
| Database / migration | **Implemented** (schema) | Migration `InitialClassFund` + `AddDatabaseProtections`: 8 bảng, ràng buộc, index, trigger; đã kiểm chứng trên PostgreSQL 16 (mục 3a). Chưa cấu hình tài khoản `app_user` / `migrator_user` |
| Test tự động | **Partially Implemented** | Có test của Foundation và `AccessService` (xUnit + Moq, Karma, Playwright); phần lớn test nghiệp vụ mục A của `test-cases.md` chưa viết. Kết quả cũ chỉ còn là lịch sử (mục 3) |
| Tài liệu nghiệp vụ, DB, API, phân quyền, test case | Hoàn thiện – Source of Truth | `docs/requirements`, `docs/business-analysis`, `docs/database`, `docs/api`, `docs/testing` |
| UI/UX | Hoàn thiện – Source of Truth (prototype) | Canvas https://claude.ai/artifact/D5JVqKkBFqQhmcp5spfw6Y, `docs/ui-design` |

Giai đoạn hiện tại: đã có Codebase Foundation, Domain, schema và `AccessService` (nhánh `feature/codebase`, 2026-11-05); các feature nghiệp vụ triển khai tiếp theo Docs + Canvas.

## 2. Quy ước trạng thái trong tài liệu

| Trạng thái | Nghĩa |
|---|---|
| `Not Implemented` | Đã đặc tả / đã quyết định, chưa có mã nguồn (áp dụng cho các yêu cầu, quyết định, endpoint, test case chưa có mã nguồn) |
| `Partially Implemented` / `Implemented` | Dùng lại khi mã nguồn mới được viết và kiểm thử |
| `Pending Decision` | Chờ PO quyết định (PD-01 → PD-06, UQ-23) |
| *Historical* | Kết quả / mô tả của mã nguồn đã xóa – chỉ để tham khảo, không phản ánh hiện trạng |

Đặc tả (nghiệp vụ, database, API, roles & permissions, validation, test case) **giữ nguyên** và là căn cứ cho lần triển khai tiếp theo.

## 3a. Kết quả kiểm chứng hiện tại

| Ngày | Phạm vi | Kết quả | Môi trường |
|---|---|---|---|
| 2026-11-05 | Backend Foundation + Domain + AccessService | Build 0 warning / 0 error · **Unit 50 / 50** | .NET SDK 10.0.403 |
| 2026-11-05 | Frontend Foundation | Unit 29 / 29 · E2E 9 / 9 (`/auth/me` giả lập) | Chrome |
| 2026-11-05 | Migration trên DB thật | Áp, rollback về 0, áp lại đều đạt · 17 / 17 kịch bản (AccessService qua EF, trigger, FK ghép, unique filter, CHECK). Bộ kịch bản này chạy tạm, **chưa nằm trong repo** | PostgreSQL 16.15 (container tạm, đã xóa) |

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
