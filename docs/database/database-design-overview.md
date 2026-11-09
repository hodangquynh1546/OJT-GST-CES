# Database Design – Tổng quan

> Phiên bản: 2.0 · 2026-11-05 (mô hình Giáo viên · Học sinh · Phụ huynh; migration `InitialClassFund` + `AddDatabaseProtections` – **Implemented trong Foundation**; module **không** tạo lại bảng, chỉ thêm migration khi bắt buộc – [api-conventions.md](../api/api-conventions.md) mục 9) · 1.2 · 2026-11-04 (thiết kế `AddFundMembers` – không triển khai) · PostgreSQL 16 · EF Core 10.0.4 · Npgsql 10.0.3. Thay thế thiết kế 37 bảng của phạm vi cũ (Git history `f6cecab`).

## 1. Bộ tài liệu
[erd.md](erd.md) · [data-dictionary.md](data-dictionary.md) · [database-constraints-and-indexes.md](database-constraints-and-indexes.md) · [financial-data-design.md](financial-data-design.md) · [database-workflows.md](database-workflows.md) · [database-design-decisions.md](database-design-decisions.md)

## 2. Bảng

| Bảng | Mục đích |
|---|---|
| `User` | Tài khoản đăng nhập: giáo viên, học sinh, phụ huynh (`Kind`) |
| `Fund` | Quỹ lớp; `OwnerUserId` = GVCN (một giáo viên nhiều quỹ) |
| `Student` | Học sinh của quỹ (2.0), có thể có tài khoản |
| `ParentLink` | Liên kết phụ huynh – học sinh (2.0) |
| `TreasurerAssignment` | Phân công / thu hồi thủ quỹ (2.0) |
| ~~`FundMember`~~ | *(v1.1 – không triển khai)* |
| `Category` | Danh mục thu / chi của quỹ |
| `FundTransaction` | Khoản thu / chi, kèm cờ công khai |
| `AuditLog` | Nhật ký thêm / sửa / hủy, đăng nhập (append-only) |

## 3. Nguyên tắc
1. **Số dư không lưu** – tính từ giao dịch chưa hủy (BR-02) → không có dữ liệu tổng hợp có thể lệch.
2. Tiền `bigint` (đồng), luôn > 0; chiều tăng / giảm do `Type`.
3. Không xóa cứng: hủy giao dịch = cờ `IsCancelled`; danh mục = cờ `IsHidden` (trigger chặn DELETE).
4. Ràng buộc quan trọng nằm ở DB: số tiền > 0, giao dịch tham chiếu danh mục **cùng quỹ, cùng loại**, chống lưu trùng (UNIQUE khóa idempotency).
5. Ứng dụng kết nối bằng `app_user` (chỉ DML); migration bằng `migrator_user`.
6. *(Sửa 2.0)* Vai trò trong quỹ suy ra từ `Fund.OwnerUserId` / `Student` / `TreasurerAssignment` / `ParentLink` của phiên đăng nhập; phạm vi công khai (`IsPublic`, trạng thái theo bộ lọc – D-18) áp ngay trong truy vấn.
