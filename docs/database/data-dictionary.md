# Data Dictionary

> Phiên bản: 2.0 · 2026-11-05 – mô hình Giáo viên · Học sinh · Phụ huynh. Migration: `InitialClassFund` (baseline) + `TeacherStudentParent` (bổ sung, giữ dữ liệu). Trạng thái triển khai: **Not Implemented** cho mọi bảng (mã nguồn và migration đã xóa ngày 2026-11-05 – xem [implementation-status.md](../implementation/implementation-status.md)); thiết kế giữ nguyên. Nhãn "Thiết kế hiện hành" = dùng cho lần triển khai tới; "Không dùng" = thiết kế cũ đã bỏ.

Quy ước: PascalCase; khóa chính UUID v7 (sinh ở ứng dụng), riêng `AuditLog` dùng `bigint identity`; thời điểm `timestamptz` (UTC); ngày nghiệp vụ `date`; enum `varchar(30)` + CHECK `CK_<Bảng>_<Cột>`; cột kết thúc `By` / `UserId` là FK → `User` (Restrict).

## User — Thiết kế hiện hành
| Cột | Kiểu | Null | Ghi chú |
|---|---|---|---|
| Id | uuid | NN | PK |
| Kind | varchar(30) | NN | **2.0** `Teacher`, `Student`, `Parent` (CK_User_Kind). Dữ liệu cũ → `Teacher` (D-30) |
| Email | varchar(254) | N | **2.0 cho phép null** (học sinh không bắt buộc email) |
| NormalizedEmail | varchar(254) | N | UNIQUE `UQ_User_NormalizedEmail` (chữ thường; nhiều NULL hợp lệ) |
| Username | varchar(60) | N | **2.0** tên đăng nhập học sinh (chữ thường), UNIQUE `UQ_User_Username` |
| FullName | varchar(200) | NN | |
| PasswordHash | varchar(500) | NN | PBKDF2 (ASP.NET Core PasswordHasher) – không bao giờ lưu mật khẩu rõ |
| Status | varchar(30) | NN | `Active`, `Disabled` |
| MustChangePassword | boolean | NN | **2.0** mặc định `false`; `true` khi dùng mật khẩu tạm (học sinh / phụ huynh mới, cấp lại) |
| SecurityStamp | varchar(64) | NN | Đổi khi đổi / đặt lại mật khẩu, khóa → vô hiệu phiên cũ |
| LastLoginAt, CreatedAt, UpdatedAt | timestamptz | N / NN / N | |

CHECK `CK_User_LoginName`: `NormalizedEmail IS NOT NULL OR Username IS NOT NULL`.

## Fund — Thiết kế hiện hành
| Cột | Kiểu | Null | Ghi chú |
|---|---|---|---|
| Id | uuid | NN | PK |
| OwnerUserId | uuid → User | NN | **GVCN** phụ trách. **2.0:** bỏ UNIQUE (một giáo viên nhiều quỹ), index thường `IX_Fund_OwnerUserId` |
| Name | varchar(100) | NN | |
| CreatedAt, UpdatedAt | timestamptz | NN / N | |

## Student — Thiết kế hiện hành (2.0)
| Cột | Kiểu | Null | Ghi chú |
|---|---|---|---|
| Id | uuid | NN | PK; AK `(Id, FundId)` cho FK ghép |
| FundId | uuid → Fund | NN | Học sinh thuộc đúng một quỹ; `IX_Student_FundId` |
| FullName | varchar(100) | NN | |
| UserId | uuid → User | N | Tài khoản học sinh (null = chưa cấp); UNIQUE `UQ_Student_UserId` |
| CreatedBy | uuid → User | NN | GVCN thêm |
| CreatedAt, UpdatedAt | timestamptz | NN / N | |

Trigger `TR_NoDelete` – không xóa cứng.

## ParentLink — Thiết kế hiện hành (2.0)
| Cột | Kiểu | Null | Ghi chú |
|---|---|---|---|
| Id | uuid | NN | PK |
| StudentId | uuid → Student | NN | UNIQUE lọc `UQ_ParentLink_Student_Active` (`WHERE "IsActive"`) – v1 một phụ huynh / học sinh |
| ParentUserId | uuid → User | NN | Tài khoản phụ huynh; `IX_ParentLink_ParentUserId` |
| IsActive | boolean | NN | CHECK `CK_ParentLink_RemovedTrace`: `IsActive = (RemovedAt IS NULL)` |
| CreatedBy, RemovedBy | uuid → User | NN / N | Người liên kết / gỡ |
| CreatedAt, RemovedAt | timestamptz | NN / N | |

Trigger `TR_NoDelete`.

## TreasurerAssignment — Thiết kế hiện hành (2.0)
| Cột | Kiểu | Null | Ghi chú |
|---|---|---|---|
| Id | uuid | NN | PK |
| FundId | uuid → Fund | NN | `IX_TreasurerAssignment_FundId` |
| StudentId | uuid | NN | FK ghép `(StudentId, FundId)` → `Student(Id, FundId)` `FK_TreasurerAssignment_Student_SameFund` – học sinh phải cùng quỹ. UNIQUE lọc `UQ_TreasurerAssignment_Student_Active` (`WHERE "RevokedAt" IS NULL`) |
| AssignedBy, RevokedBy | uuid → User | NN / N | GVCN phân công / thu hồi |
| AssignedAt, RevokedAt | timestamptz | NN / N | CHECK `CK_TreasurerAssignment_RevokedTrace`: `(RevokedAt IS NULL) = (RevokedBy IS NULL)` |

Trigger `TR_NoDelete`. "Thủ quỹ cuối cùng" kiểm tra ở Application (khóa dòng `Fund` `FOR UPDATE`).

## Category — Thiết kế hiện hành
| Cột | Kiểu | Null | Ghi chú |
|---|---|---|---|
| Id | uuid | NN | PK |
| FundId | uuid → Fund | NN | |
| Type | varchar(30) | NN | `Income`, `Expense` |
| Name | varchar(100) | NN | UNIQUE (FundId, Type, lower(Name)) |
| IsHidden | boolean | NN | Ẩn thay cho xóa |
| CreatedAt, UpdatedAt | timestamptz | NN / N | |

## FundTransaction — Thiết kế hiện hành
| Cột | Kiểu | Null | Ghi chú |
|---|---|---|---|
| Id | uuid | NN | PK |
| FundId | uuid → Fund | NN | |
| Type | varchar(30) | NN | `Income`, `Expense` |
| Amount | bigint | NN | > 0 (đồng) |
| OccurredOn | date | NN | |
| CategoryId | uuid | NN | FK ghép (CategoryId, FundId, Type) → Category |
| Description | varchar(200) | NN | |
| Note | varchar(1000) | N | Ghi chú nội bộ – không trả cho phụ huynh / học sinh chưa phân công |
| IsPublic | boolean | NN | **2.0** mặc định `true`; dữ liệu cũ = công khai (D-09) |
| IsCancelled, CancelledAt | boolean, timestamptz | NN / N | |
| CancelReason | varchar(500) | N | Bắt buộc khi hủy mới (API + domain); dữ liệu cũ có thể null |
| CreatedBy | uuid → User | NN | |
| UpdatedBy | uuid → User | N | **2.0** người sửa gần nhất; dữ liệu cũ điền từ AuditLog nếu có, không suy diễn |
| CancelledBy | uuid → User | N | **2.0** người hủy; dữ liệu cũ điền từ AuditLog nếu có. *CHECK `NOT VALID` bắt buộc với khoản hủy mới (thiết kế v1.2): không dùng – domain bảo đảm* |
| IdempotencyKey | uuid | NN | UNIQUE (FundId, IdempotencyKey) |
| CreatedAt, UpdatedAt | timestamptz | NN / N | |

## AuditLog (append-only) — Thiết kế hiện hành
| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | bigint identity | PK |
| OccurredAt | timestamptz | |
| ActorUserId | uuid → User | Lấy từ phiên đăng nhập, không từ request |
| Category | varchar(30) | `Change`, `Security` |
| Action | varchar(100) | `Account.RegisterTeacher`, `Account.UpdateProfile`, `Auth.SignIn`, `Auth.SignInFailed`, `Auth.PasswordChanged`, `Fund.Create`, `Fund.Rename`, `Student.Create`, `Student.Update`, `Student.IssueAccount`, `Student.ResetPassword`, `Parent.CreateAccount`, `ParentLink.Create`, `ParentLink.Remove`, `Treasurer.Assign`, `Treasurer.Revoke`, `Income/Expense.Create/Update/Cancel`, `Category.Create/Update` – **không** chứa mật khẩu |
| EntityType, EntityId | varchar | |
| OldValues, NewValues | jsonb | |
| CorrelationId | varchar(64) | |
| IpAddress | inet | |

## FundMember — Không dùng (thay bằng Student / ParentLink / TreasurerAssignment)
Thiết kế v1.1 / v1.2 (một tài khoản – một quỹ, cột `Role`, `UpdatedBy`) không được tạo. Bỏ theo D-26, D-31.

## Lịch sử thay đổi
| Ngày | Thay đổi |
|---|---|
| 2026-11-04 | v1.1 – `FundMember`, `IsPublic`, `MustChangePassword` (thiết kế) |
| 2026-11-04 | v1.2 – `FundTransaction.UpdatedBy`, `CancelledBy`; `FundMember.UpdatedBy`; `CancelReason` bắt buộc khi hủy mới (thiết kế) |
| 2026-11-05 | 2.0 – triển khai `TeacherStudentParent`: `User.Kind / Username / MustChangePassword`, email nullable; `Fund.OwnerUserId` bỏ UNIQUE; `Student`, `ParentLink`, `TreasurerAssignment`; `FundTransaction.IsPublic / UpdatedBy / CancelledBy`; `FundMember` không triển khai |
| 2026-11-05 | Mã nguồn / migration đã xóa – trạng thái Not Implemented; thiết kế 2.0 giữ nguyên |
