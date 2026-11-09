# CLAUDE.md

Hướng dẫn cho Claude Code khi làm việc trong repository này.

## 1. Giới thiệu dự án

**ClassFund** (Class Fund Management, trước đây là SchoolFund) – hệ thống quản lý quỹ lớp học sinh (thu, chi, theo dõi số dư và minh bạch quỹ lớp).

Trạng thái: Foundation Backend + Frontend (Domain, schema, `AccessService`, pipeline, quy ước API, Design System, shell); backend nghiệp vụ đã đưa về codebase (2026-11-09) và làm lại **theo module trọn luồng FE → BE → DB → Test** (`docs/implementation/module-plan.md`, `docs/architecture/foundation-guide.md`, `docs/api/api-conventions.md`). Chi tiết ở `docs/implementation/implementation-status.md`.

## 2. Tài liệu chuẩn

- **`training-doc.md`** là tài liệu chuẩn quy định Tech Stack, phiên bản và kiến trúc. Luôn đọc file này trước khi tạo hoặc thay đổi project, package, kiến trúc.
- **Không** chỉnh sửa, di chuyển, đổi tên hoặc xóa `training-doc.md`.
- **Không** tự ý thay đổi công nghệ, phiên bản hoặc kiến trúc khác với tài liệu này. Nếu cần thay đổi, hỏi người dùng trước.

## 3. Tech Stack (tóm tắt – chi tiết xem `training-doc.md`)

| Tầng | Công nghệ chính |
|---|---|
| Frontend | Angular ^20.3, TypeScript ~5.9, RxJS, Kendo UI for Angular 23.3.0 |
| Backend | .NET 10 (`net10.0`), ASP.NET Core Web API, Asp.Versioning, FluentValidation, Swashbuckle, Serilog |
| Database | PostgreSQL, EF Core 10.0.4, Npgsql provider |
| AuthN / AuthZ | Cơ chế xác thực tự thiết kế; phân quyền theo permission (`RequirePermission`), không theo role |

## 4. Kiến trúc

Clean Architecture, một artifact – một origin (Api phục vụ luôn SPA):

```text
Angular UI --/api--> Api --> Application --> Domain
                      |           ^
                      +--DI--> Infrastructure (EF Core) --> PostgreSQL
```

- **Domain**: entity, rule; không phụ thuộc project/NuGet nào.
- **Application**: service theo feature, DTO, interface (port). Phụ thuộc Domain.
- **Infrastructure**: cài đặt port (EF Core, query). Phụ thuộc Application, Domain.
- **Api**: controller, middleware, validation, authZ, host SPA. Chỉ tham chiếu Infrastructure để đăng ký DI.
- Mã tổ chức theo feature (`Features/` ở backend, `src/app/features/` ở frontend).

## 5. Cấu trúc thư mục

| Thư mục | Mục đích |
|---|---|
| `ClassFundAPI/` | Workspace .NET (`{ProjectName}` = `ClassFund`): `ClassFund.sln`, `src/`, `tests/`, `docker-compose.yml` |
| `ClassFundUI/` | Workspace Angular (npm workspace độc lập) |
| `docs/README.md` | **Chỉ mục + chuẩn tài liệu** (bản đồ, thứ tự ưu tiên, mã định danh, tài liệu cần cập nhật khi làm module) |
| `docs/requirements/` | Yêu cầu nghiệp vụ, user story |
| `docs/business-analysis/` | Phân tích nghiệp vụ, use case, quy trình |
| `docs/architecture/` | Thiết kế kiến trúc; `foundation-guide.md` – cách làm một module FE → BE → DB → Test |
| `docs/database/` | Thiết kế CSDL, ERD |
| `docs/api/` | Đặc tả API |
| `docs/ui-design/` | Wireframe, thiết kế giao diện |
| `docs/implementation/` | Trạng thái triển khai, roadmap, quyết định kỹ thuật |
| `docs/testing/` | Test case |
| `.claude/skills/` | Skill dùng riêng cho Claude Code trong dự án (chưa tạo) |

## 6. Quy tắc làm việc với Claude Code

- Chỉ thực hiện đúng phạm vi TASK được giao; không tự mở rộng.
- Kiểm tra hiện trạng trước khi tạo file; không ghi đè, xóa, di chuyển file có sẵn khi chưa được đồng ý.
- Gặp xung đột hoặc thông tin không rõ: dừng lại và hỏi.
- Không cài package, tạo project, database hoặc migration khi TASK không yêu cầu.
- Tuân thủ cấu hình build trong `training-doc.md` (Nullable bật, `TreatWarningsAsErrors`).
- Sau mỗi TASK: báo cáo file đã tạo/sửa và xác nhận các giới hạn đã tuân thủ.

## 7. Quy tắc Git

- Nhánh chính: `main`. Làm việc trên nhánh `feature/<tên>`, không commit trực tiếp lên `main`.
- Claude Code **không** tự commit hoặc push; chỉ làm khi người dùng yêu cầu rõ ràng.
- Commit message ngắn gọn, mô tả đúng thay đổi (ví dụ: `feat: ...`, `fix: ...`, `docs: ...`, `chore: ...`).
- Không commit secret, connection string, license key (Kendo) hoặc file build.
