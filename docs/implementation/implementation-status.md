# Trạng thái triển khai

> Cập nhật: 2026-11-05 · Tài liệu này là nguồn duy nhất về **trạng thái triển khai**. Các tài liệu khác ghi trạng thái theo đây.

## 1. Hiện trạng

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Backend (.NET 10, Clean Architecture) | **Partially Implemented** – chỉ có Codebase Foundation | `backend/SchoolFundAPI`: solution, 4 tầng, pipeline request, xử lý lỗi, `[RequirePermission]`, `GET /auth/antiforgery`, `GET /api/health`. Chưa có feature nghiệp vụ; `NoIdentityStore` từ chối mọi phiên cho tới khi có feature Auth |
| Frontend (Angular 20 + Kendo) | **Partially Implemented** – chỉ có Codebase Foundation | `frontend/SchoolFundUI`: `core` / `shared` / `features`, route theo sitemap, guard, interceptor, AppHeader, AppNav, AppState. Các màn hình đang là trang giữ chỗ |
| Database / migration | **Not Implemented** | Migration `InitialClassFund`, `TeacherStudentParent` bị xóa cùng mã nguồn; thiết kế giữ trong `docs/database` |
| Test tự động | **Partially Implemented** | Chỉ có test của Foundation (xUnit + Moq, Karma, Playwright); test nghiệp vụ mục A của `test-cases.md` chưa viết. Kết quả cũ chỉ còn là lịch sử (mục 3) |
| Tài liệu nghiệp vụ, DB, API, phân quyền, test case | Hoàn thiện – Source of Truth | `docs/requirements`, `docs/business-analysis`, `docs/database`, `docs/api`, `docs/testing` |
| UI/UX | Hoàn thiện – Source of Truth (prototype) | Canvas https://claude.ai/artifact/D5JVqKkBFqQhmcp5spfw6Y, `docs/ui-design` |

Giai đoạn hiện tại: đã dựng Codebase Foundation (nhánh `feature/codebase`, 2026-11-05); các feature nghiệp vụ triển khai tiếp theo Docs + Canvas.

## 2. Quy ước trạng thái trong tài liệu

| Trạng thái | Nghĩa |
|---|---|
| `Not Implemented` | Đã đặc tả / đã quyết định, chưa có mã nguồn (áp dụng cho mọi yêu cầu, quyết định, bảng DB, endpoint, test case tại thời điểm này) |
| `Partially Implemented` / `Implemented` | Dùng lại khi mã nguồn mới được viết và kiểm thử |
| `Pending Decision` | Chờ PO quyết định (PD-01 → PD-06, UQ-23) |
| *Historical* | Kết quả / mô tả của mã nguồn đã xóa – chỉ để tham khảo, không phản ánh hiện trạng |

Đặc tả (nghiệp vụ, database, API, roles & permissions, validation, test case) **giữ nguyên** và là căn cứ cho lần triển khai tiếp theo.

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
