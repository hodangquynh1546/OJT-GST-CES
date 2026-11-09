# Known Issues

> Cập nhật 2026-11-09. Trạng thái triển khai: [implementation-status.md](implementation-status.md). Codebase hiện chỉ có Foundation (D-39): mục **Đang mở** ghi rủi ro / giới hạn đã biết; cột **Module** cho biết module nào phải xử lý hoặc tuân theo khi triển khai (đa số chưa phải lỗi của code hiện tại). Mục **Đã xử lý** giữ để lưu lịch sử.

## Đang mở

| Mã | Nội dung | Module |
|---|---|---|
| KI-02 | Sửa đồng thời cùng một giao dịch: lần ghi sau thắng (DD-06). Nhiều thủ quỹ / quỹ nên có thể xảy ra; nhật ký ghi đủ cả hai lần | M6 |
| KI-03 | Không có quên mật khẩu tự phục vụ. Học sinh: GVCN cấp lại mật khẩu tạm (`POST …/students/{id}/reset-password`). Giáo viên / phụ huynh: lệnh `reset-password` – **CLI chưa được làm lại** (Q-07, PD-02, KI-16) | M3 · CLI |
| KI-04 | UI chuẩn là Canvas v4.0 (prototype); frontend nghiệp vụ chưa có – mỗi module dựng màn hình của mình | M1 → M8 |
| KI-05 | Câu hỏi nghiệp vụ Q-01 → Q-10 (`docs/requirements/open-questions.md`) | – |
| KI-06 | Đăng ký giáo viên chưa xác minh email → có thể chiếm trước email của phụ huynh (PD-01) | M1 (PD-01) |
| KI-07 | Thủ quỹ có tài khoản bị khóa / đang chờ đổi mật khẩu vẫn được tính là "thủ quỹ" khi kiểm tra thủ quỹ cuối cùng (PD-06). *Bản trước (`6b6645d`) áp dụng mặc định: chỉ đếm phân công – M4 giữ mặc định này cho tới khi PD-06 được quyết định. Ví dụ: sau khi GVCN cấp lại mật khẩu tạm cho thủ quỹ duy nhất, học sinh vẫn là thủ quỹ nhưng nhận 403 `password.change_required` cho tới khi đổi mật khẩu* | M4 (PD-06) |
| KI-08 | GVCN thấy họ tên do giáo viên khác đặt cho tài khoản phụ huynh dùng chung (phụ huynh tự sửa được qua `PUT /auth/me`) | M3 |
| KI-14 | Mật khẩu `postgres` / `migrator_user` / `app_user` chỉ được đặt **một lần** khi tạo volume Docker. Sửa `.env` sau đó không đổi mật khẩu trong DB → `password authentication failed`. Xử lý: `ALTER ROLE … PASSWORD` theo `.env`, hoặc `docker compose down -v` (mất dữ liệu dev) | Môi trường |
| KI-15 | Chưa hỗ trợ đổi `SECRET_KEY` (pepper): đổi khóa làm mọi mật khẩu đã băm không đăng nhập được. Cả team dùng chung DB phải dùng cùng khóa (TD-98) | Foundation |
| KI-16 | CLI vận hành `create-teacher`, `reset-password`, `disable-account`, `enable-account` (TD-88) chưa được làm lại | CLI (chưa xếp lịch) |
| KI-18 | `POST /funds/{fundId}/students` không có chống lưu trùng (đặc tả): nhấn "Lưu học sinh" hai lần tạo hai học sinh trùng tên (danh sách lớp cho phép trùng họ tên). Frontend cần khóa nút khi đang lưu | M3 |
| KI-19 | Đổi họ tên học sinh (`PUT …/students/{id}`) không đổi họ tên của tài khoản học sinh (`User.FullName`, học sinh tự sửa qua `PUT /auth/me`); hai tên có thể khác nhau – chưa có quyết định đồng bộ | M3 (chưa quyết định) |
| KI-20 | `GET /funds/{fundId}/treasurers` trả toàn bộ lịch sử phân công, không phân trang. Quy mô một lớp (vài chục dòng) nên chấp nhận được; cần phân trang nếu lịch sử lớn | M4 |
| KI-17 | `POST /funds` không có chống lưu trùng (đặc tả): nhấn "Tạo quỹ" hai lần tạo hai quỹ (chỉ giao dịch có `idempotencyKey` – BR-12). Frontend cần khóa nút khi đang lưu | M2 |
| KI-21 | Sau khi đổi tên SchoolFund → ClassFund (2026-11-06), máy đã chạy bản cũ cần: (1) DB dev mới – compose tạo volume `classfundapi_classfund-pgdata` rỗng, chạy lại `docker compose up -d --wait` + `dotnet ef database update`; volume cũ `schoolfundapi_schoolfund-pgdata` **không bị xóa**, tự xóa bằng `docker volume rm schoolfundapi_schoolfund-pgdata` khi không cần; (2) `.env`: đổi `POSTGRES_DB` và `Database=` trong connection string thành `classfund`; (3) user-secrets cũ (`schoolfund-api`) không còn được đọc – đặt lại nếu dùng; (4) cookie đổi tên nên phiên đăng nhập cũ hết hiệu lực, đăng nhập lại | Môi trường |
| KI-22 | E2E full dùng chung DB dev (`classfund-postgres`): dữ liệu test tích lũy, không tự xóa. Test phải dùng `unique()` cho email / tên, không phụ thuộc thứ tự; muốn làm sạch: `docker compose down -v` (mất dữ liệu dev) | Foundation |
| KI-23 | Sau khi bỏ test của bản trước, **chưa có test tự động** cho trigger `TR_NoDelete` / `TR_AppendOnly` và quyền `app_user` (M-02, M-03). Migration vẫn tạo đủ; M3 (Student, ParentLink), M4 (TreasurerAssignment) thêm test integration xóa cứng → `P0001` | M3, M4 |
| KI-24 | Không có deploy nên **không làm CI/CD** ở giai đoạn này: mọi lệnh build / test chạy trên máy dev trước khi mở PR (checklist PR trong [backend-checklist.md](backend-checklist.md)), ghi số liệu vào mô tả PR | – |

## Đã xử lý

| Mã | Nội dung | Xử lý |
|---|---|---|
| KI-01 | DB dev đã áp 14 migration cũ không áp được `InitialClassFund` | Historical – DB dev tạo mới bằng `docker-compose.yml` |
| KI-09 | Chưa chạy API thủ công bằng `dotnet run` | 2026-11-05: chạy API với `app_user` + Swagger, register / login / me / logout đúng |
| KI-10 | Chưa tạo `app_user` / `migrator_user` và phân quyền DB: khi dùng một tài khoản có DDL, trigger `TR_NoDelete`, `TR_AppendOnly` có thể bị tắt | `docker-compose.yml` + `docker/postgres/init/01-roles.sh` tạo hai tài khoản; kiểm chứng `app_user` không DELETE, không tắt trigger, không DDL, không đọc `__EFMigrationsHistory` |
| KI-11 | Chưa đăng nhập được: `NoIdentityStore` từ chối mọi phiên | `IdentityStore` (feature Auth, PR #5) |
| KI-12 | Kịch bản kiểm chứng migration trên PostgreSQL thật chạy tạm, chưa có project integration test trong repo | `tests/ClassFund.IntegrationTests` (Testcontainers PostgreSQL 16) |
| KI-13 | Chưa có API thêm học sinh, cấp tài khoản, phân công thủ quỹ: test thủ công trên Swagger phải tạo dữ liệu bằng SQL | 2026-11-06 đã có API (bản trước). 2026-11-09 (D-39): API đã bỏ; test của module dùng `TestData` + `SignInAsAsync` (integration) và `RealApi` (e2e full) – không cần SQL tay |
