# Thao tác dữ liệu

> 2.0 · 2026-11-05: dòng "(v1.1 / v1.2)" về thành viên là thiết kế cũ **không dùng**; các dòng "2.0" là thiết kế hiện hành. Schema đã có; các thao tác dưới đây **Not Implemented**, làm cùng từng feature. Đoạn "Đồng thời" cuối file về SERIALIZABLE là thiết kế v1.2; thiết kế hiện hành dùng khóa dòng `Fund` (DD-19, TD-85).

| Thao tác | Ghi DB | Ghi chú |
|---|---|---|
| Thêm khoản thu / chi | INSERT `FundTransaction` + `AuditLog` (một lần lưu) | Trùng khóa idempotency → trả lại giao dịch đã lưu (không INSERT lần hai) |
| Sửa | UPDATE `FundTransaction` (+ `UpdatedBy`, v1.2) + INSERT `AuditLog` (cũ / mới) | Không cho sửa khi đã hủy |
| Hủy | UPDATE `IsCancelled`, `CancelledAt`, `CancelReason` (bắt buộc), `CancelledBy` (v1.2) + `AuditLog` | Không DELETE |
| Danh mục | INSERT / UPDATE `Category` + `AuditLog` | Ẩn = `IsHidden` |
| ~~Tạo quỹ + thủ quỹ đầu tiên~~ | *(v1.2, thay bằng 2 dòng dưới)* | Lệnh `create-treasurer` (đã bỏ) |
| Đăng ký giáo viên (2.0) | Transaction: kiểm tra email; INSERT `User(Kind=Teacher)` + `AuditLog Account.RegisterTeacher` | Email trùng → 422 `user.email_taken` |
| Tạo quỹ (2.0) | INSERT `Fund(OwnerUserId = người đăng nhập)` + 6 `Category` mặc định + `AuditLog Fund.Create` | `create-teacher` có `SCHOOLFUND_FUND_NAME` cũng tạo quỹ |
| Thêm học sinh (2.0) | Transaction: INSERT `Student` + `AuditLog`; có email phụ huynh → như dòng liên kết | |
| Cấp tài khoản học sinh (2.0) | Transaction: chọn username duy nhất; INSERT `User(Kind=Student, MustChangePassword)`; UPDATE `Student.UserId` + `AuditLog` (không có mật khẩu) | Đã có → 422 `student.account_exists`; trùng username → 422 / 409 |
| Cấp lại mật khẩu học sinh (2.0) | UPDATE `User` (PasswordHash, MustChangePassword = true, SecurityStamp mới) + `AuditLog Student.ResetPassword` | |
| Liên kết phụ huynh (2.0) | Transaction: kiểm tra liên kết đang hiệu lực; tìm `User` theo email (Parent → dùng lại; loại khác → 422 `parent.email_in_use`; không có → INSERT `User(Kind=Parent)`); INSERT `ParentLink` + `AuditLog` | UNIQUE lọc chặn hai liên kết đồng thời |
| Gỡ liên kết (2.0) | UPDATE `ParentLink` (IsActive = false, RemovedBy, RemovedAt) + `AuditLog` | Không DELETE |
| Phân công thủ quỹ (2.0) | Transaction: `SELECT … FROM "Fund" … FOR UPDATE`; kiểm tra học sinh cùng quỹ, tài khoản Active + đã đổi mật khẩu, chưa là thủ quỹ; INSERT `TreasurerAssignment` + `AuditLog` | UNIQUE lọc chặn trùng |
| Thu hồi thủ quỹ (2.0) | Transaction: khóa dòng `Fund`; đếm phân công đang hiệu lực > 1; UPDATE `RevokedAt`, `RevokedBy` + `AuditLog` | Cuối cùng → 422 `treasurer.last`; đã thu hồi → 409 |
| Thêm thành viên (v1.1) | Một transaction: `User` (MustChangePassword = true), `FundMember`, `AuditLog` (không ghi mật khẩu) | Email trùng → 409 `member.email_taken` |
| Đặt lại mật khẩu thành viên | UPDATE `User` (PasswordHash, MustChangePassword = true, SecurityStamp mới) + `AuditLog` | Thành viên cùng quỹ, không phải chính mình |
| Khóa / mở thành viên | UPDATE `User.Status` (+ SecurityStamp mới khi khóa) + `AuditLog` | Khóa thủ quỹ: đếm thủ quỹ đang hoạt động trong cùng transaction, phải còn ≥ 1 |
| Đổi vai trò (v1.2) | Transaction SERIALIZABLE: đếm thủ quỹ Active; UPDATE `FundMember.Role`, `UpdatedBy`, `UpdatedAt` + `AuditLog` (vai trò cũ / mới) | Chính mình → 422 `member.self_action`; mất thủ quỹ cuối → 422 `member.last_treasurer` |
| Đổi mật khẩu | UPDATE `User` (PasswordHash, MustChangePassword = false, SecurityStamp mới) + `AuditLog` | |

**Đồng thời:** không có số dư lưu sẵn nên không cần khóa dòng; hai lần "Lưu" cùng khóa được UNIQUE chặn ở DB. Hai thao tác sửa cùng lúc trên một giao dịch: lần ghi sau thắng (DD-06). Hai thủ quỹ cùng khóa nhau cùng lúc: transaction SERIALIZABLE (đã có retry 40001) đảm bảo vẫn còn ≥ 1 thủ quỹ hoạt động.
