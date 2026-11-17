# ClassFund – Quản lý quỹ lớp

Hệ thống quản lý quỹ lớp học sinh với **hai phạm vi tài chính độc lập**: quỹ chung của lớp và quỹ từng sự kiện (quyết toán riêng, hoàn tiền thừa / thu bổ sung, số dư cá nhân), kèm thông báo nội bộ và nhật ký hoạt động.

- Yêu cầu & thiết kế: [`docs/README.md`](docs/README.md)
- Tech stack & kiến trúc chuẩn: [`training-doc.md`](training-doc.md)
- Kế hoạch module & task: [`docs/implementation/module-plan.md`](docs/implementation/module-plan.md)

## Cấu trúc

| Thư mục | Nội dung |
|---|---|
| `ClassFundAPI/` | .NET 10: `ClassFund.sln`, `src/` (Api, Application, Domain, Infrastructure), `tests/`, `database/neon-roles.sql` |
| `ClassFundUI/` | Angular 20 + Kendo UI 23.3 |
| `docs/` | Tài liệu |

## Chạy local

Yêu cầu: .NET SDK 10, Node 22, (Docker – chỉ để chạy integration test). Database: Neon dùng chung – xem [`docs/implementation/neon-database-guide.md`](docs/implementation/neon-database-guide.md).

```bash
# Backend
cd ClassFundAPI/src/ClassFund.Api
dotnet user-secrets set "ConnectionStrings:App" "<chuỗi kết nối app_user Neon>"
dotnet user-secrets set "SECRET_KEY" "<≥ 32 ký tự, giống đồng đội>"
dotnet run                                  # http://localhost:5286, Swagger: /swagger

# Frontend (terminal khác)
cd ClassFundUI
npm install
npm start                                   # http://localhost:4200 (proxy /api → 5286)
```

Kiểm tra:

```bash
cd ClassFundAPI && dotnet test
cd ClassFundUI && npx ng test --watch=false --browsers=ChromeHeadless && npx ng build
```

Seed dữ liệu giả (Development): `dotnet run --project ClassFundAPI/src/ClassFund.Api -- --seed`.
