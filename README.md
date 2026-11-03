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
├── backend/        # Mã nguồn .NET (chưa khởi tạo)
├── frontend/       # Mã nguồn Angular (chưa khởi tạo)
├── docs/           # Tài liệu: requirements, business-analysis, architecture, database, api, ui-design
├── .claude/skills/ # Skill cho Claude Code
├── CLAUDE.md       # Hướng dẫn cho Claude Code
└── training-doc.md # Tài liệu chuẩn Tech Stack & kiến trúc
```

### Trạng thái hiện tại

- [x] Khởi tạo cấu trúc thư mục dự án
- [ ] Phân tích yêu cầu nghiệp vụ
- [ ] Khởi tạo project backend (.NET) và frontend (Angular)
- [ ] Thiết kế database
