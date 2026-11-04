# Constraints & Indexes

> Phiên bản: 0.1 (bản nháp, chờ duyệt) · Ngày: 2026-11-03
> Tên constraint theo quy ước: `PK_<Bảng>`, `FK_<Bảng>_<BảngĐích>_<Cột>`, `UQ_<Bảng>_<Cột>`, `CK_<Bảng>_<Nội dung>`, `IX_<Bảng>_<Cột>`. Cách khai báo trong EF Core: mục 8.

## 1. Primary Key

| Loại | Bảng |
|---|---|
| `uuid` (UUID v7 sinh ở ứng dụng) | Tất cả bảng, trừ các dòng dưới |
| Khóa ghép (`RoleId`, `PermissionId`) | `RolePermission` |
| `StudentId` (1-1 với `Student`) | `StudentBalance` |
| `bigint GENERATED ALWAYS AS IDENTITY` | `FundLedgerEntry`, `StudentBalanceLedgerEntry`, `AuditLog`, `DocumentStatusHistory` |

## 2. Foreign Key

- Mọi cột `…Id` / `…UserId` / `…By` trỏ đến bảng tương ứng như trong [data-dictionary.md](data-dictionary.md), **trừ** liên kết mềm có chủ đích: `Notification.RelatedEntityId`, `AuditLog.EntityId/ClassId/StudentId`, `DocumentStatusHistory.DocumentId` (DD-11).
- **`ON DELETE RESTRICT`** cho mọi FK (mặc định trong cấu hình EF Core: `DeleteBehavior.Restrict`). Không dùng `CASCADE`: dữ liệu tài chính và lịch sử không được biến mất theo dòng cha.
- Riêng `RolePermission` → `Role` / `Permission`: `CASCADE` được phép (dữ liệu cấu hình, không phải lịch sử tài chính).
- Mỗi cột FK có index (PostgreSQL không tự tạo); liệt kê ở mục 6 các index ghép thay thế.

## 3. Unique

| Constraint | Bảng (cột) | Điều kiện | Mục đích |
|---|---|---|---|
| `UQ_User_NormalizedEmail` | `User` (`NormalizedEmail`) | | BR-ACC-02 |
| `UQ_Role_Code`, `UQ_Permission_Code` | | | |
| `UQ_UserRole_Active` | `UserRole` (`UserId`, `RoleId`) | `WHERE RevokedAt IS NULL` | Một vai trò hiệu lực |
| `UQ_UserPermissionGrant_Active` | `UserPermissionGrant` (`UserId`, `PermissionId`, `ClassId`) `NULLS NOT DISTINCT` | `WHERE RevokedAt IS NULL` | |
| `UQ_Invitation_TokenHash` | `Invitation` (`TokenHash`) | | |
| `UQ_Invitation_PendingEmail` | `Invitation` (`NormalizedEmail`, `RoleId`, `ClassId`, `StudentId`) `NULLS NOT DISTINCT` | `WHERE Status = 'Pending'` | Không gửi trùng lời mời đang chờ |
| `UQ_SchoolYear_Code` | | | |
| `UQ_Class_SchoolYear_Name` | `Class` (`SchoolYearId`, `Name`) | | |
| `UQ_ClassAssignment_Active` | `ClassAssignment` (`ClassId`, `RoleId`) | `WHERE EndedAt IS NULL` | Một GVCN / một thủ quỹ / một người giữ quỹ mỗi lớp (BR-ACC-04, D-45) |
| `UQ_Student_StudentCode` | | | |
| `UQ_StudentClassEnrollment_Active` | `StudentClassEnrollment` (`StudentId`) | `WHERE EndedOn IS NULL` | Học sinh chỉ học một lớp tại một thời điểm |
| `UQ_ParentStudent_Active` | `ParentStudent` (`ParentUserId`, `StudentId`) | `WHERE UnlinkedAt IS NULL` | |
| `UQ_ClassFund_ClassId` | `ClassFund` (`ClassId`) | | 1-1 |
| `UQ_StudentDebt_Campaign_Student` | `StudentDebt` (`CollectionCampaignId`, `StudentId`) | | Một khoản nợ / học sinh / đợt |
| `UQ_ReceiptAllocation_Receipt_Debt` | `ReceiptAllocation` (`CollectionReceiptId`, `StudentDebtId`) | | |
| `UQ_ExpenseCategory_SharedName` | `ExpenseCategory` (`lower(Name)`) | `WHERE ClassId IS NULL` | Danh mục chung không trùng tên |
| `UQ_ExpenseCategory_ClassName` | `ExpenseCategory` (`ClassId`, `lower(Name)`) | `WHERE ClassId IS NOT NULL` | |
| `UQ_<Bảng>_IdempotencyKey` | `CollectionReceipt`, `OtherIncome`, `ExpenseVoucher`, `RefundVoucher`, `FundTransfer`, `FinancialAdjustment` | | Chống gửi trùng |
| `UQ_CashHandoverItem_ActiveAllocation` | `CashHandoverItem` (`ReceiptAllocationId`) | `WHERE IsActive` | Một khoản thu hộ không bị bàn giao hai lần |
| `UQ_Discrepancy_CashHandoverId`, `UQ_Discrepancy_StudentBalanceHandoverId` | | (unique cho phép nhiều NULL) | Một biên bản / bàn giao |
| `UQ_FinancialAdjustment_DiscrepancyResolutionId` | | | Một phương án sinh tối đa một điều chỉnh |
| `UQ_FinancialAdjustment_Reversed<X>` (6 constraint, mỗi cột `Reversed*Id`) | | `WHERE Status IN ('PendingApproval','Approved')` | **Mỗi chứng từ gốc chỉ đảo một lần** |
| `UQ_FundLedgerEntry_ReceiptAllocationId`, `…_OtherIncomeId`, `…_ExpenseVoucherId` | `FundLedgerEntry` | (NULL được phép nhiều) | **Một chứng từ sinh đúng một bút toán** |
| `UQ_FundLedgerEntry_Transfer_EntryType` | (`FundTransferId`, `EntryType`) | | Một bút toán ra + một bút toán vào |
| `UQ_FundLedgerEntry_Adjustment_Fund` | (`FinancialAdjustmentId`, `ClassFundId`) | | |
| `UQ_StudentBalanceLedgerEntry_CollectionReceiptId`, `…_RefundVoucherId`, `…_FinancialAdjustmentId` | | | |
| `UQ_Attachment_StorageKey` | | | |

`NULLS NOT DISTINCT` yêu cầu **PostgreSQL ≥ 15** (DQ-01).

## 4. Check

### 4.1 Số tiền

| Constraint | Điều kiện |
|---|---|
| `CK_<Bảng>_AmountPositive` | `Amount > 0` / `TotalAmount > 0` / `AmountPerStudent > 0` / `AmountDue > 0` / `ExpectedAmount > 0` trên mọi bảng chứng từ |
| `CK_<Ledger>_AmountNonZero` | `Amount <> 0` trên `FundLedgerEntry`, `StudentBalanceLedgerEntry` |
| `CK_ClassFund_Balance` | `BalanceAmount >= 0 AND ReservedAmount >= 0 AND ReservedAmount <= BalanceAmount` (D-17, D-40; không âm: Q-52) |
| `CK_StudentDebt_Amounts` | `ExemptedAmount >= 0 AND PaidAmount >= 0 AND ReservedAmount >= 0 AND ExemptedAmount + PaidAmount + ReservedAmount <= AmountDue` (D-39, D-48 – **chống thu trùng**) |
| `CK_StudentBalance_Amounts` | `ActualAmount >= 0 AND ReservedAmount >= 0 AND PendingHandoverAmount >= 0 AND ReservedAmount + PendingHandoverAmount <= ActualAmount` (D-38, D-47) |
| `CK_CashHandover_Received`, `CK_StudentBalanceHandover_Received`, `CK_Discrepancy_Received` | `ReceivedAmount IS NULL OR ReceivedAmount >= 0` |
| `CK_Discrepancy_NonZero` | `ReceivedAmount <> ExpectedAmount` |
| `CK_StudentBalanceHandover_Snapshot` | `BalanceBefore >= 0 AND BalanceAfter >= 0` |

### 4.2 Trạng thái & enum
`CK_<Bảng>_Status`, `CK_<Bảng>_<Loại>`: cột trạng thái / loại chỉ nhận giá trị liệt kê trong data dictionary.

### 4.3 Nhất quán trạng thái – dấu vết

| Constraint | Điều kiện |
|---|---|
| `CK_<Bảng>_ApprovedTrace` | `Status` ở trạng thái đã duyệt / sau duyệt ⇒ `ApprovedAt IS NOT NULL AND ApprovedBy IS NOT NULL` |
| `CK_<Bảng>_RejectedTrace` | `Status = 'Rejected'` ⇒ `RejectedAt`, `RejectedBy`, `RejectReason` có giá trị |
| `CK_<Bảng>_CancelledTrace` | `Status = 'Cancelled'` ⇒ `CancelledAt`, `CancelledBy`, `CancelReason` có giá trị |
| `CK_RefundVoucher_CompletedTrace` | `Status = 'Completed'` ⇔ `CompletedAt IS NOT NULL AND CompletedBy IS NOT NULL` |
| `CK_ClassFund_ClosedTrace` | `Status = 'Closed'` ⇔ `ClosedAt IS NOT NULL` |
| `CK_CollectionReceipt_ConfirmedTrace` | `Status = 'Confirmed'` ⇒ `ConfirmedAt IS NOT NULL` |

### 4.4 Tách nhiệm vụ (người lập ≠ người duyệt / xác nhận)

| Constraint | Điều kiện | Nguồn |
|---|---|---|
| `CK_<Bảng>_ApproverNotCreator` | `ApprovedBy IS NULL OR ApprovedBy <> CreatedBy` trên `ExpenseVoucher`, `RefundVoucher`, `Exemption`, `FundTransfer`, `FinancialAdjustment` (`Reversal`) | Nguyên tắc phân quyền 2 |
| `CK_DiscrepancyResolution_NotSelfDecided` | `(ApprovedBy IS NULL OR ApprovedBy <> CreatedBy) AND (RejectedBy IS NULL OR RejectedBy <> CreatedBy)` | D-50 |
| `CK_CashHandover_DifferentUsers` | `FromUserId <> ToUserId` | D-43 |
| `CK_CashHandover_ConfirmedByReceiver` | `ConfirmedBy IS NULL OR ConfirmedBy = ToUserId` | D-43 |
| `CK_StudentBalanceHandover_DifferentUsers`, `…_ConfirmedByReceiver` | tương tự | D-38 |

### 4.5 Cấu trúc liên kết

| Constraint | Điều kiện |
|---|---|
| `CK_FundTransfer_DifferentFunds` | `FromClassFundId <> ToClassFundId` |
| `CK_FundLedgerEntry_OneSource` | `num_nonnulls(ReceiptAllocationId, OtherIncomeId, ExpenseVoucherId, FundTransferId, FinancialAdjustmentId) = 1` |
| `CK_FundLedgerEntry_SignByType` | `DebtCollection`, `OtherIncome`, `TransferIn` ⇒ `Amount > 0`; `Expense`, `TransferOut` ⇒ `Amount < 0` |
| `CK_StudentBalanceLedgerEntry_OneSource` | `num_nonnulls(CollectionReceiptId, RefundVoucherId, FinancialAdjustmentId) = 1` |
| `CK_StudentBalanceLedgerEntry_SignByType` | `Overpayment` ⇒ `> 0`; `AppliedToDebt`, `RefundCompleted` ⇒ `< 0` |
| `CK_Attachment_OneOwner` | `num_nonnulls(<10 cột chủ sở hữu>) = 1` |
| `CK_Discrepancy_OneHandover` | `num_nonnulls(CashHandoverId, StudentBalanceHandoverId) = 1` |
| `CK_FinancialAdjustment_Source` | `(AdjustmentType = 'Reversal' AND num_nonnulls(Reversed*) = 1 AND DiscrepancyResolutionId IS NULL) OR (AdjustmentType IN ('ShortageWriteOff','SurplusRecognition') AND num_nonnulls(Reversed*) = 0 AND DiscrepancyResolutionId IS NOT NULL)` |
| `CK_CollectionReceipt_BalanceMethod` | `Method <> 'StudentBalance' OR (CollectedByUserId IS NULL AND CashHolderUserId IS NULL)` |
| `CK_StudentBalanceHandover_Trigger` | `Trigger <> 'OverpaymentAfterTransfer' OR SourceReceiptId IS NOT NULL` |
| `CK_Invitation_Target` | Mời GVCN ⇒ `ClassId IS NOT NULL`; mời phụ huynh ⇒ `StudentId IS NOT NULL` (so theo `RoleId` cố định của seed) |
| `CK_StudentClassEnrollment_Dates` | `EndedOn IS NULL OR EndedOn >= StartedOn`; `(EndedOn IS NULL) = (EndReason IS NULL)` |
| `CK_SchoolYear_Dates` | `EndDate > StartDate` |

## 5. Trigger bảo vệ dữ liệu

Trigger viết bằng PL/pgSQL, tạo qua `migrationBuilder.Sql(...)` trong migration của Infrastructure.

| Trigger | Bảng | Sự kiện | Hành vi |
|---|---|---|---|
| `TR_AppendOnly` | `FundLedgerEntry`, `StudentBalanceLedgerEntry`, `ReceiptAllocation`, `AuditLog`, `DocumentStatusHistory` | `BEFORE UPDATE OR DELETE` | `RAISE EXCEPTION` – append-only |
| `TR_NoDelete` | Mọi bảng chứng từ tài chính (mục 1 của financial-data-design), `StudentClassEnrollment`, `ClassAssignment`, `ParentStudent`, `UserRole`, `UserPermissionGrant` | `BEFORE DELETE` | `RAISE EXCEPTION` – không xóa cứng |
| `TR_FinalDocumentImmutable` | `CollectionReceipt` (`Confirmed`), `OtherIncome`, `ExpenseVoucher` (`Approved`), `RefundVoucher` (`Completed`), `Exemption` (`Approved`), `FundTransfer` (`Approved`), `CashHandover` / `StudentBalanceHandover` (`Completed`), `FinancialAdjustment` (`Approved`) | `BEFORE UPDATE` | Nếu `OLD.Status` là trạng thái hoàn tất ⇒ chặn mọi thay đổi (D-57) |
| `TR_ClosedFundGuard` | `FundLedgerEntry` | `BEFORE INSERT` | Nếu quỹ `Closed`: chỉ cho phép khi `IsPostClose = true` **và** nguồn là (a) `FundTransfer.IsPostCloseAdjustment`, (b) phân bổ của phiếu `IsPostCloseSupplement`, (c) `FinancialAdjustment.IsPostClose`. Nếu quỹ `Open`: `IsPostClose` phải `false` |
| `TR_ClosedFundNoReservation` | `ExpenseVoucher`, `FundTransfer` (không phải điều chỉnh), `CollectionCampaign` | `BEFORE INSERT` | Chặn tạo chứng từ mới vào quỹ `Closed` |
| `TR_ClosedFundNoReopen` | `ClassFund` | `BEFORE UPDATE` | Không cho `Closed → Open` (chưa có nghiệp vụ mở lại sổ) |

## 6. Index cho truy vấn thường xuyên

| Index | Bảng (cột) | Truy vấn phục vụ |
|---|---|---|
| `IX_Class_SchoolYearId` | `Class` (`SchoolYearId`) | Danh sách lớp theo năm |
| `IX_ClassAssignment_User_Active` | `ClassAssignment` (`UserId`) `WHERE EndedAt IS NULL` | Tính quyền khi đăng nhập |
| `IX_UserPermissionGrant_User_Active` | (`UserId`) `WHERE RevokedAt IS NULL` | Tính quyền |
| `IX_StudentClassEnrollment_Class_Active` | (`ClassId`) `WHERE EndedOn IS NULL` | Học sinh đang học lớp |
| `IX_StudentClassEnrollment_Student` | (`StudentId`, `StartedOn`) | Lịch sử lớp của học sinh |
| `IX_ParentStudent_Parent_Active` | (`ParentUserId`) `WHERE UnlinkedAt IS NULL` | Phụ huynh xem các con |
| `IX_CollectionCampaign_Fund_Status` | (`ClassFundId`, `Status`) | Đợt thu của lớp |
| `IX_StudentDebt_Student_Open` | (`StudentId`) `WHERE OutstandingAmount > 0` | Công nợ (kể cả nợ cũ) của học sinh – thu hộ, tự trừ số dư |
| `IX_StudentDebt_Campaign` | (`CollectionCampaignId`) | Báo cáo theo đợt thu |
| `IX_CollectionReceipt_Student_ReceivedOn` | (`StudentId`, `ReceivedOn`) | Lịch sử nộp tiền |
| `IX_CollectionReceipt_Pending` | (`CollectingClassId`) `WHERE Status = 'PendingConfirmation'` | Thủ quỹ xem báo chuyển khoản chờ xác nhận |
| `IX_ReceiptAllocation_Debt` | (`StudentDebtId`) | Tổng đã trả của khoản nợ |
| `IX_ReceiptAllocation_OnBehalf` | (`StudentDebtId`) `WHERE IsOnBehalf` | Tiền thu hộ chưa bàn giao |
| `IX_ExpenseVoucher_Fund_Status` | (`ClassFundId`, `Status`) | Phiếu chờ duyệt, giữ chỗ |
| `IX_FundTransfer_From_Status`, `IX_FundTransfer_To` | | |
| `IX_RefundVoucher_Student_Status` | (`StudentId`, `Status`) | Giữ chỗ số dư cá nhân |
| `IX_CashHandover_To_Status`, `IX_StudentBalanceHandover_To_Status` | (`ToUserId`, `Status`) | Danh sách chờ xác nhận của người nhận |
| `IX_Discrepancy_Open` | (`Status`) `WHERE Status = 'Open'` | |
| `IX_FundLedgerEntry_Fund_Id` | (`ClassFundId`, `Id`) | Sổ quỹ có số dư lũy kế (window function `SUM() OVER (ORDER BY Id)`) |
| `IX_FundLedgerEntry_Fund_BusinessDate` | (`ClassFundId`, `BusinessDate`) | Báo cáo theo khoảng thời gian |
| `IX_StudentBalanceLedgerEntry_Student_Id` | (`StudentId`, `Id`) | |
| `IX_AuditLog_Entity` | (`EntityType`, `EntityId`) | Lịch sử một chứng từ |
| `IX_AuditLog_Class_OccurredAt` | (`ClassId`, `OccurredAt`) | GVCN xem lịch sử lớp |
| `IX_AuditLog_Access` | (`StudentId`, `OccurredAt`) `WHERE Category = 'Access'` | Lịch sử truy cập danh tính (D-46) |
| `IX_DocumentStatusHistory_Document` | (`DocumentType`, `DocumentId`, `OccurredAt`) | |
| `IX_Notification_Recipient_Unread` | (`RecipientUserId`, `CreatedAt`) `WHERE ReadAt IS NULL` | |

## 7. Chiến lược chống trùng chứng từ (tổng hợp)

| Lớp bảo vệ | Cơ chế | Chặn được |
|---|---|---|
| 1. Client | `IdempotencyKey` sinh một lần cho mỗi form; `UNIQUE` | Bấm gửi hai lần, mạng chập chờn gửi lại |
| 2. Chuyển trạng thái | `UPDATE … SET Status = 'X' WHERE Id = @id AND Status = 'Y'` (hoặc `xmin`); 0 dòng ⇒ báo xung đột | Hai người cùng duyệt / xác nhận |
| 3. Khóa dòng | `SELECT … FOR UPDATE` trên `ClassFund` / `StudentDebt` / `StudentBalance` trước khi kiểm tra số dư | Race condition kiểm tra – rồi – ghi |
| 4. CHECK trên dòng tổng hợp | `CK_StudentDebt_Amounts`, `CK_ClassFund_Balance`, `CK_StudentBalance_Amounts` | Vượt nợ, vượt số dư, kể cả khi ứng dụng có lỗi |
| 5. UNIQUE trên nguồn bút toán | `UQ_FundLedgerEntry_*`, `UQ_StudentBalanceLedgerEntry_*` | Ghi sổ hai lần cho cùng chứng từ |
| 6. UNIQUE đảo một lần | `UQ_FinancialAdjustment_Reversed*` | Đảo cùng chứng từ hai lần |
| 7. UNIQUE bàn giao một lần | `UQ_CashHandoverItem_ActiveAllocation` | Bàn giao cùng khoản thu hộ hai lần |

## 8. Ánh xạ EF Core (không phải code, chỉ ghi chú cấu hình)

| Thành phần | Cách khai báo trong EF Core 10 / Npgsql 10 |
|---|---|
| Check constraint | `builder.ToTable(t => t.HasCheckConstraint("CK_…", "…"))` |
| Partial unique index | `HasIndex(…).IsUnique().HasFilter("\"EndedAt\" IS NULL")` |
| `NULLS NOT DISTINCT` | `HasIndex(…).IsUnique().AreNullsDistinct(false)` |
| Index trên biểu thức `lower(Name)` | Tạo bằng `migrationBuilder.Sql` (hoặc cột chuẩn hóa riêng) |
| Generated column | `Property(x => x.OutstandingAmount).HasComputedColumnSql("\"AmountDue\" - \"ExemptedAmount\" - \"PaidAmount\"", stored: true)` |
| `xmin` concurrency | `Property<uint>("Version").IsRowVersion()` (Npgsql map sang cột hệ thống `xmin`) |
| Identity bigint | `Property(x => x.Id).UseIdentityAlwaysColumn()` |
| Enum dạng chuỗi | `HasConversion<string>().HasMaxLength(30)` |
| Delete behavior | `OnDelete(DeleteBehavior.Restrict)` |
| Trigger, row lock | `migrationBuilder.Sql(...)`; `FromSql($"SELECT * FROM \"ClassFund\" WHERE \"Id\" = {id} FOR UPDATE")` |
