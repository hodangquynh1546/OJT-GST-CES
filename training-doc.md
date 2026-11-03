# Tech Stack & Cấu trúc project

> Quy ước tên: `{ProjectName}` là tiền tố do người làm tự định nghĩa (thay cho tên project hiện có trong repo). Ví dụ `{ProjectName}.Api`, `{ProjectName}UI`.

## 1. Tech Stack

Phiên bản lấy từ `package.json`, các `*.csproj` và `Directory.Build.props`. Dấu `^` và `~` là dải phiên bản.

### Frontend (`{ProjectName}UI/`)
| Công nghệ | Phiên bản | Vai trò |
|---|---|---|
| Angular | ^20.3 | Framework SPA (`core`, `router`, `forms`, `localize`) |
| TypeScript | ~5.9.2 | Ngôn ngữ |
| RxJS / zone.js | ~7.8 / ~0.15 | Reactive programming / change detection |
| Kendo UI for Angular | 23.3.0 | Bộ UI component (grid, dialog, inputs, export Excel/PDF...). Cần license key lúc build |
| Kendo Theme Default | ^14.3.0 | Theme cho Kendo |

### Backend (`{ProjectName}API/`)
| Công nghệ | Phiên bản | Vai trò |
|---|---|---|
| .NET / C# | net10.0, `LangVersion latest` | Runtime. Cấu hình chung ở `Directory.Build.props` (Nullable bật, `TreatWarningsAsErrors`) |
| ASP.NET Core Web API | 10.0 | REST API, đồng thời phục vụ file tĩnh của SPA |
| Asp.Versioning.Mvc (+ApiExplorer) | 10.2.1 | Versioning cho API |
| FluentValidation | 12.1.1 | Validate request ở tầng API |
| Swashbuckle.AspNetCore | 10.2.3 | Sinh OpenAPI/Swagger |
| Serilog (AspNetCore, Sinks.Async, Compact, Enrichers) | 10.0.0 (AspNetCore) | Structured logging dạng JSON |

### Database
| Công nghệ | Phiên bản | Vai trò |
|---|---|---|
| PostgreSQL | tự chọn | CSDL chính |
| Entity Framework Core | 10.0.4 | ORM, migrations |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | Provider PostgreSQL cho EF Core |

### Authentication / Authorization
| Công nghệ | Phiên bản | Vai trò |
|---|---|---|
| Cơ chế xác thực | tự định nghĩa | Do người làm tự thiết kế |
| Permission-based authorization (`RequirePermission`) | code nội bộ | Phân quyền theo permission, không theo role |

## 2. Cấu trúc project tổng quan

### a) Sơ đồ kiến trúc

```text
+--------- Một artifact, một origin ---------+
|  {ProjectName}UI  --- /api --->  {ProjectName}.Api   |
|  Angular + Kendo UI              ASP.NET Core + serve SPA |
+----------------------------------------------+
                                   |  (gọi service)         . DI registration
                                   v                        .
                       {ProjectName}.Application  <---  {ProjectName}.Infrastructure
                                   |                        |
                                   v                        v
                       {ProjectName}.Domain  <------- (EF Core) ---> PostgreSQL
```

### b) Cây thư mục

```text
.
├── {ProjectName}API/                   # Workspace backend (.NET), build unit: file .sln
│   ├── Directory.Build.props       # Cấu hình build chung (net10.0, warnings as errors)
│   ├── src/
│   │   ├── {ProjectName}.Api/            # Entry point web: controller, middleware, authorization, host SPA
│   │   ├── {ProjectName}.Application/    # Service, DTO, interface (port) cho Infrastructure
│   │   ├── {ProjectName}.Domain/         # Entity, enum, rule; không phụ thuộc gì
│   │   └── {ProjectName}.Infrastructure/ # EF Core, query đọc, cài đặt các port
│   └── tests/
│       └── {ProjectName}.UnitTests/      # Test xUnit + Moq cho các project trong src
└── {ProjectName}UI/                    # Workspace frontend (Angular), npm workspace độc lập
  ├── src/app/                    # Mã ứng dụng (core, shared, features)
  ├── src/assets, environments    # Tài nguyên tĩnh, cấu hình môi trường
  ├── e2e/                        # Playwright E2E
  └── angular.json, proxy.conf.json
```

### c) Mô tả thành phần

| Thành phần | Trách nhiệm | Thư mục | Phụ thuộc |
|---|---|---|---|
| Frontend | SPA chạy trên trình duyệt, gọi `/api` | `{ProjectName}UI/` | Api (qua HTTP) |
| Api | Controller, validate, authN/authZ, middleware, serve SPA | `{ProjectName}API/src/{ProjectName}.Api/` | Application, Infrastructure (chỉ để đăng ký DI) |
| Application | Service theo feature, DTO, interface (port) | `{ProjectName}API/src/{ProjectName}.Application/` | Domain |
| Domain | Entity, value object, rule; không có project reference hay NuGet | `{ProjectName}API/src/{ProjectName}.Domain/` | Không có |
| Infrastructure | Cài đặt các port: persistence, query | `{ProjectName}API/src/{ProjectName}.Infrastructure/` | Application, Domain |

Mã theo feature được tổ chức mỗi feature một thư mục (`Features/` ở Api và Application, `src/app/features/` ở UI).
Phía UI, `core/` chứa auth, config, interceptors, navigation, services; `shared/` chứa components, directives, models, pipes, utils.

### d) Luồng request điển hình

1. Trình duyệt tải SPA từ cùng origin; Angular gọi `/api/...`. Khi dev, `proxy.conf.json` chuyển tiếp request; interceptor trong `src/app/core/interceptors/` xử lý request chung.
2. Pipeline trong Api: `CorrelationIdMiddleware`, xác thực, `CurrentUserMiddleware`, rồi kiểm tra `[RequirePermission]` (`Authorization/`).
3. Controller trong `Features/` nhận request; `ValidationFilter` chạy FluentValidation.
4. Controller gọi service trong Application; service dùng entity/rule của Domain và gọi qua interface (port).
5. Infrastructure cài đặt port: `DbContext` (EF Core, `IQueryable`) truy vấn PostgreSQL.
6. Kết quả map thành DTO và trả JSON; lỗi đi qua `GlobalExceptionHandler`; Angular render lại.