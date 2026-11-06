# Backend Known Issues

> Cập nhật 2026-11-06. Trạng thái triển khai xem [implementation-status.md](implementation-status.md). Mục **Đã xử lý** giữ lại để lưu lịch sử. Historical: 124 / 124 test đạt trước khi xóa mã nguồn cũ.

## Đang mở

| Mã | Nội dung |
|---|---|
| KI-02 | Sửa đồng thời cùng một giao dịch: lần ghi sau thắng (DD-06). Nhiều thủ quỹ / quỹ nên có thể xảy ra; nhật ký ghi đủ cả hai lần |
| KI-03 | Không có quên mật khẩu tự phục vụ. Học sinh: GVCN cấp lại mật khẩu tạm (`POST …/students/{id}/reset-password`). Giáo viên / phụ huynh: lệnh `reset-password` – **CLI chưa được làm lại** (Q-07, PD-02, KI-16) |
| KI-04 | Prototype UI trên Design Canvas – vẫn là prototype, chưa có frontend nghiệp vụ |
| KI-05 | Câu hỏi nghiệp vụ Q-01 → Q-10 (`docs/requirements/open-questions.md`) |
| KI-06 | Đăng ký giáo viên chưa xác minh email → có thể chiếm trước email của phụ huynh (PD-01) |
| KI-07 | Thủ quỹ có tài khoản bị khóa / đang chờ đổi mật khẩu vẫn được tính là "thủ quỹ" khi kiểm tra thủ quỹ cuối cùng (PD-06) |
| KI-08 | GVCN thấy họ tên do giáo viên khác đặt cho tài khoản phụ huynh dùng chung (phụ huynh tự sửa được qua `PUT /auth/me`) |
| KI-13 | Chưa có API phân công thủ quỹ: test thủ công trên Swagger phải tạo phân công bằng SQL. *(Một phần đã xử lý 2026-11-06: Fund Lifecycle có `POST /funds` kèm danh mục mặc định – FR-23; Học sinh & phụ huynh có thêm học sinh, cấp tài khoản, liên kết phụ huynh – FR-24 → FR-26)* |
| KI-14 | Mật khẩu `postgres` / `migrator_user` / `app_user` chỉ được đặt **một lần** khi tạo volume Docker. Sửa `.env` sau đó không đổi mật khẩu trong DB → `password authentication failed`. Xử lý: `ALTER ROLE … PASSWORD` theo `.env`, hoặc `docker compose down -v` (mất dữ liệu dev) |
| KI-15 | Chưa hỗ trợ đổi `SECRET_KEY` (pepper): đổi khóa làm mọi mật khẩu đã băm không đăng nhập được. Cả team dùng chung DB phải dùng cùng khóa (TD-98) |
| KI-16 | CLI vận hành `create-teacher`, `reset-password`, `disable-account`, `enable-account` (TD-88) chưa được làm lại |
| KI-18 | `POST /funds/{fundId}/students` chưa chống lưu trùng: nhấn "Lưu học sinh" hai lần tạo hai học sinh trùng tên (danh sách lớp cho phép trùng họ tên). Frontend cần khóa nút khi đang lưu |
| KI-19 | Đổi họ tên học sinh (`PUT …/students/{id}`) không đổi họ tên của tài khoản học sinh (`User.FullName`, học sinh tự sửa qua `PUT /auth/me`); hai tên có thể khác nhau – chưa có quyết định đồng bộ |
| KI-17 | `POST /funds` chưa chống lưu trùng: nhấn "Tạo quỹ" hai lần tạo hai quỹ (chỉ giao dịch có `idempotencyKey` – BR-12). Frontend cần khóa nút khi đang lưu |

## Đã xử lý

| Mã | Nội dung | Xử lý |
|---|---|---|
| KI-01 | DB dev đã áp 14 migration cũ không áp được `InitialClassFund` | Historical – DB dev tạo mới bằng `docker-compose.yml` |
| KI-09 | Chưa chạy API thủ công bằng `dotnet run` | 2026-11-05: chạy API với `app_user` + Swagger, register / login / me / logout đúng |
| KI-10 | Chưa tạo `app_user` / `migrator_user` và phân quyền DB: khi dùng một tài khoản có DDL, trigger `TR_NoDelete`, `TR_AppendOnly` có thể bị tắt | `docker-compose.yml` + `docker/postgres/init/01-roles.sh` tạo hai tài khoản; kiểm chứng `app_user` không DELETE, không tắt trigger, không DDL, không đọc `__EFMigrationsHistory` |
| KI-11 | Chưa đăng nhập được: `NoIdentityStore` từ chối mọi phiên | `IdentityStore` (feature Auth, PR #5) |
| KI-12 | Kịch bản kiểm chứng migration trên PostgreSQL thật chạy tạm, chưa có project integration test trong repo | `tests/SchoolFund.IntegrationTests` (Testcontainers PostgreSQL 16) |
