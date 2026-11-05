# Constraints & Indexes

> Phiên bản 2.0 · 2026-11-05. Dòng **v1.1 / v1.2** về `FundMember` và `CK_FundTransaction_CancelledBy` là thiết kế **Not Implemented** (giữ để lưu lịch sử). Phần "2.0" là thiết kế hiện hành (migration dự kiến `TeacherStudentParent`). Trạng thái triển khai: **Not Implemented** (mã nguồn đã xóa ngày 2026-11-05).

| Tên | Bảng | Nội dung | Mục đích |
|---|---|---|---|
| `UQ_User_NormalizedEmail` | User | UNIQUE | Email duy nhất |
| ~~`UQ_Fund_OwnerUserId`~~ | Fund | UNIQUE | *(Bỏ 2.0 – một giáo viên nhiều quỹ; thay bằng `IX_Fund_OwnerUserId`)* |
| `UQ_FundMember_UserId` | FundMember | UNIQUE (UserId) | v1.1 · 1 tài khoản – 1 quỹ – 1 vai trò — **Not Implemented** |
| `IX_FundMember_FundId_Role` | FundMember | (FundId, Role) | v1.1 — **Not Implemented** |
| `CK_FundMember_Role` | FundMember | IN (Treasurer, HomeroomTeacher, Parent) | v1.1 — **Not Implemented** |
| `AK_Category_Id_FundId_Type` | Category | Khóa phụ | Đích của FK ghép |
| `UQ_Category_Fund_Type_Name` | Category | UNIQUE (FundId, Type, lower(Name)) | Không trùng tên danh mục |
| `FK_FundTransaction_Category_SameFundAndType` | FundTransaction | FK (CategoryId, FundId, Type) | Giao dịch chỉ dùng danh mục cùng quỹ, cùng loại |
| `CK_FundTransaction_AmountPositive` | FundTransaction | Amount > 0 | BR-01 |
| `CK_FundTransaction_CancelledTrace` | FundTransaction | IsCancelled ⇔ CancelledAt có giá trị | Nhất quán trạng thái hủy |
| `CK_FundTransaction_CancelledBy` | FundTransaction | NOT IsCancelled OR CancelledBy IS NOT NULL – tạo **`NOT VALID`** | v1.2 — **Not Implemented** (domain bắt buộc người hủy; dữ liệu cũ có thể null) |
| FK `UpdatedBy`, `CancelledBy` → User | FundTransaction | ON DELETE RESTRICT (theo quy ước `*By`) | v1.2 · truy vết người thực hiện |
| `UQ_FundTransaction_FundId_IdempotencyKey` | FundTransaction | UNIQUE | BR-12 chống lưu trùng |
| `IX_FundTransaction_Fund_OccurredOn` | FundTransaction | (FundId, OccurredOn) | Số dư, báo cáo tháng, lọc ngày |
| `IX_FundTransaction_Fund_CreatedAt` | FundTransaction | (FundId, CreatedAt) | Giao dịch gần đây |
| `DF IsPublic = true`, `DF MustChangePassword = false` | FundTransaction, User | DEFAULT | v1.1 · cột mới NOT NULL thêm được vào bảng có dữ liệu mà không cần ghi lại dữ liệu |
| `CK_<Bảng>_<Cột>` | mọi enum | IN (...) | Giá trị hợp lệ (gồm `CK_User_Kind`) |
| `IX_Fund_OwnerUserId` | Fund | (OwnerUserId) | 2.0 · danh sách quỹ của giáo viên |
| `UQ_User_Username` | User | UNIQUE (Username) | 2.0 · tên đăng nhập học sinh duy nhất |
| `CK_User_LoginName` | User | NormalizedEmail IS NOT NULL OR Username IS NOT NULL | 2.0 · luôn có cách đăng nhập |
| `AK_Student_Id_FundId` | Student | Khóa phụ (Id, FundId) | 2.0 · đích FK ghép của phân công |
| `UQ_Student_UserId` | Student | UNIQUE (UserId) | 2.0 · một tài khoản học sinh – một học sinh – một quỹ |
| `IX_Student_FundId` | Student | (FundId) | 2.0 · danh sách lớp |
| `UQ_ParentLink_Student_Active` | ParentLink | UNIQUE (StudentId) WHERE IsActive | 2.0 · v1 một phụ huynh / học sinh |
| `IX_ParentLink_ParentUserId` | ParentLink | (ParentUserId) | 2.0 · quỹ của phụ huynh |
| `CK_ParentLink_RemovedTrace` | ParentLink | IsActive = (RemovedAt IS NULL) | 2.0 · nhất quán trạng thái gỡ |
| `FK_TreasurerAssignment_Student_SameFund` | TreasurerAssignment | FK (StudentId, FundId) → Student(Id, FundId) | 2.0 · chỉ phân công học sinh cùng quỹ |
| `UQ_TreasurerAssignment_Student_Active` | TreasurerAssignment | UNIQUE (StudentId) WHERE RevokedAt IS NULL | 2.0 · không phân công trùng (kể cả gửi đồng thời) |
| `IX_TreasurerAssignment_FundId` | TreasurerAssignment | (FundId) | 2.0 · đếm thủ quỹ đang hiệu lực |
| `CK_TreasurerAssignment_RevokedTrace` | TreasurerAssignment | (RevokedAt IS NULL) = (RevokedBy IS NULL) | 2.0 · thu hồi luôn có người thực hiện |
| FK | mọi FK | `ON DELETE RESTRICT` | Không cascade |

## Trigger
| Trigger | Bảng | Hành vi |
|---|---|---|
| `TR_AppendOnly` | AuditLog | Chặn UPDATE / DELETE |
| `TR_NoDelete` | FundTransaction, Category, Fund, User; **2.0:** Student, ParentLink, TreasurerAssignment | Chặn DELETE (hủy / ẩn / khóa / thu hồi / gỡ thay cho xóa) |

`app_user` không có DDL nên không tắt được trigger; không sửa được `__EFMigrationsHistory`.

## Lịch sử thay đổi
| Ngày | Thay đổi |
|---|---|
| 2026-11-04 | v1.1 – `UQ_FundMember_UserId`, `IX_FundMember_FundId_Role`, `CK_FundMember_Role`, default cột mới, NoDelete cho FundMember |
| 2026-11-04 | v1.2 – `CK_FundTransaction_CancelledBy` (NOT VALID), FK `UpdatedBy` / `CancelledBy` |
| 2026-11-05 | 2.0 – bỏ `UQ_Fund_OwnerUserId`; thêm ràng buộc / index cho `User.Username`, `Student`, `ParentLink`, `TreasurerAssignment`; NoDelete 3 bảng mới; đánh dấu thiết kế `FundMember`, `CK_FundTransaction_CancelledBy` Not Implemented |
