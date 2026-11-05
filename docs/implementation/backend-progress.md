# Backend Progress

> Cập nhật: 2026-11-05 · Nhánh `feature/codebase`.

## Trạng thái
**Partially Implemented** – mã nguồn cũ đã xóa (D-35) và đang làm lại: đã có Codebase Foundation, Domain Entities, `AppDbContext`, `AccessService`, migration `InitialClassFund` + `AddDatabaseProtections`. Chưa có feature nghiệp vụ. Xem [implementation-status.md](implementation-status.md).

### Historical (mã nguồn đã xóa)
R3 – Giáo viên · Học sinh · Phụ huynh (D-24 → D-33) đã từng được cài đặt đầy đủ trước khi xóa:

| Kiểm chứng (2026-11-05, historical) | Kết quả |
|---|---|
| Build toàn solution (`TreatWarningsAsErrors`) | 0 warning, 0 error |
| Unit test | 57 / 57 |
| Integration test (PostgreSQL 16 – Testcontainers) | 67 / 67 |
| Migration từ DB sạch | `InitialClassFund` + `TeacherStudentParent` → 8 bảng |
| Nâng cấp DB có dữ liệu cũ | `UpgradeMigrationTests` – dữ liệu giữ nguyên |
| Khởi động API thủ công | Chưa từng chạy |

2026-11-04 – R1 (một thủ quỹ / quỹ): 45 unit / 53 integration.

## Chạy dev (historical – áp dụng cho mã nguồn đã xóa, tham khảo khi dựng lại)
```bash
cd backend/SchoolFundAPI
# DB dev đã áp migration cũ (M0 → M14) phải tạo lại trước – XÓA dữ liệu, tự thực hiện khi đồng ý:
#   docker compose down -v && docker compose up -d --wait
export ConnectionStrings__Migrator="Host=127.0.0.1;Port=5432;Database=schoolfund;Username=migrator_user;Password=<MIGRATOR_DB_PASSWORD>"
dotnet tool restore && dotnet ef database update --project src/SchoolFund.Infrastructure --startup-project src/SchoolFund.Infrastructure
dotnet user-secrets --project src/SchoolFund.Api set "ConnectionStrings:App" "Host=127.0.0.1;Port=5432;Database=schoolfund;Username=app_user;Password=<APP_DB_PASSWORD>"
# Giáo viên có thể tự đăng ký qua POST /api/v1/auth/register-teacher; hoặc người vận hành tạo (quỹ tùy chọn):
SCHOOLFUND_EMAIL=co.hanh@example.vn SCHOOLFUND_FULLNAME="Cô Hạnh" SCHOOLFUND_PASSWORD='<≥8 ký tự, có chữ và số>' SCHOOLFUND_FUND_NAME="Quỹ lớp 6A" \
  dotnet run --project src/SchoolFund.Api -- create-teacher
dotnet run --project src/SchoolFund.Api            # http://localhost:5286/swagger
```
Lệnh khác: `reset-password` (`SCHOOLFUND_LOGIN` = email hoặc username – hoặc `SCHOOLFUND_EMAIL` cũ – và `SCHOOLFUND_PASSWORD`), `disable-account`, `enable-account` (`SCHOOLFUND_LOGIN`). `create-treasurer` đã bỏ (D-24).

DB dev đang ở `InitialClassFund`: chạy `dotnet ef database update` để áp `TeacherStudentParent` (bổ sung, không xóa dữ liệu). Sau đó quỹ cũ ở trạng thái "Chưa phân công thủ quỹ" – người phụ trách cũ (nay là GVCN) thêm học sinh, cấp tài khoản, phân công thủ quỹ.
