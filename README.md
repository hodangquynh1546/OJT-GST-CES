# OJT Project

## School Fund Management

Hệ thống quản lý quỹ lớp học sinh: quản lý các khoản thu, chi và theo dõi số dư quỹ lớp một cách minh bạch.

### Công nghệ

| Tầng | Công nghệ |
|---|---|
| Frontend | Angular ^20.3, TypeScript ~5.9, Kendo UI for Angular 23.3.0 |
| Backend | .NET 10, ASP.NET Core Web API, FluentValidation, Serilog, Swagger |
| Database | PostgreSQL, Entity Framework Core 10 (Npgsql) |
| Phân quyền | Permission-based authorization |

Chi tiết phiên bản và kiến trúc: xem [`training-doc.md`](training-doc.md).

### Kiến trúc

Clean Architecture gồm các tầng **Api → Application → Domain**, với **Infrastructure** cài đặt truy cập dữ liệu (EF Core → PostgreSQL). Angular SPA được Api phục vụ trên cùng origin và gọi qua `/api`.

### Cấu trúc thư mục

```text
.
├── backend/SchoolFundAPI/   # Workspace .NET: SchoolFund.sln, src/, tests/
├── frontend/SchoolFundUI/   # Workspace Angular: src/app/{core,shared,features}, e2e/
├── docs/           # Tài liệu: requirements, business-analysis, architecture, database, api, ui-design
├── .claude/skills/ # Skill cho Claude Code
├── CLAUDE.md       # Hướng dẫn cho Claude Code
└── training-doc.md # Tài liệu chuẩn Tech Stack & kiến trúc
```

### Trạng thái hiện tại

- [x] Khởi tạo cấu trúc thư mục dự án
- [x] Phân tích yêu cầu nghiệp vụ
- [x] Thiết kế database (`docs/database`) và migration
- [x] Khởi tạo project backend (.NET) và frontend (Angular) – Codebase Foundation
- [x] Domain Entities, `AppDbContext`, `AccessService`
- [x] Backend: Auth & User Session (đăng ký giáo viên, đăng nhập, mật khẩu tạm, phiên)
- [x] Backend: Danh mục thu chi
- [x] Backend: Thu chi (thêm, sửa, hủy, lịch sử)
- [x] Backend: Fund Lifecycle (quỹ của tôi, tạo quỹ, đổi tên quỹ)
- [x] Backend: Học sinh & phụ huynh (thêm học sinh, cấp tài khoản, liên kết / gỡ phụ huynh)
- [x] Backend: Tổng quan, Báo cáo tháng / 12 tháng, Thành viên
- [x] Backend: Phân công thủ quỹ (phân công / thu hồi, khóa dòng chống thu hồi thủ quỹ cuối cùng)
- [ ] Frontend nghiệp vụ

Chi tiết: [`docs/implementation/implementation-status.md`](docs/implementation/implementation-status.md).

### Chạy dự án

Cần .NET SDK 10, Node.js 22, Chrome và Docker (PostgreSQL 16 dev + Testcontainers cho integration test). Không commit `.env`, connection string, `SECRET_KEY` hay license key.

```bash
# Database dev: PostgreSQL 16 với migrator_user (DDL) / app_user (chỉ đọc ghi dữ liệu)
cd backend/SchoolFundAPI
cp .env.example .env               # điền mật khẩu DB (bắt buộc cho docker compose), connection string, SECRET_KEY
docker compose up -d --wait

# Backend: http://localhost:5286, Swagger ở /swagger (Development)
# Cấu hình cho dotnet (ConnectionStrings:App, ConnectionStrings:Migrator, SECRET_KEY ≥ 32 ký tự – openssl rand -base64 48), chọn một cách:
#   A. Nạp .env vào shell (dotnet không tự đọc .env – nạp lại mỗi lần mở terminal):
set -a && source .env && set +a
#   B. Hoặc dùng user-secrets (lưu một lần; khi đó để trống ConnectionStrings__App / SECRET_KEY trong .env
#      hoặc không source .env – biến môi trường rỗng sẽ che giá trị user-secrets):
#   dotnet user-secrets --project src/SchoolFund.Api set "ConnectionStrings:App" "<connection string app_user>"
#   dotnet user-secrets --project src/SchoolFund.Api set "SECRET_KEY" "<chuỗi ngẫu nhiên từ 32 ký tự>"
#   (dotnet ef database update vẫn cần biến ConnectionStrings__Migrator)
dotnet tool restore
dotnet ef database update --project src/SchoolFund.Infrastructure --startup-project src/SchoolFund.Infrastructure
dotnet run --project src/SchoolFund.Api
dotnet test                        # Unit + Integration (cần Docker đang chạy)

# Frontend: http://localhost:4200, /api chuyển tới backend
cd frontend/SchoolFundUI
npm install
npm start
npm test
npm run e2e
```

Swagger: mọi POST / PUT cần header `X-XSRF-TOKEN` – gọi `GET /auth/antiforgery`, dán `antiforgeryToken` vào nút **Authorize**; lấy token mới sau mỗi lần đăng ký / đăng nhập / đăng xuất / đổi mật khẩu. Mật khẩu DB trong `.env` chỉ có tác dụng khi tạo volume lần đầu (KI-14).

`npm run build` đưa SPA vào `wwwroot` của `SchoolFund.Api`, khi đó Api phục vụ cả SPA và `/api` trên cùng origin. Kendo cần license key lúc build: đặt `KENDO_UI_LICENSE` rồi chạy `npm run license`.
