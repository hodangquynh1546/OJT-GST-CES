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
- [ ] Triển khai các feature nghiệp vụ

Chi tiết: [`docs/implementation/implementation-status.md`](docs/implementation/implementation-status.md).

### Chạy dự án

Cần .NET SDK 10, Node.js 22, Chrome và PostgreSQL. Không commit connection string hay license key.

```bash
# Backend: http://localhost:5286, Swagger ở /swagger (Development)
cd backend/SchoolFundAPI
dotnet user-secrets --project src/SchoolFund.Api set "ConnectionStrings:App" "<connection string>"
dotnet tool restore
ConnectionStrings__Migrator="<connection string>" dotnet ef database update --project src/SchoolFund.Infrastructure --startup-project src/SchoolFund.Infrastructure
dotnet run --project src/SchoolFund.Api
dotnet test

# Frontend: http://localhost:4200, /api chuyển tới backend
cd frontend/SchoolFundUI
npm install
npm start
npm test
npm run e2e
```

`npm run build` đưa SPA vào `wwwroot` của `SchoolFund.Api`, khi đó Api phục vụ cả SPA và `/api` trên cùng origin. Kendo cần license key lúc build: đặt `KENDO_UI_LICENSE` rồi chạy `npm run license`.
