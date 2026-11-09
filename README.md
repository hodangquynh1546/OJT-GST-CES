# OJT Project

## ClassFund – Quản lý quỹ lớp

Hệ thống quản lý quỹ lớp học sinh: quản lý các khoản thu, chi và theo dõi số dư quỹ lớp một cách minh bạch. *(Tên cũ: SchoolFund – đổi thành ClassFund ngày 2026-11-06.)*

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
├── ClassFundAPI/   # Workspace .NET: ClassFund.sln, src/, tests/, docker-compose.yml
├── ClassFundUI/    # Workspace Angular: src/app/{core,shared,features}, e2e/
├── docs/           # Tài liệu: requirements, business-analysis, architecture, database, api, ui-design, implementation, testing
├── CLAUDE.md       # Hướng dẫn cho Claude Code
└── training-doc.md # Tài liệu chuẩn Tech Stack & kiến trúc
```

### Trạng thái hiện tại

- [x] Khởi tạo cấu trúc thư mục dự án
- [x] Phân tích yêu cầu nghiệp vụ
- [x] Thiết kế database (`docs/database`) và migration
- [x] Khởi tạo project backend (.NET) và frontend (Angular) – Codebase Foundation
- [x] Domain Entities, `AppDbContext`, `AccessService`
- [x] Foundation Backend + Frontend: quy ước API ([`docs/api/api-conventions.md`](docs/api/api-conventions.md)), Design System, khung ứng dụng, hạ tầng test / e2e ([`docs/architecture/foundation-guide.md`](docs/architecture/foundation-guide.md))
- [ ] Module nghiệp vụ M1 → M8, mỗi module trọn luồng FE → BE → DB → Test ([`docs/implementation/module-plan.md`](docs/implementation/module-plan.md)) – backend bản trước tham khảo tại commit `6b6645d`

Chi tiết: [`docs/implementation/implementation-status.md`](docs/implementation/implementation-status.md).

### Chạy dự án

Cần .NET SDK 10, Node.js 22, Chrome và Docker (PostgreSQL 16 dev + Testcontainers cho integration test). Không commit `.env`, connection string, `SECRET_KEY` hay license key.

```bash
# Database dev: PostgreSQL 16 với migrator_user (DDL) / app_user (chỉ đọc ghi dữ liệu)
cd ClassFundAPI
cp .env.example .env               # điền mật khẩu DB (bắt buộc cho docker compose), connection string, SECRET_KEY
docker compose up -d --wait

# Backend: http://localhost:5286, Swagger ở /swagger (Development)
# Cấu hình cho dotnet (ConnectionStrings:App, ConnectionStrings:Migrator, SECRET_KEY ≥ 32 ký tự – openssl rand -base64 48), chọn một cách:
#   A. Nạp .env vào shell (dotnet không tự đọc .env – nạp lại mỗi lần mở terminal):
set -a && source .env && set +a
#   B. Hoặc dùng user-secrets (lưu một lần; khi đó để trống ConnectionStrings__App / SECRET_KEY trong .env
#      hoặc không source .env – biến môi trường rỗng sẽ che giá trị user-secrets):
#   dotnet user-secrets --project src/ClassFund.Api set "ConnectionStrings:App" "<connection string app_user>"
#   dotnet user-secrets --project src/ClassFund.Api set "SECRET_KEY" "<chuỗi ngẫu nhiên từ 32 ký tự>"
#   (dotnet ef database update vẫn cần biến ConnectionStrings__Migrator)
dotnet tool restore
dotnet ef database update --project src/ClassFund.Infrastructure --startup-project src/ClassFund.Infrastructure
dotnet run --project src/ClassFund.Api
dotnet test                        # Unit + Integration + hợp đồng API (cần Docker đang chạy)
# Đổi API có chủ đích: UPDATE_API_CONTRACTS=1 dotnet test --filter ApiContractTests (commit snapshot cùng PR)

# Frontend: http://localhost:4200, /api chuyển tới backend
cd ClassFundUI
npm install
npm start
npm test
npm run e2e                        # E2E mock (API giả lập)
npm run e2e:full                   # E2E thật FE + BE + DB: cần DB dev đã migrate và đã source .env (xem docs/architecture/foundation-guide.md)
```

Swagger: mọi POST / PUT cần header `X-XSRF-TOKEN` – gọi `GET /auth/antiforgery`, dán `antiforgeryToken` vào nút **Authorize**; lấy token mới sau mỗi lần đăng ký / đăng nhập / đăng xuất / đổi mật khẩu. Mật khẩu DB trong `.env` chỉ có tác dụng khi tạo volume lần đầu (KI-14).

`npm run build` đưa SPA vào `wwwroot` của `ClassFund.Api`, khi đó Api phục vụ cả SPA và `/api` trên cùng origin. Kendo cần license key lúc build: đặt `KENDO_UI_LICENSE` rồi chạy `npm run license`.
