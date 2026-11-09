# Database Design Decisions

> 2026-11-09: schema thuộc Foundation (D-39) – module dùng lại, không tạo lại. 2026-11-05: Schema **Implemented** theo DD-17 → DD-20, DD-22 → DD-24; DD-21 không triển khai. DD-09, DD-13, DD-16 (thiết kế `FundMember` / `AddFundMembers`) **không triển khai** – thay bằng DD-17 → DD-22. DD-14 phần CHECK `NOT VALID` không triển khai.

| Mã | Quyết định | Lý do | Phương án đã bỏ |
|---|---|---|---|
| DD-01 | Một bảng `FundTransaction` cho cả thu và chi, cột `Type` | Một lịch sử chung, một công thức số dư, ít bảng | Bảng riêng cho thu / chi; phiếu thu + phiếu chi + phân bổ |
| DD-02 | Không lưu số dư; tính từ giao dịch | Không thể lệch; đơn giản | Sổ cái + số dư cache + khóa dòng (thiết kế cũ) |
| DD-03 | Hủy = cờ, không xóa; trigger chặn DELETE | Giữ lịch sử, sửa nhầm được | Xóa cứng; đảo giao dịch bằng chứng từ riêng |
| DD-04 | FK ghép tới (Category.Id, FundId, Type) | DB tự chặn dùng danh mục sai loại / sai quỹ | Chỉ kiểm tra ở ứng dụng |
| DD-05 | Khóa chống lưu trùng do client sinh, UNIQUE theo quỹ | Nhấn Lưu nhiều lần không tạo trùng | Không chống trùng |
| DD-06 | Không dùng optimistic concurrency cho sửa giao dịch | Thường chỉ một người ghi; thêm trường phiên bản làm phức tạp API. **v1.1:** có thể có nhiều thủ quỹ – hai người sửa cùng một khoản cùng lúc thì lần ghi sau thắng, nhật ký vẫn ghi đủ cả hai; xem lại nếu xảy ra thực tế | `xmin` + gửi phiên bản từ client |
| DD-07 | Migration baseline mới `InitialClassFund` (xóa 14 migration cũ chưa phát hành) | Quyết định PO D-08 | Migration DROP 35 bảng |
| DD-08 | Không có bảng vai trò / permission | Vai trò cố định (2.0: GVCN / Thủ quỹ / Học sinh / Phụ huynh, suy ra từ dữ liệu – DD-17); bảng vai trò → permission là hằng số trong Domain | Bảng Role / Permission / RolePermission |
| DD-09 | *(Not Implemented – thay bằng DD-17)* Bảng `FundMember` (UserId UNIQUE, Role) thay cho quan hệ 1-1 `Fund.OwnerUserId` khi phân quyền | Nhiều tài khoản / quỹ với vai trò khác nhau; giữ ràng buộc 1 tài khoản – 1 quỹ ở DB; sau này mở rộng nhiều quỹ chỉ cần bỏ UNIQUE | Thêm `FundId` + `Role` vào `User` (trộn danh tính với thành viên); bảng Role riêng |
| DD-10 | Giữ cột `Fund.OwnerUserId` (không xóa) *(2.0: nghĩa là GVCN phụ trách quỹ)* | Migration không phá dữ liệu; vẫn có ích để biết ai tạo quỹ | Xóa cột (migration phá hủy) |
| DD-11 | Cờ `FundTransaction.IsPublic` mặc định `true` | D-09; dữ liệu cũ hiển thị cho phụ huynh như trước khi có phụ huynh (chưa có ai xem) | Bảng công bố theo tháng |
| DD-12 | `User.MustChangePassword` | Buộc đổi mật khẩu tạm (D-10) mà không cần bảng token / lời mời | Bảng lời mời có hạn |
| DD-14 | `UpdatedBy` / `CancelledBy` là cột nullable; CHECK cho `CancelledBy` tạo `NOT VALID` | D-19: truy vết trực tiếp, không phải dò `AuditLog` mỗi lần hiển thị; dữ liệu cũ có thể thiếu người hủy nên không ép ràng buộc ngược | Chỉ dựa vào `AuditLog` (truy vấn phức tạp); NOT NULL + điền giả (sai sự thật) |
| DD-15 | Điền `UpdatedBy` / `CancelledBy` cho dữ liệu cũ bằng `AuditLog` (Action `*.Update` / `*.Cancel` mới nhất theo `EntityId`) | Đúng người thực hiện thật; không bịa dữ liệu; chỉ UPDATE cột mới (null → giá trị) | Bỏ trống toàn bộ |
| DD-16 | *(Not Implemented – thay bằng DD-19; nguồn "D-11" ở đây chỉ đúng phần "không mất thủ quỹ cuối")* Đổi vai trò = UPDATE `FundMember.Role` + `UpdatedBy` + `AuditLog` (Old/New Role) trong transaction SERIALIZABLE có đếm thủ quỹ hoạt động | D-16, D-11; tránh 2 thủ quỹ hạ quyền nhau cùng lúc | Khóa dòng thủ công (`FOR UPDATE`) – bỏ vì port `IAppDb` đã bỏ khóa dòng, SERIALIZABLE + retry 40001 đã có |
| DD-13 | *(Not Implemented – thay bằng DD-21)* Migration bổ sung `AddFundMembers` (không đổi baseline) | Chỉ thêm bảng / cột / index / trigger, một câu INSERT sinh `FundMember(Treasurer)` cho mỗi `Fund` từ `OwnerUserId`, và (v1.2) UPDATE điền `UpdatedBy` / `CancelledBy` đang null từ `AuditLog`; không xóa / sửa giá trị có sẵn | Sửa lại baseline `InitialClassFund` (buộc tạo lại DB) |

| DD-17 | Vai trò trong quỹ **suy ra** từ quan hệ (`Fund.OwnerUserId`, `Student.UserId`, `TreasurerAssignment`, `ParentLink`), không có bảng thành viên / cột vai trò | Một nguồn sự thật; không thể lệch giữa "vai trò" và dữ liệu thật; phụ huynh không bao giờ trùng tài khoản học sinh | `FundMember(Role)` (v1.2) |
| DD-18 | `User.Kind` cố định + `Username` nullable UNIQUE, `Email` nullable | Học sinh không cần email; giáo viên / phụ huynh đăng nhập bằng email; CHECK bảo đảm luôn có tên đăng nhập | Bảng tài khoản riêng cho từng loại |
| DD-19 | `TreasurerAssignment` có `RevokedAt/RevokedBy`, UNIQUE lọc theo phân công đang hiệu lực, FK ghép `(StudentId, FundId)` | Lịch sử đầy đủ; DB chặn phân công trùng và sai quỹ; thủ quỹ cuối cùng kiểm tra ở Application với khóa dòng `Fund` (`FOR UPDATE` qua `IAppDb.LockAsync`) | Cờ `IsTreasurer` trên Student (mất lịch sử); SERIALIZABLE |
| DD-20 | `ParentLink.IsActive` + UNIQUE lọc `(StudentId) WHERE IsActive` | v1 một phụ huynh / học sinh nhưng giữ lịch sử gỡ / đổi | Cột `ParentUserId` trên Student |
| DD-21 | *(Không triển khai – thay bằng DD-23)* Migration bổ sung `TeacherStudentParent` | Chỉ thêm cột (có default) / bảng / index / trigger; đổi `Email` thành nullable; bỏ UNIQUE `OwnerUserId` (không mất dữ liệu); backfill `UpdatedBy` / `CancelledBy` từ `AuditLog`, không suy diễn; mọi tài khoản cũ → `Teacher`; không tạo phân công thủ quỹ (D-30) | Drop / tạo lại bảng; giữ người lớn làm thủ quỹ |
| DD-22 | Không xóa cứng `Student`, `ParentLink`, `TreasurerAssignment` (trigger) | Lịch sử phân công / liên kết là dữ liệu kiểm toán | Xóa mềm bằng cột `DeletedAt` |
| DD-23 | Gộp toàn bộ schema 2.0 vào baseline `InitialClassFund`, không có `TeacherStudentParent` | Mã nguồn và migration cũ đã xóa, chưa có DB nào cần nâng cấp nên không cần bước chuyển dữ liệu (D-36) | Tạo lại hai bước để mô phỏng lịch sử nâng cấp |
| DD-24 | Migration riêng `AddDatabaseProtections` cho phần SQL viết tay (trigger, index theo biểu thức) | Sinh lại baseline không làm mất phần viết tay; có `Down()` đầy đủ | Thêm SQL vào cuối baseline |

## Lưu ý vận hành
*(Không áp dụng – DD-23)* **2.0:** `TeacherStudentParent` phải áp được lên DB đang có dữ liệu `InitialClassFund` (*historical*: đã kiểm chứng bằng `UpgradeMigrationTests` trước khi mã nguồn bị xóa 2026-11-05; cần kiểm chứng lại khi triển khai). `Down()` chỉ dành cho dev: thất bại nếu đã có tài khoản học sinh (email null) – không ép xóa dữ liệu.

*(v1.2, không triển khai)* `AddFundMembers` áp được lên DB đang có dữ liệu `InitialClassFund`. `Down()` xóa bảng / cột mới → mất dữ liệu thành viên và cờ công khai; chỉ dùng trên môi trường dev.

DB dev đã áp migration cũ phải tạo lại (ví dụ `docker compose down -v` rồi `up`) trước khi áp `InitialClassFund` – thao tác xóa dữ liệu, người vận hành tự thực hiện.
