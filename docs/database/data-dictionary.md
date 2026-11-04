# Data Dictionary

> Phiên bản: 0.1 (bản nháp, chờ duyệt) · Ngày: 2026-11-03
> Ràng buộc chi tiết (tên constraint, điều kiện partial index, trigger): [database-constraints-and-indexes.md](database-constraints-and-indexes.md). Trạng thái: [database-workflows.md](database-workflows.md).

## 0. Quy ước

| Quy ước | Nội dung |
|---|---|
| Tên bảng / cột | PascalCase, số ít, trùng tên entity (mặc định EF Core, không cần thêm package naming convention ngoài Tech Stack) |
| Khóa chính | `Id uuid` (giá trị sinh ở ứng dụng bằng UUID v7 – có thứ tự thời gian). Riêng sổ cái và log append-only dùng `Id bigint GENERATED ALWAYS AS IDENTITY` để có thứ tự tuyệt đối |
| Tiền | `bigint`, đơn vị **đồng** (VND không có đơn vị lẻ). Xem DD-06 |
| Thời điểm | `timestamptz` (lưu UTC). Ngày nghiệp vụ (ngày nộp, ngày chi): `date` |
| Trạng thái / loại | `varchar(30)` + `CHECK (... IN (...))`; EF Core map enum bằng `HasConversion<string>()` |
| Concurrency | Cột hệ thống `xmin` của PostgreSQL map làm row version (`IsRowVersion()` trong Npgsql) cho các bảng ghi chú **[xmin]**; không tạo cột vật lý |
| NN / N | NN = `NOT NULL`, N = nullable |

### 0.1 Nhóm cột chuẩn

| Ký hiệu | Cột | Kiểu | Null | Default | Ý nghĩa |
|---|---|---|---|---|---|
| **[Audit]** | `CreatedAt` | timestamptz | NN | `now()` | Thời điểm tạo |
| | `CreatedBy` | uuid → `User` | NN | | Người tạo (thao tác tự động dùng tài khoản hệ thống `User.IsSystem = true`) |
| | `UpdatedAt` | timestamptz | N | | Lần sửa gần nhất |
| | `UpdatedBy` | uuid → `User` | N | | Người sửa gần nhất |
| **[Approve]** | `ApprovedAt`, `ApprovedBy` | timestamptz, uuid → `User` | N | | Duyệt |
| | `RejectedAt`, `RejectedBy`, `RejectReason` | timestamptz, uuid, varchar(500) | N | | Từ chối (lý do bắt buộc khi từ chối) |
| **[Cancel]** | `CancelledAt`, `CancelledBy`, `CancelReason` | timestamptz, uuid, varchar(500) | N | | Hủy khi còn chờ (người tạo) hoặc theo quy trình hủy riêng |
| **[Idem]** | `IdempotencyKey` | uuid | NN | | Khóa chống gửi trùng do client sinh cho mỗi lần thao tác; `UNIQUE` |

Lịch sử nhiều vòng (bị từ chối → gửi lại) lưu ở `DocumentStatusHistory`; các cột [Approve]/[Cancel] chỉ giữ lần gần nhất.

---

## A. Identity & Authorization

### A1. `User` – Tài khoản đăng nhập
Mọi người dùng (Quản trị, GVCN, phụ huynh – kể cả thủ quỹ, người giữ quỹ) và một tài khoản hệ thống.

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `Email` | varchar(254) | NN | | Email hiển thị |
| `NormalizedEmail` | varchar(254) | NN | | Email viết thường, đã trim; **UNIQUE** (BR-ACC-02) |
| `FullName` | varchar(200) | NN | | |
| `PhoneNumber` | varchar(20) | N | | |
| `PasswordHash` | varchar(500) | N | | Null cho đến khi nhận lời mời; tài khoản hệ thống luôn null |
| `Status` | varchar(30) | NN | `'Invited'` | `Invited`, `Active`, `Locked`, `Disabled` |
| `IsSystem` | boolean | NN | `false` | Tài khoản hệ thống dùng cho thao tác tự động (tự trừ số dư, …) |
| `LastLoginAt` | timestamptz | N | | |
| [Audit] | | | | Riêng `CreatedBy` **N** (tài khoản đầu tiên / seed) |

### A2. `Role` – Vai trò (nhóm permission mặc định)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `Code` | varchar(50) | NN | | **UNIQUE**. Giá trị seed: `Admin`, `HomeroomTeacher`, `Treasurer`, `FundCustodian`, `Parent` |
| `Name` | varchar(100) | NN | | Tên hiển thị tiếng Việt |
| `ScopeType` | varchar(30) | NN | | `School` (Admin) · `Class` (HomeroomTeacher, Treasurer, FundCustodian – gán qua `ClassAssignment`) · `Student` (Parent – phạm vi qua `ParentStudent`) |

### A3. `Permission`

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `Code` | varchar(100) | NN | | **UNIQUE**; đúng tên trong [roles-and-permissions.md](../business-analysis/roles-and-permissions.md), ví dụ `Expense.Approve`. Dùng trong `[RequirePermission]` |
| `Description` | varchar(300) | NN | | |

### A4. `RolePermission` – Permission mặc định của vai trò

| Cột | Kiểu | Null | Ghi chú |
|---|---|---|---|
| `RoleId` | uuid → `Role` | NN | PK (cùng `PermissionId`) |
| `PermissionId` | uuid → `Permission` | NN | |

### A5. `UserRole` – Vai trò không gắn lớp (Admin, Parent)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `UserId` | uuid → `User` | NN | | |
| `RoleId` | uuid → `Role` | NN | | Chỉ vai trò `ScopeType` = `School` hoặc `Student` (kiểm tra ở ứng dụng) |
| `GrantedAt` | timestamptz | NN | `now()` | |
| `GrantedBy` | uuid → `User` | N | | Null với seed |
| `RevokedAt`, `RevokedBy` | timestamptz, uuid | N | | Thu hồi, không xóa dòng |

Unique: một dòng hiệu lực cho mỗi (`UserId`, `RoleId`) – partial unique `WHERE RevokedAt IS NULL`.

### A6. `UserPermissionGrant` – Permission cấp riêng (D-45, D-52)
Ví dụ: `Receipt.CollectFormerStudent`, `Report.Export`, `Report.ViewStudentIdentity` cho người giữ quỹ.

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `UserId` | uuid → `User` | NN | | |
| `PermissionId` | uuid → `Permission` | NN | | |
| `ClassId` | uuid → `Class` | N | | Phạm vi: lớp – năm học được giao; null = toàn trường |
| `Reason` | varchar(500) | N | | |
| `GrantedAt`, `GrantedBy` | timestamptz, uuid → `User` | NN | `now()` | |
| `RevokedAt`, `RevokedBy` | timestamptz, uuid | N | | |

Unique: (`UserId`, `PermissionId`, `ClassId`) `NULLS NOT DISTINCT` `WHERE RevokedAt IS NULL`.

### A7. `Invitation` – Lời mời qua email (D-03, BR-ACC-05)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `NormalizedEmail` | varchar(254) | NN | | Email được mời |
| `RoleId` | uuid → `Role` | NN | | `HomeroomTeacher` (Quản trị mời) hoặc `Parent` (GVCN mời) |
| `ClassId` | uuid → `Class` | N | | Bắt buộc khi mời GVCN |
| `StudentId` | uuid → `Student` | N | | Bắt buộc khi mời phụ huynh (BR-ACC-03) |
| `TokenHash` | varchar(128) | NN | | Băm của token trong liên kết; **UNIQUE**; không lưu token gốc |
| `Status` | varchar(30) | NN | `'Pending'` | `Pending`, `Accepted`, `Expired`, `Revoked` |
| `ExpiresAt` | timestamptz | NN | | Thời hạn (NFR-07) |
| `AcceptedAt` | timestamptz | N | | Dùng một lần: đã có giá trị thì không chấp nhận lại |
| `AcceptedUserId` | uuid → `User` | N | | |
| [Audit] | | | | `CreatedBy` = người mời |

---

## B. Academic

### B1. `SchoolYear` – Năm học (D-02)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `Code` | varchar(20) | NN | | Ví dụ `2026-2027`; **UNIQUE** |
| `StartDate`, `EndDate` | date | NN | | `CHECK (EndDate > StartDate)` |
| `Status` | varchar(30) | NN | `'Planned'` | `Planned`, `Active`, `Ended`. `Ended` = "năm học đã kết thúc" (dùng cho người giữ quỹ – D-45) |
| [Audit] | | | | |

### B2. `Class` – Lớp trong một năm học

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `SchoolYearId` | uuid → `SchoolYear` | NN | | |
| `Name` | varchar(50) | NN | | Ví dụ `6A`; **UNIQUE** (`SchoolYearId`, `Name`) |
| [Audit] | | | | |

"6A năm 2026-2027" và "7A năm 2027-2028" là hai dòng khác nhau; không có liên kết tự động giữa chúng (D-25).

### B3. `ClassAssignment` – Phân công người theo lớp (GVCN, Thủ quỹ, Người giữ quỹ)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `ClassId` | uuid → `Class` | NN | | Lớp – năm học được giao (D-45) |
| `UserId` | uuid → `User` | NN | | |
| `RoleId` | uuid → `Role` | NN | | `HomeroomTeacher`, `Treasurer` hoặc `FundCustodian` |
| `StartedAt` | timestamptz | NN | `now()` | |
| `EndedAt` | timestamptz | N | | Null = đang hiệu lực |
| `AssignedBy` | uuid → `User` | NN | | GVCN (thủ quỹ – D-05), Quản trị (GVCN – D-22, người giữ quỹ – D-37) |
| `EndedBy` | uuid → `User` | N | | |
| `Note` | varchar(500) | N | | |

Unique: mỗi lớp tối đa **một** người hiệu lực cho mỗi vai trò (BR-ACC-04, D-45) – partial unique (`ClassId`, `RoleId`) `WHERE EndedAt IS NULL`. "Không còn GVCN phụ trách" (D-50) = không có dòng `HomeroomTeacher` hiệu lực.

### B4. `Student`

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `StudentCode` | varchar(30) | NN | | Mã học sinh; **UNIQUE** |
| `FullName` | varchar(200) | NN | | |
| `DateOfBirth` | date | N | | |
| `Status` | varchar(30) | NN | `'Active'` | `Active`, `Left` (nghỉ học – D-14) |
| [Audit] | | | | |

### B5. `StudentClassEnrollment` – Lịch sử học sinh theo lớp (D-14, D-23, D-56)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `StudentId` | uuid → `Student` | NN | | |
| `ClassId` | uuid → `Class` | NN | | |
| `StartedOn` | date | NN | | |
| `EndedOn` | date | N | | Null = đang học lớp này |
| `EndReason` | varchar(30) | N | | `Transferred` (chuyển lớp giữa năm), `Promoted` (lên lớp), `Left` (nghỉ học), `YearEnded` |
| `EndedBy` | uuid → `User` | N | | Quản trị khi chuyển / lên lớp (thao tác này là **xác nhận** – D-56); GVCN khi nghỉ học |
| [Audit] | | | | `CreatedBy` = người đưa học sinh vào lớp |

Unique: mỗi học sinh tối đa **một** dòng đang hiệu lực – partial unique (`StudentId`) `WHERE EndedOn IS NULL`. Check: `EndedOn IS NULL OR EndedOn >= StartedOn`; `EndReason` có giá trị ⇔ `EndedOn` có giá trị.

### B6. `ParentStudent` – Quan hệ phụ huynh – học sinh (D-04, D-35)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `ParentUserId` | uuid → `User` | NN | | |
| `StudentId` | uuid → `Student` | NN | | |
| `LinkedAt`, `LinkedBy` | timestamptz, uuid → `User` | NN | `now()` | |
| `UnlinkedAt`, `UnlinkedBy` | timestamptz, uuid | N | | |

Gắn với **học sinh**, không gắn với lớp → tự động "đi theo học sinh" khi chuyển lớp; phụ huynh xem được lịch sử lớp cũ qua `StudentClassEnrollment`. Unique (`ParentUserId`, `StudentId`) `WHERE UnlinkedAt IS NULL`.

---

## C. Fund & Collection

### C1. `ClassFund` – Quỹ của một lớp – năm học **[xmin]**

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `ClassId` | uuid → `Class` | NN | | **UNIQUE** (1-1 với lớp – năm học) |
| `Status` | varchar(30) | NN | `'Open'` | `Open`, `Closed` (đã khóa sổ – D-20) |
| `BalanceAmount` | bigint | NN | `0` | **Tổng hợp**: số dư quỹ = Σ `FundLedgerEntry.Amount` của quỹ |
| `ReservedAmount` | bigint | NN | `0` | **Tổng hợp**: Σ phiếu chi / chuyển quỹ đi / điều chỉnh giảm đang chờ duyệt (D-40) |
| `ClosedAt`, `ClosedBy` | timestamptz, uuid → `User` | N | | GVCN chốt sổ |
| [Audit] | | | | |

Số dư khả dụng = `BalanceAmount − ReservedAmount` (không lưu). Check: `ReservedAmount >= 0`, `BalanceAmount >= 0` (Q-52), `ReservedAmount <= BalanceAmount`; `Status = 'Closed'` ⇔ `ClosedAt` có giá trị. Dòng này là **điểm khóa** (row lock) cho mọi giao dịch ghi vào quỹ.

### C2. `CollectionCampaign` – Đợt thu (D-07, D-34)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `ClassFundId` | uuid → `ClassFund` | NN | | Quỹ sở hữu mọi khoản nợ của đợt này, vĩnh viễn |
| `Name` | varchar(200) | NN | | |
| `Description` | varchar(1000) | N | | |
| `AmountPerStudent` | bigint | NN | | `> 0` |
| `DueDate` | date | NN | | Hạn nộp |
| `Status` | varchar(30) | NN | `'Open'` | `Open`, `Closed`, `Cancelled` |
| `ClosedAt`, `ClosedBy` | timestamptz, uuid | N | | |
| [Cancel] | | | | Chỉ hủy được khi chưa có phân bổ hợp lệ (BR-REC-02) |
| [Audit] | | | | |

### C3. `StudentDebt` – Khoản phải nộp của một học sinh trong một đợt thu **[xmin]**
Gộp "Collection Target" và "Student Debt" (DD-09).

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `CollectionCampaignId` | uuid → `CollectionCampaign` | NN | | Xác định quỹ sở hữu nợ (D-31) |
| `StudentId` | uuid → `Student` | NN | | |
| `AmountDue` | bigint | NN | | **Gốc**: số phải nộp (`> 0`), chụp từ `AmountPerStudent` khi tạo |
| `ExemptedAmount` | bigint | NN | `0` | **Tổng hợp**: Σ miễn giảm đã duyệt − đã đảo |
| `PaidAmount` | bigint | NN | `0` | **Tổng hợp**: Σ `ReceiptAllocation` của phiếu `Confirmed` − đã đảo |
| `ReservedAmount` | bigint | NN | `0` | **Tổng hợp**: Σ `ReceiptAllocation` của phiếu `PendingConfirmation` (D-48) |
| `OutstandingAmount` | bigint | NN | generated | `GENERATED ALWAYS AS (AmountDue − ExemptedAmount − PaidAmount) STORED` = nợ chưa thanh toán |
| [Audit] | | | | |

Số nợ còn có thể thu = `OutstandingAmount − ReservedAmount` (D-48). Check: các cột ≥ 0 và `ExemptedAmount + PaidAmount + ReservedAmount <= AmountDue` → **chống thu trùng ở mức DB**. Unique (`CollectionCampaignId`, `StudentId`).

### C4. `CollectionReceipt` – Phiếu thu **[xmin]**
Một lần nhận tiền (hoặc một lần tự trừ số dư cá nhân) cho **một** học sinh (Q-53).

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `StudentId` | uuid → `Student` | NN | | |
| `Method` | varchar(30) | NN | | `Cash`, `BankTransfer`, `StudentBalance` (tự trừ số dư cá nhân – D-09) |
| `TotalAmount` | bigint | NN | | **Gốc**: số tiền nhận (`> 0`) |
| `ReceivedOn` | date | NN | | Ngày nộp |
| `Status` | varchar(30) | NN | | `PendingConfirmation`, `Confirmed`, `Rejected`, `Cancelled` |
| `PayerUserId` | uuid → `User` | N | | Phụ huynh nộp / báo chuyển khoản |
| `CollectedByUserId` | uuid → `User` | N | | Thủ quỹ nhận tiền / xác nhận chuyển khoản; null với `StudentBalance` |
| `CollectingClassId` | uuid → `Class` | N | | Lớp của người thu (để hiển thị "Đã thu hộ" ở lớp thu hộ – D-42) |
| `CashHolderUserId` | uuid → `User` | N | | Người **ban đầu** cầm tiền (D-37, D-58); người giữ hiện tại tính theo bàn giao (financial-data-design mục 6) |
| `IsPostCloseSupplement` | boolean | NN | `false` | Chứng từ thu hộ bổ sung sau khóa sổ (D-36) |
| `ConfirmedAt`, `ConfirmedBy` | timestamptz, uuid | N | | |
| `RejectedAt`, `RejectedBy`, `RejectReason` | | N | | |
| [Cancel] | | | | Phụ huynh hủy báo chuyển khoản của mình khi còn chờ (D-57) |
| `Note` | varchar(500) | N | | |
| [Idem], [Audit] | | | | |

Phần tiền thừa = `TotalAmount − Σ ReceiptAllocation.Amount` (không lưu; khi xác nhận sinh `StudentBalanceLedgerEntry`). Phiếu có Σ phân bổ = 0 là phiếu nộp vào số dư cá nhân (D-54). Check: `Method = 'StudentBalance'` ⇒ `CollectedByUserId IS NULL AND CashHolderUserId IS NULL`; `Method = 'BankTransfer'` hoặc `Status = 'PendingConfirmation'` ⇒ cần ảnh biên lai (`Attachment`, kiểm tra ở ứng dụng).

### C5. `ReceiptAllocation` – Phân bổ tiền phiếu thu vào khoản nợ (append-only)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `CollectionReceiptId` | uuid → `CollectionReceipt` | NN | | |
| `StudentDebtId` | uuid → `StudentDebt` | NN | | Khoản nợ gốc được thanh toán (BR-REC-11) |
| `Amount` | bigint | NN | | `> 0`; không vượt số nợ còn có thể thu tại thời điểm tạo (CHECK trên `StudentDebt`) |
| `IsOnBehalf` | boolean | NN | `false` | `true` khi lớp của người thu ≠ lớp sở hữu nợ (thu hộ – D-31) |
| `CreatedAt` | timestamptz | NN | `now()` | |

Unique (`CollectionReceiptId`, `StudentDebtId`). Không sửa / xóa (trigger).

### C6. `OtherIncome` – Thu khác (D-13)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `ClassFundId` | uuid → `ClassFund` | NN | | |
| `SourceDescription` | varchar(300) | NN | | Nguồn thu (bắt buộc) |
| `Amount` | bigint | NN | | `> 0` |
| `ReceivedOn` | date | NN | | |
| `Status` | varchar(30) | NN | `'Confirmed'` | Chỉ `Confirmed` (không cần duyệt); sai thì đảo giao dịch |
| `CashHolderUserId` | uuid → `User` | NN | | Thủ quỹ nhận tiền |
| [Idem], [Audit] | | | | Chứng từ bắt buộc (`Attachment`) |

### C7. `ExpenseCategory` – Danh mục chi (D-18)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `ClassId` | uuid → `Class` | N | | Null = danh mục chung (Quản trị); có giá trị = danh mục riêng của lớp (Thủ quỹ) |
| `Name` | varchar(100) | NN | | |
| `IsActive` | boolean | NN | `true` | Ngừng dùng thay vì xóa |
| [Audit] | | | | |

Unique: tên không trùng (không phân biệt hoa thường) trong danh mục chung và trong từng lớp.

### C8. `ExpenseVoucher` – Phiếu chi **[xmin]**

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `ClassFundId` | uuid → `ClassFund` | NN | | |
| `ExpenseCategoryId` | uuid → `ExpenseCategory` | NN | | Chung hoặc của chính lớp này |
| `Description` | varchar(500) | NN | | Nội dung chi |
| `PayeeName` | varchar(200) | N | | Người nhận |
| `Amount` | bigint | NN | | `> 0`; khi `PendingApproval` được tính vào `ClassFund.ReservedAmount` |
| `SpentOn` | date | NN | | Ngày chi |
| `Status` | varchar(30) | NN | `'PendingApproval'` | `PendingApproval`, `Approved`, `Rejected`, `Cancelled` |
| [Approve], [Cancel], [Idem], [Audit] | | | | Check `ApprovedBy <> CreatedBy`. Chứng từ bắt buộc (D-16) |

---

## D. Student Financial

### D1. `StudentBalance` – Số dư cá nhân học sinh **[xmin]**
Quan hệ 1-1 với `Student`. **Không** thuộc quỹ lớp nào (D-32).

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `StudentId` | uuid → `Student` | NN | | PK |
| `ActualAmount` | bigint | NN | `0` | **Tổng hợp**: Σ `StudentBalanceLedgerEntry.Amount` = số dư thực tế |
| `ReservedAmount` | bigint | NN | `0` | **Tổng hợp**: Σ phiếu hoàn trả `PendingApproval` + `AwaitingPayment` (D-47, D-53) |
| `PendingHandoverAmount` | bigint | NN | `0` | **Tổng hợp**: phần số dư chưa bàn giao xong cho thủ quỹ đang quản lý (D-38, D-55) |
| `ManagingClassId` | uuid → `Class` | N | | Lớp có thủ quỹ đang quản lý số dư (lớp hiện tại của học sinh) |
| `UpdatedAt` | timestamptz | N | | |

Số dư khả dụng = `ActualAmount − ReservedAmount − PendingHandoverAmount` (không lưu). Check: các cột ≥ 0 và `ReservedAmount + PendingHandoverAmount <= ActualAmount`.

### D2. `Exemption` – Miễn giảm (D-10, gộp "Discount") **[xmin]**

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `StudentDebtId` | uuid → `StudentDebt` | NN | | |
| `ExemptionType` | varchar(30) | NN | | `Full`, `Partial` |
| `Amount` | bigint | NN | | `> 0`; với `Full` = nợ chưa thanh toán tại lúc lập (Q-51) |
| `Reason` | varchar(500) | NN | | Bắt buộc |
| `Status` | varchar(30) | NN | `'PendingApproval'` | `PendingApproval`, `Approved`, `Rejected`, `Cancelled` |
| [Approve], [Cancel], [Audit] | | | | Check `ApprovedBy <> CreatedBy` (khi chính GVCN lập đề nghị thì ai duyệt: Q-55) |

### D3. `RefundVoucher` – Phiếu hoàn trả tiền thừa **[xmin]**

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `StudentId` | uuid → `Student` | NN | | Trừ vào `StudentBalance` của học sinh |
| `Amount` | bigint | NN | | `> 0`; giữ chỗ từ lúc tạo đến khi `Completed` / `Rejected` / `Cancelled` |
| `Reason` | varchar(500) | NN | | |
| `Status` | varchar(30) | NN | `'PendingApproval'` | `PendingApproval`, `AwaitingPayment`, `Completed`, `Rejected`, `Cancelled` |
| [Approve] | | | | GVCN duyệt → `AwaitingPayment` |
| `CompletedAt`, `CompletedBy` | timestamptz, uuid | N | | Thủ quỹ xác nhận đã trả (D-53); minh chứng bắt buộc (`Attachment`) |
| `PaymentNote` | varchar(500) | N | | |
| [Cancel] | | | | Hủy khi chờ (người tạo) hoặc hủy sau duyệt theo quy trình riêng (Q-49) |
| `CancelApprovedBy` | uuid → `User` | N | | Người duyệt yêu cầu hủy sau duyệt (Q-49) |
| [Idem], [Audit] | | | | Check `ApprovedBy <> CreatedBy`; `Status = 'Completed'` ⇔ `CompletedAt` có giá trị |

---

## E. Transfer, Handover & Adjustment

### E1. `FundTransfer` – Chuyển quỹ giữa các lớp (D-26, D-29, D-30) **[xmin]**

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `FromClassFundId` | uuid → `ClassFund` | NN | | Lớp chuyển; giữ chỗ khi chờ duyệt |
| `ToClassFundId` | uuid → `ClassFund` | NN | | Lớp nhận; check `<> FromClassFundId` |
| `Amount` | bigint | NN | | `> 0` |
| `Reason` | varchar(500) | NN | | Bắt buộc (bắt buộc cho chứng từ điều chỉnh – D-30) |
| `IsPostCloseAdjustment` | boolean | NN | `false` | `true` = chứng từ điều chỉnh sau khóa sổ (D-30) |
| `Status` | varchar(30) | NN | `'PendingApproval'` | `PendingApproval`, `Approved`, `Rejected`, `Cancelled` |
| [Approve], [Cancel], [Idem], [Audit] | | | | GVCN lớp chuyển tạo, Quản trị duyệt; check `ApprovedBy <> CreatedBy` |

### E2. `CashHandover` – Bàn giao tiền thu hộ về lớp gốc (D-37, D-43) **[xmin]**

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `OriginClassFundId` | uuid → `ClassFund` | NN | | Quỹ lớp gốc sở hữu tiền |
| `FromUserId` | uuid → `User` | NN | | Người thu hộ (người giao) |
| `ToUserId` | uuid → `User` | NN | | Người giữ quỹ lớp gốc (người nhận); check `<> FromUserId` (D-43) |
| `ExpectedAmount` | bigint | NN | | `> 0`; = Σ phân bổ trong `CashHandoverItem`, chụp lúc tạo |
| `ReceivedAmount` | bigint | N | | `>= 0`; số thực nhận do người nhận ghi (D-43) |
| `Status` | varchar(30) | NN | `'PendingConfirmation'` | `PendingConfirmation`, `Completed`, `Discrepancy`, `Cancelled` |
| `ConfirmedAt`, `ConfirmedBy` | timestamptz, uuid | N | | Check `ConfirmedBy = ToUserId` |
| `CompletedAt` | timestamptz | N | | Khi khớp, hoặc khi chênh lệch được xử lý hết |
| [Cancel], [Audit] | | | | Người tạo hủy khi còn chờ |

Không sinh bút toán quỹ (bàn giao ≠ khoản thu).

### E3. `CashHandoverItem` – Khoản tiền thu hộ trong một bàn giao

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `CashHandoverId` | uuid → `CashHandover` | NN | | |
| `ReceiptAllocationId` | uuid → `ReceiptAllocation` | NN | | Phân bổ thu hộ được bàn giao |
| `IsActive` | boolean | NN | `true` | `false` khi bàn giao bị hủy (để phân bổ được đưa vào bàn giao khác) |

Unique: một phân bổ chỉ thuộc **một** bàn giao đang hiệu lực – partial unique (`ReceiptAllocationId`) `WHERE IsActive`.

### E4. `StudentBalanceHandover` – Bàn giao số dư cá nhân (D-32, D-38, D-55) **[xmin]**

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `StudentId` | uuid → `Student` | NN | | |
| `FromClassId`, `ToClassId` | uuid → `Class` | NN | | Lớp cũ, lớp mới |
| `FromUserId`, `ToUserId` | uuid → `User` | NN | | Thủ quỹ lớp cũ (lập), thủ quỹ lớp mới (xác nhận); check khác nhau |
| `Trigger` | varchar(30) | NN | | `ClassTransfer` (chuyển lớp) hoặc `OverpaymentAfterTransfer` (D-55) |
| `SourceReceiptId` | uuid → `CollectionReceipt` | N | | Bắt buộc khi `Trigger = 'OverpaymentAfterTransfer'` |
| `Amount` | bigint | NN | | `> 0`; số bàn giao |
| `BalanceBefore`, `BalanceAfter` | bigint | NN | | Snapshot `StudentBalance.ActualAmount` trước / sau bàn giao (D-32); bằng nhau vì bàn giao không đổi số tiền |
| `ReceivedAmount` | bigint | N | | Số thực nhận |
| `Status` | varchar(30) | NN | `'PendingConfirmation'` | `PendingConfirmation`, `Completed`, `Discrepancy`, `Cancelled` |
| `ConfirmedAt`, `ConfirmedBy` | timestamptz, uuid | N | | Check `ConfirmedBy = ToUserId` |
| `CompletedAt` | timestamptz | N | | |
| [Cancel], [Audit] | | | | |

Không sinh bút toán số dư cá nhân; chỉ giảm `StudentBalance.PendingHandoverAmount` khi hoàn tất.

### E5. `Discrepancy` – Biên bản chênh lệch bàn giao (D-44) **[xmin]**

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `CashHandoverId` | uuid → `CashHandover` | N | | Đúng **một** trong hai FK; mỗi FK **UNIQUE** |
| `StudentBalanceHandoverId` | uuid → `StudentBalanceHandover` | N | | |
| `ExpectedAmount` | bigint | NN | | Số dự kiến |
| `ReceivedAmount` | bigint | NN | | Số thực nhận (`>= 0`) |
| `DifferenceAmount` | bigint | NN | generated | `ReceivedAmount − ExpectedAmount` (âm = thiếu, dương = thừa); check `<> 0` |
| `Reason` | varchar(1000) | NN | | |
| `DelivererUserId`, `ReceiverUserId` | uuid → `User` | NN | | Người bàn giao, người nhận |
| `Status` | varchar(30) | NN | `'Open'` | `Open`, `Resolved` |
| `ResolvedAt` | timestamptz | N | | |
| [Audit] | | | | `CreatedBy` = người nhận (người lập biên bản) |

### E6. `DiscrepancyResolution` – Phương án xử lý chênh lệch (D-50, D-51) **[xmin]**

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `DiscrepancyId` | uuid → `Discrepancy` | NN | | Một biên bản có thể có nhiều phương án (bị từ chối, đề xuất lại, kết hợp – Q-54) |
| `ResolutionType` | varchar(30) | NN | | `CorrectionBeforePosting`, `SupplementaryHandover`, `ShortageWriteOff`, `SurplusRecognition` |
| `Amount` | bigint | NN | | `> 0`; phần chênh lệch phương án này xử lý |
| `Description` | varchar(1000) | NN | | |
| `Status` | varchar(30) | NN | `'Proposed'` | `Proposed`, `Approved`, `Rejected`, `Cancelled` |
| `SupplementReceivedAt`, `SupplementReceivedBy` | timestamptz, uuid | N | | Với `SupplementaryHandover`: người nhận xác nhận đã nhận phần bổ sung |
| [Approve], [Cancel], [Audit] | | | | `CreatedBy` = người đề xuất (Q-47); check `ApprovedBy <> CreatedBy`, `RejectedBy <> CreatedBy` (D-50) |

### E7. `FinancialAdjustment` – Đảo giao dịch / điều chỉnh (D-51, D-57) **[xmin]**

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `AdjustmentType` | varchar(30) | NN | | `Reversal` (đảo chứng từ hoàn tất), `ShortageWriteOff`, `SurplusRecognition` (từ phương án chênh lệch) |
| `TargetType` | varchar(30) | NN | | `ClassFund`, `StudentBalance`, `StudentDebt` – đối tượng chịu ảnh hưởng chính |
| `ClassFundId` | uuid → `ClassFund` | N | | Quỹ bị ảnh hưởng (để khóa dòng và kiểm tra khóa sổ) |
| `StudentId` | uuid → `Student` | N | | Học sinh bị ảnh hưởng (số dư cá nhân / nợ) |
| `Amount` | bigint | NN | | `> 0` (độ lớn); chiều tăng / giảm do loại và chứng từ gốc quyết định |
| `Direction` | varchar(10) | NN | | `Increase`, `Decrease` – chiều tác động lên `TargetType` |
| `Reason` | varchar(1000) | NN | | Bắt buộc |
| `IsPostClose` | boolean | NN | `false` | Phát sinh sau khi quỹ bị ảnh hưởng đã khóa sổ |
| `ReversedReceiptId` | uuid → `CollectionReceipt` | N | | Chứng từ gốc bị đảo – **đúng một** cột `Reversed*` khi `Reversal` |
| `ReversedOtherIncomeId` | uuid → `OtherIncome` | N | | |
| `ReversedExpenseVoucherId` | uuid → `ExpenseVoucher` | N | | |
| `ReversedExemptionId` | uuid → `Exemption` | N | | |
| `ReversedFundTransferId` | uuid → `FundTransfer` | N | | |
| `ReversedRefundVoucherId` | uuid → `RefundVoucher` | N | | |
| `DiscrepancyResolutionId` | uuid → `DiscrepancyResolution` | N | | Bắt buộc khi `ShortageWriteOff` / `SurplusRecognition`; **UNIQUE** |
| `Status` | varchar(30) | NN | `'PendingApproval'` | `PendingApproval`, `Approved`, `Rejected`, `Cancelled`. Điều chỉnh từ phương án chênh lệch được tạo thẳng `Approved` (việc duyệt nằm ở phương án) |
| [Approve], [Cancel], [Idem], [Audit] | | | | Check `ApprovedBy <> CreatedBy` (với `Reversal`) |

Unique: mỗi chứng từ gốc chỉ có **một** đảo giao dịch đang chờ hoặc đã duyệt (partial unique trên từng cột `Reversed*`).

---

## F. Ledger – Dữ liệu phát sinh (append-only)

### F1. `FundLedgerEntry` – Sổ cái quỹ lớp

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | bigint identity | NN | | PK; thứ tự tuyệt đối để tính "số dư sau mỗi giao dịch" |
| `ClassFundId` | uuid → `ClassFund` | NN | | |
| `EntryType` | varchar(30) | NN | | `DebtCollection`, `OtherIncome`, `Expense`, `TransferOut`, `TransferIn`, `Adjustment` |
| `Amount` | bigint | NN | | **Có dấu**: `+` tăng quỹ, `−` giảm quỹ; check `<> 0` |
| `BusinessDate` | date | NN | | Ngày nghiệp vụ của chứng từ nguồn |
| `IsPostClose` | boolean | NN | `false` | Ghi sau khi quỹ đã khóa sổ (BR-RPT-06) |
| `ReceiptAllocationId` | uuid → `ReceiptAllocation` | N | | Nguồn – **đúng một** trong 5 cột nguồn |
| `OtherIncomeId` | uuid → `OtherIncome` | N | | |
| `ExpenseVoucherId` | uuid → `ExpenseVoucher` | N | | |
| `FundTransferId` | uuid → `FundTransfer` | N | | |
| `FinancialAdjustmentId` | uuid → `FinancialAdjustment` | N | | |
| `CreatedAt`, `CreatedBy` | timestamptz, uuid → `User` | NN | `now()` | |

Unique chống ghi trùng: `ReceiptAllocationId`, `OtherIncomeId`, `ExpenseVoucherId` (mỗi cái một bút toán); (`FundTransferId`, `EntryType`) (một ra + một vào); (`FinancialAdjustmentId`, `ClassFundId`). Không UPDATE / DELETE (trigger).

### F2. `StudentBalanceLedgerEntry` – Sổ số dư cá nhân

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | bigint identity | NN | | PK |
| `StudentId` | uuid → `Student` | NN | | |
| `EntryType` | varchar(30) | NN | | `Overpayment` (+), `AppliedToDebt` (−), `RefundCompleted` (−), `Adjustment` (±) |
| `Amount` | bigint | NN | | Có dấu; check `<> 0` |
| `CollectionReceiptId` | uuid → `CollectionReceipt` | N | | Nguồn – **đúng một** trong 3 cột nguồn: phần thừa của phiếu (`Overpayment`) hoặc phiếu `StudentBalance` (`AppliedToDebt`) |
| `RefundVoucherId` | uuid → `RefundVoucher` | N | | |
| `FinancialAdjustmentId` | uuid → `FinancialAdjustment` | N | | |
| `CreatedAt`, `CreatedBy` | timestamptz, uuid → `User` | NN | `now()` | |

Unique chống ghi trùng: mỗi cột nguồn. Không UPDATE / DELETE.

---

## G. Supporting & Audit

### G1. `Attachment` – Metadata file đính kèm

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `Purpose` | varchar(30) | NN | | `ExpenseEvidence`, `TransferProof`, `IncomeEvidence`, `RefundPaymentProof`, `DiscrepancyEvidence`, `Other` |
| `FileName` | varchar(255) | NN | | |
| `ContentType` | varchar(100) | NN | | |
| `SizeBytes` | bigint | NN | | `> 0` |
| `StorageKey` | varchar(500) | NN | | Đường dẫn trong kho lưu trữ (DQ-02); **UNIQUE** |
| `Sha256` | char(64) | NN | | Kiểm tra toàn vẹn file |
| `CollectionReceiptId`, `OtherIncomeId`, `ExpenseVoucherId`, `RefundVoucherId`, `FundTransferId`, `CashHandoverId`, `StudentBalanceHandoverId`, `DiscrepancyId`, `DiscrepancyResolutionId`, `FinancialAdjustmentId` | uuid → bảng tương ứng | N | | **Đúng một** cột chủ sở hữu (`num_nonnulls(...) = 1`) |
| `UploadedAt`, `UploadedBy` | timestamptz, uuid → `User` | NN | `now()` | |

Không xóa khi chứng từ chủ đã hoàn tất.

### G2. `Notification` – Thông báo trong hệ thống (D-12)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | uuid | NN | | PK |
| `RecipientUserId` | uuid → `User` | NN | | |
| `Type` | varchar(50) | NN | | `PaymentReminder`, `PendingApproval`, `TransferConfirmed`, `FundTransferReceived`, `HandoverPending`, `DiscrepancyOpened`, … |
| `Title` | varchar(200) | NN | | |
| `Body` | varchar(2000) | NN | | |
| `RelatedEntityType` | varchar(50) | N | | Liên kết mềm để mở màn hình liên quan (không cần FK – thông báo không ảnh hưởng toàn vẹn) |
| `RelatedEntityId` | uuid | N | | |
| `CreatedAt` | timestamptz | NN | `now()` | |
| `ReadAt` | timestamptz | N | | |

### G3. `AuditLog` – Nhật ký thay đổi & truy cập (append-only)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | bigint identity | NN | | PK |
| `OccurredAt` | timestamptz | NN | `now()` | |
| `ActorUserId` | uuid → `User` | N | | Null với sự kiện trước đăng nhập |
| `Category` | varchar(30) | NN | | `Change` (thay đổi dữ liệu), `Access` (xem dữ liệu có danh tính – D-46), `Security` (đăng nhập, lời mời) |
| `Action` | varchar(100) | NN | | Ví dụ `ExpenseVoucher.Approve`, `Report.ViewStudentIdentity` |
| `EntityType` | varchar(100) | N | | |
| `EntityId` | varchar(64) | N | | Id dạng chuỗi (uuid hoặc bigint) |
| `ClassId` | uuid | N | | Phạm vi lớp để lọc theo quyền (không FK – log phải tồn tại độc lập) |
| `StudentId` | uuid | N | | Học sinh bị xem / bị ảnh hưởng |
| `OldValues`, `NewValues` | jsonb | N | | Snapshot các trường thay đổi |
| `Reason` | varchar(1000) | N | | |
| `CorrelationId` | varchar(64) | N | | Từ `CorrelationIdMiddleware` (training-doc) |
| `IpAddress` | inet | N | | |

### G4. `DocumentStatusHistory` – Lịch sử chuyển trạng thái chứng từ (append-only)

| Cột | Kiểu | Null | Default | Ghi chú |
|---|---|---|---|---|
| `Id` | bigint identity | NN | | PK |
| `DocumentType` | varchar(50) | NN | | Tên bảng chứng từ: `CollectionReceipt`, `ExpenseVoucher`, `RefundVoucher`, `Exemption`, `FundTransfer`, `CashHandover`, `StudentBalanceHandover`, `Discrepancy`, `DiscrepancyResolution`, `FinancialAdjustment` |
| `DocumentId` | uuid | NN | | Id chứng từ (liên kết mềm – DD-11) |
| `FromStatus` | varchar(30) | N | | Null khi tạo mới |
| `ToStatus` | varchar(30) | NN | | |
| `Action` | varchar(30) | NN | | `Submit`, `Resubmit`, `Approve`, `Reject`, `Cancel`, `Confirm`, `Complete`, `MarkDiscrepancy`, `Resolve` |
| `ActorUserId` | uuid → `User` | NN | | |
| `OccurredAt` | timestamptz | NN | `now()` | |
| `Comment` | varchar(1000) | N | | Lý do từ chối / hủy |
