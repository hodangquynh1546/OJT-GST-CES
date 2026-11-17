# CLAUDE.md

Hướng dẫn cho Claude Code khi làm việc trong repository này.

## 1. Giới thiệu dự án

**ClassFund** – hệ thống quản lý quỹ lớp học sinh với **hai phạm vi tài chính độc lập**: **Quỹ chung** (không quyết toán theo hoạt động, tổng kết cuối năm) và **Quỹ sự kiện** (quyết toán riêng: thừa → hoàn tiền / cộng số dư cá nhân, thiếu → thu bổ sung). Vai trò theo từng quỹ: **Thủ quỹ chính** (Owner), **Thủ quỹ**, **Học sinh**; thủ quỹ cũng tham gia như học sinh và được tự ghi nhận thanh toán của mình (đánh dấu tự ghi nhận, có audit).

Trạng thái (2026-11-17): **đã dựng lại Foundation** trên nhánh `feature/rebuild-foundation` (Domain đủ 18 entity, cấu hình EF, pipeline, phân quyền, audit, notifier, shell FE, route giữ chỗ). **Đã có migration `InitialSchema` (chưa áp Neon) và adapter Neon Object Storage; chưa có code nghiệp vụ.** Tài liệu đã duyệt (M0-01, 2026-11-17). Chi tiết: `docs/implementation/implementation-status.md`.

## 2. Tài liệu chuẩn

- **`training-doc.md`** quy định Tech Stack, phiên bản, kiến trúc. Đọc trước khi tạo/thay đổi project, package, kiến trúc. **Không** sửa, di chuyển, đổi tên, xóa file này.
- Không tự thay đổi công nghệ, phiên bản, kiến trúc khác tài liệu này – hỏi người dùng trước.
- Chỉ mục tài liệu & thứ tự ưu tiên: `docs/README.md`.

## 3. Tech Stack (tóm tắt)

| Tầng | Công nghệ |
|---|---|
| Frontend | Angular ^20.3, TypeScript ~5.9, RxJS, Kendo UI for Angular 23.3.0 |
| Backend | .NET 10, ASP.NET Core Web API, Asp.Versioning, FluentValidation, Swashbuckle, Serilog |
| Database | **Neon PostgreSQL dùng chung** cho hai developer, EF Core 10.0.4, Npgsql |
| File | **Neon Object Storage** (S3-compatible) qua port `IFileStorage`, client `AWSSDK.S3` (ngoại lệ đã duyệt ngoài training-doc, OQ-17) – file thực tế không lưu trong DB |
| AuthN / AuthZ | Cookie session tự thiết kế; phân quyền theo permission (`RequirePermission`), không theo role |

## 4. Kiến trúc

Clean Architecture, một artifact – một origin (Api phục vụ luôn SPA). Domain không phụ thuộc gì; Application → Domain; Infrastructure → Application, Domain; Api → Application (+ Infrastructure chỉ để DI). Mã theo feature: `Features/<Module>/` (BE), `src/app/features/<module>/` (FE). Xem `docs/architecture/architecture-overview.md`.

## 5. Cấu trúc thư mục

| Thư mục | Mục đích |
|---|---|
| `ClassFundAPI/` | Workspace .NET: `ClassFund.sln`, `src/`, `tests/`, `database/neon-roles.sql`, `.env.example` |
| `ClassFundUI/` | Workspace Angular (`scripts/gen-routes.mjs` sinh route giữ chỗ) |
| `docs/requirements/` | FR, câu hỏi mở |
| `docs/business-analysis/` | Phân quyền, quy tắc nghiệp vụ, quy trình |
| `docs/architecture/` | Kiến trúc, **cách làm module**, **quy tắc code** |
| `docs/database/` | Thiết kế CSDL |
| `docs/api/` | Quy ước API, danh mục endpoint |
| `docs/ui-design/` | Design system, sitemap & màn hình |
| `docs/implementation/` | **Module plan**, trạng thái, hướng dẫn Neon |
| `docs/testing/` | Chiến lược & test case |

## 6. Quy tắc làm việc với Claude Code

- Nhận task dạng mã `Mx-Ty` → đọc task trong `docs/implementation/module-plan.md`, FR/BR/AZ/OQ liên quan, `docs/architecture/foundation-guide.md` và `coding-standards.md` trước khi code.
- Chỉ làm đúng phạm vi task; không sửa file Foundation (`Hosting/`, `Common/`, `Authorization/`, `Filters/`, `Middleware/`, `AppDbContext`, `core/`, `shared/`, `app.routes.ts`) trừ khi task yêu cầu.
- OQ chưa chốt hoặc thông tin mâu thuẫn: dừng lại và hỏi.
- Kiểm tra hiện trạng trước khi tạo file; không ghi đè/xóa/di chuyển file có sẵn khi chưa được đồng ý.
- Không cài package, tạo project, tạo migration khi task không yêu cầu. **Không bao giờ** tự chạy migration, xóa/reset dữ liệu trên Neon.
- Build phải 0 warning (`TreatWarningsAsErrors`); chạy `dotnet test`, `ng test`, `ng build` trước khi báo xong.
- Sau mỗi task: báo cáo file tạo/sửa, kết quả test thật, giới hạn đã tuân thủ, cập nhật tài liệu theo `docs/README.md` mục 4.

## 7. Quy tắc Git

- Nhánh chính `main`; làm trên `feature/<tên>`, không commit thẳng `main`.
- Claude Code **không** tự commit/push; chỉ khi người dùng yêu cầu rõ.
- Commit message ngắn gọn: `feat: ...`, `fix: ...`, `docs: ...`, `chore: ...`, `test: ...`.
- Không commit secret, connection string, `SECRET_KEY`, license Kendo, `.env`, `App_Data/`, file build (`wwwroot/`).
