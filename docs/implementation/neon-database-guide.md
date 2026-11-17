# Neon PostgreSQL – môi trường dùng chung & quy trình migration

> Áp dụng yêu cầu mục 10. Neon là database **dùng chung** của hai developer trong môi trường phát triển.
> Không tự ý thay đổi cấu trúc hoặc xóa dữ liệu trên database dùng chung.

## 1. Mô hình

```text
Dev A: VS Code · ClassFund.Api (local) · ClassFundUI (local) ─┐
                                                              ├──► Neon Cloud: 1 PostgreSQL database dùng chung
Dev B: VS Code · ClassFund.Api (local) · ClassFundUI (local) ─┘
```

- Backend mỗi người chạy local, kết nối Neon bằng **`app_user`**.
- **Integration test không chạy trên Neon** – dùng PostgreSQL tạm trong Docker (`PostgresFixture`, Testcontainers).

## 2. Tài khoản database

| Tài khoản | Dùng cho | Quyền |
|---|---|---|
| Owner Neon (quản trị) | Chỉ người quản trị: tạo role, backup/restore | Toàn quyền – **không** dùng cho ứng dụng |
| `migrator_user` | `dotnet ef database update` – chỉ người được phân công | Sở hữu schema `public` (DDL) |
| `app_user` | Backend khi chạy | `SELECT, INSERT, UPDATE` (không DELETE, không DDL) |

Tạo role một lần: `ClassFundAPI/database/neon-roles.sql` (người quản trị chạy trong Neon SQL Editor, thay mật khẩu).

## 3. Cấu hình kết nối (không commit)

```bash
cd ClassFundAPI/src/ClassFund.Api
dotnet user-secrets set "ConnectionStrings:App" "Host=<neon-host>;Database=<db>;Username=app_user;Password=<pw>;SSL Mode=Require"
dotnet user-secrets set "SECRET_KEY" "<chuỗi ngẫu nhiên ≥ 32 ký tự>"
```

`SECRET_KEY` (pepper băm mật khẩu) **phải giống nhau** giữa hai developer vì dùng chung bảng `User`; trao đổi qua kênh riêng, không qua Git.
Chuỗi migrator chỉ đặt bằng biến môi trường lúc chạy lệnh (xem mục 4). Mẫu biến: `ClassFundAPI/.env.example`.

## 3b. Neon Object Storage – lưu file thực tế (OQ-16)

Ảnh hóa đơn, bill chuyển khoản, ảnh đại diện, tài liệu lưu trên **Neon Object Storage** (S3-compatible, đang beta). DB chỉ giữ metadata (`Attachment`) và `StorageKey`.

| Điểm cần biết | Cách xử lý trong dự án |
|---|---|
| API tương thích S3 (một phần) | Adapter `NeonObjectStorage : IFileStorage` (`AWSSDK.S3` 4.1, checksum chỉ khi bắt buộc) chỉ dùng `PutObject`, `GetObject`, `DeleteObject`, `HeadObject` |
| Bắt buộc path-style addressing | Client S3 cấu hình `ServiceURL` = endpoint, `ForcePathStyle = true` |
| Bucket gắn theo **branch** Neon, mỗi branch một endpoint | Dùng endpoint của branch dùng chung (cùng branch với database). Branch tạo để backup/thử nghiệm có view storage riêng |
| Quyền truy cập bucket đổi qua Neon Console, không qua `PutBucketPolicy` | Bucket để **private**; file chỉ tải qua API ClassFund (kiểm quyền AZ-05), không phát URL công khai |
| Khóa truy cập (Access Key ID / Secret) | user-secrets / biến môi trường, không commit |

Cấu hình (user-secrets trên máy mỗi dev):

```bash
dotnet user-secrets set "Storage:Provider" "Neon"
dotnet user-secrets set "Storage:Endpoint" "<S3 endpoint của branch>"
dotnet user-secrets set "Storage:Bucket" "classfund-files"
dotnet user-secrets set "Storage:Region" "<region>"
dotnet user-secrets set "Storage:AccessKeyId" "<key id>"
dotnet user-secrets set "Storage:SecretAccessKey" "<secret>"
```

Adapter `NeonObjectStorage` (M5-T0) đã có: đặt `Storage:Provider = Neon` + đủ khóa ở trên là dùng; thiếu khóa thì ứng dụng báo lỗi ngay khi khởi động. Mặc định (không cấu hình) là `Local`. Test tự động dùng S3 client giả / `Local`, không gọi Neon.
Quy tắc: không xóa object hàng loạt trên bucket dùng chung; file chưa gắn quá 24h do job dọn của M5 xử lý.

## 4. Quy trình thay đổi cấu trúc DB (yêu cầu 10.7)

| Bước | Ai | Việc |
|---|---|---|
| 1 | Người đề xuất | Mô tả thay đổi (bảng/cột/index/constraint) trong PR nháp hoặc issue, cập nhật `docs/database/database-design.md` |
| 2 | Cả hai | Trao đổi & thống nhất |
| 3 | Người được phân công | Sửa Entity + Configuration, tạo migration: `dotnet ef migrations add <Tên> -p src/ClassFund.Infrastructure -s src/ClassFund.Infrastructure -o Persistence/Migrations` |
| 4 | Người được phân công | Kiểm tra: đọc file migration, `dotnet ef migrations script -p src/ClassFund.Infrastructure -s src/ClassFund.Infrastructure` xem SQL, chạy `dotnet test` (Testcontainers – cần Docker Desktop; `InitialSchemaTests` kiểm model khớp migration) |
| 5 | Người được phân công | Commit + push code **cùng** migration, mở PR |
| 6 | Người còn lại | Pull, review migration |
| 7 | Cả hai | Xác nhận sẵn sàng (comment trên PR) → merge |
| 8 | **Chỉ người được phân công** | Áp lên Neon (bên dưới) |
| 9 | Cả hai | Pull `main`, chạy ứng dụng kiểm tra |

Áp migration (bash):

```bash
cd ClassFundAPI
export ConnectionStrings__Migrator="Host=<neon-host>;Database=<db>;Username=migrator_user;Password=<pw>;SSL Mode=Require"
dotnet tool restore
dotnet ef database update -p src/ClassFund.Infrastructure -s src/ClassFund.Infrastructure
unset ConnectionStrings__Migrator
```

### Quy tắc bắt buộc

- Không sửa / xóa migration đã merge hoặc đã áp lên Neon – thay đổi tiếp bằng migration mới.
- Không để hai người chạy migration cùng lúc. Báo trong nhóm chat trước và sau khi chạy.
- Không chạy `database drop`, `migrations remove` sau khi đã áp, `TRUNCATE`, `DELETE` hàng loạt trên Neon.
- Migration đi kèm thay đổi model trong cùng PR; không merge model mà thiếu migration.

## 5. Seed

- Seed nằm trong code (`DatabaseSeeder`), idempotent, chỉ dữ liệu giả. Thay đổi seed phải trao đổi trước.
- Chạy: `dotnet run --project src/ClassFund.Api -- --seed` (Development). Ai chạy cũng được vì idempotent, nhưng báo nhóm.
- Phân biệt: tài khoản/quỹ có hậu tố `.demo@classfund.local` / "(demo)" là seed; còn lại là dữ liệu phát sinh khi dùng thử.

## 6. Backup & sự cố

- Dùng Point-in-time restore / branch của Neon theo gói đang dùng. Trước migration lớn: tạo **Neon branch** làm điểm khôi phục.
- Khi sự cố: dừng ghi, báo nhóm, người quản trị khôi phục từ branch/PITR, ghi lại vào `docs/implementation/implementation-status.md` (mục Sự cố).
- Không dùng dữ liệu cá nhân thật trong môi trường phát triển. Chỉ thành viên dự án có quyền truy cập Neon.
