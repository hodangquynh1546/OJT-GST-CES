# Database Design – Tổng quan

> Phiên bản: 0.1 (bản nháp, chờ duyệt) · Ngày: 2026-11-03
> Căn cứ: Business Analysis v0.7 (quyết định D-01 → D-58) trong `docs/requirements/` và `docs/business-analysis/`.
> Tech Stack (theo `training-doc.md`, không thay đổi): PostgreSQL · EF Core 10.0.4 · Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 · .NET 10 · Clean Architecture.

## 1. Bộ tài liệu

| Tài liệu | Nội dung |
|---|---|
| [database-design-overview.md](database-design-overview.md) | Tài liệu này: kiến trúc, nguyên tắc, nhóm dữ liệu, quyết định chính |
| [erd.md](erd.md) | ERD tổng thể và ERD chi tiết theo nhóm (Mermaid) |
| [data-dictionary.md](data-dictionary.md) | Mô tả từng bảng, từng cột |
| [database-constraints-and-indexes.md](database-constraints-and-indexes.md) | PK, FK, Unique, Check, Index, trigger bảo vệ dữ liệu |
| [financial-data-design.md](financial-data-design.md) | Công thức số dư, giữ chỗ, luồng tiền, ví dụ tính toán |
| [database-workflows.md](database-workflows.md) | Trạng thái chứng từ, transaction boundaries, concurrency |
| [database-design-decisions.md](database-design-decisions.md) | Quyết định thiết kế, phương án cân nhắc, câu hỏi còn mở |

## 2. Vị trí của database trong kiến trúc

```text
{ProjectName}.Api ──> {ProjectName}.Application ──> {ProjectName}.Domain
                              ^
{ProjectName}.Infrastructure ─┘  (DbContext, EF Core configuration, Migrations, raw SQL cho row lock / trigger)
                              │
                              v
                         PostgreSQL
```

- **Domain**: entity và rule nghiệp vụ (ví dụ: không tạo phiếu chi vượt số dư khả dụng). Không phụ thuộc EF Core.
- **Infrastructure**: cấu hình EF Core (Fluent API), check constraint, partial index, trigger (qua migration SQL), các truy vấn khóa dòng `SELECT … FOR UPDATE`.
- **Database** là **hàng rào cuối cùng**: các bất biến tài chính quan trọng được bảo vệ ở **cả** tầng Domain **và** bằng ràng buộc PostgreSQL, để lỗi ứng dụng hoặc race condition không làm sai số liệu.

## 3. Nguyên tắc thiết kế

| # | Nguyên tắc | Áp dụng |
|---|---|---|
| 1 | **Ba lớp dữ liệu tài chính tách biệt**: dữ liệu gốc (chứng từ) → dữ liệu phát sinh (sổ cái, append-only) → số liệu tổng hợp (cột số dư cache) | [financial-data-design.md](financial-data-design.md) mục 1 |
| 2 | **Tiền là số nguyên `bigint`** (đơn vị: đồng), không dùng số thực | Mọi cột `*Amount` |
| 3 | **Không xóa cứng** dữ liệu tài chính; chứng từ đã hoàn tất không bị sửa; sai thì **đảo giao dịch** liên kết chứng từ gốc (D-57) | Trigger chặn UPDATE/DELETE; bảng `FinancialAdjustment` |
| 4 | **Khoản nợ luôn thuộc lớp – năm học của đợt thu** và không bao giờ được sao chép sang lớp mới (D-24, D-31) | `StudentDebt` → `CollectionCampaign` → `ClassFund` |
| 5 | **Số dư cá nhân học sinh không thuộc quỹ lớp nào** (D-32) | Bảng riêng `StudentBalance` + sổ riêng `StudentBalanceLedgerEntry` |
| 6 | **Bàn giao tiền không phải là khoản thu**: bàn giao chỉ đổi người giữ tiền, không tạo bút toán quỹ (D-37, D-38) | `CashHandover`, `StudentBalanceHandover` không ghi sổ quỹ |
| 7 | **Chống ghi trùng bằng ràng buộc DB**: mỗi chứng từ chỉ sinh bút toán một lần (unique trên FK nguồn); mỗi chứng từ gốc chỉ bị đảo một lần | [database-constraints-and-indexes.md](database-constraints-and-indexes.md) mục 4 |
| 8 | **Giữ chỗ lưu cùng dòng với số dư** để một `CHECK` bảo vệ được bất biến "đã dùng + giữ chỗ ≤ số dư" | `ClassFund`, `StudentDebt`, `StudentBalance` |
| 9 | **Phân quyền theo permission + phạm vi** (theo `training-doc.md`) | `Role`, `Permission`, `RolePermission`, `UserRole`, `ClassAssignment`, `UserPermissionGrant` |
| 10 | **Lịch sử theo thời gian** cho mọi phân công và quan hệ (không ghi đè) | `StudentClassEnrollment`, `ClassAssignment`, `ParentStudent`, `UserRole` có cột bắt đầu / kết thúc |
| 11 | **JSONB chỉ dùng cho snapshot audit**, không thay thế quan hệ | `AuditLog.OldValues/NewValues` |
| 12 | **Thân thiện EF Core**: mỗi bảng ↔ một entity; enum lưu dạng `varchar` + `CHECK`; concurrency token dùng cột hệ thống `xmin` | [database-design-decisions.md](database-design-decisions.md) |

## 4. Nhóm dữ liệu & số bảng

| Nhóm | Bảng | Số bảng |
|---|---|---|
| **A. Identity & Authorization** | `User`, `Role`, `Permission`, `RolePermission`, `UserRole`, `UserPermissionGrant`, `Invitation` | 7 |
| **B. Academic** | `SchoolYear`, `Class`, `ClassAssignment`, `Student`, `StudentClassEnrollment`, `ParentStudent` | 6 |
| **C. Fund & Collection** | `ClassFund`, `CollectionCampaign`, `StudentDebt`, `CollectionReceipt`, `ReceiptAllocation`, `OtherIncome`, `ExpenseCategory`, `ExpenseVoucher` | 8 |
| **D. Student Financial** | `StudentBalance`, `Exemption`, `RefundVoucher` | 3 |
| **E. Transfer, Handover & Adjustment** | `FundTransfer`, `CashHandover`, `CashHandoverItem`, `StudentBalanceHandover`, `Discrepancy`, `DiscrepancyResolution`, `FinancialAdjustment` | 7 |
| **F. Ledger (dữ liệu phát sinh)** | `FundLedgerEntry`, `StudentBalanceLedgerEntry` | 2 |
| **G. Supporting & Audit** | `Attachment`, `Notification`, `AuditLog`, `DocumentStatusHistory` | 4 |
| **Tổng** | | **37** |

### 4.1 So với danh sách gợi ý

| Gợi ý | Quyết định | Lý do |
|---|---|---|
| `School` | **Không tạo** | D-01: hệ thống chỉ phục vụ một trường; một bảng một dòng không mang thông tin nghiệp vụ. Mở rộng nhiều trường sau này: thêm `School` + `SchoolId` vào `SchoolYear` |
| `StudentClassHistory` | → `StudentClassEnrollment` | Một dòng là một giai đoạn học sinh học trong một lớp; lịch sử = tập các dòng |
| `ClassAssignment` | Giữ, dùng chung cho GVCN, Thủ quỹ, Người giữ quỹ | Cùng cấu trúc "người – lớp – vai trò – thời gian" |
| `CollectionTarget` + `StudentDebt` | **Gộp** thành `StudentDebt` | Mỗi học sinh trong một đợt thu có đúng một khoản phải nộp; tách hai bảng 1-1 là trùng lặp |
| `CollectionReceiptDetail` | → `ReceiptAllocation` | Dòng phân bổ tiền của phiếu thu vào từng khoản nợ |
| `ExpenseDetail` | **Không tạo** | BA không yêu cầu phiếu chi nhiều dòng (FR-EXP-01: một nội dung, một danh mục, một số tiền) |
| `Discount` | **Gộp** vào `Exemption` | D-10: miễn toàn phần và giảm một phần là cùng một nghiệp vụ (`ExemptionType`) |
| `ApprovalHistory` | → `DocumentStatusHistory` | Ghi mọi chuyển trạng thái (gửi, duyệt, từ chối, hủy, xác nhận), không chỉ duyệt |
| — (thêm) | `UserPermissionGrant` | D-45, D-52: quyền "cấp riêng" cho từng người (ví dụ người giữ quỹ được thu nợ cũ, được xuất báo cáo) |
| — (thêm) | `ClassFund` | Tách trạng thái khóa sổ và số dư cache khỏi `Class` để làm điểm khóa dòng (row lock) cho mọi giao dịch quỹ |
| — (thêm) | `CashHandoverItem` | Liên kết bàn giao với từng khoản tiền thu hộ, chống bàn giao trùng |
| — (thêm) | `DiscrepancyResolution` | Một biên bản chênh lệch có thể có nhiều phương án (bị từ chối, đề xuất lại, kết hợp – Q-54) |
| — (thêm) | `FundLedgerEntry`, `StudentBalanceLedgerEntry` | Sổ cái append-only: nguồn để tính và đối soát số dư, sổ quỹ có số dư sau mỗi giao dịch (FR-RPT-02) |

## 5. Các quyết định ảnh hưởng lớn đến cấu trúc

Chi tiết và phương án đã cân nhắc: [database-design-decisions.md](database-design-decisions.md).

1. **Sổ cái + số dư cache** (DD-01): số dư được cập nhật trong cùng transaction với bút toán, và luôn đối soát được với sổ cái.
2. **Lớp theo năm học** (DD-02): `Class` thuộc một `SchoolYear`; `ClassFund` quan hệ 1-1 với `Class` nên "quỹ lớp – năm học" là một dòng.
3. **Phiếu thu = tiêu đề + dòng phân bổ** (DD-03): phần vượt nợ = tổng phiếu − tổng phân bổ, ghi sang sổ số dư cá nhân. Tự trừ số dư cá nhân vào nợ cũng là một phiếu thu (phương thức `StudentBalance`), nên mọi khoản thanh toán nợ đi qua một đường duy nhất.
4. **Giữ chỗ là cột cache trên dòng số dư** (DD-04), được bảo vệ bằng `CHECK`.
5. **Đảo giao dịch / điều chỉnh dùng chung `FinancialAdjustment`** (DD-05), với FK tường minh đến chứng từ gốc và unique "chỉ đảo một lần".
6. **Bảo vệ kỳ đã khóa bằng trigger** (DD-07) trên sổ cái quỹ, chỉ cho phép ba loại chứng từ ngoại lệ đã chốt (D-30, D-36, D-44).
7. **Phân quyền** (DD-08): vai trò toàn trường qua `UserRole`; vai trò theo lớp qua `ClassAssignment`; vai trò phụ huynh qua `UserRole` + phạm vi `ParentStudent`; quyền cấp riêng qua `UserPermissionGrant`.

## 6. Truy vết Functional Requirements → bảng

| Yêu cầu | Bảng chính |
|---|---|
| FR-ADM-01 | `SchoolYear`, `Class`, `ClassAssignment` |
| FR-ADM-02, FR-ADM-03, FR-ADM-04 | `Invitation`, `User`, `UserRole`, `ParentStudent` |
| FR-ADM-05 | `User` (`PasswordHash`, `Status`), `AuditLog` (`Security`) |
| FR-ADM-06 | `ExpenseCategory` (`ClassId` null) |
| FR-ADM-07 | `StudentClassEnrollment`, `StudentBalance` (`ManagingClassId`, `PendingHandoverAmount`), `StudentBalanceHandover` |
| FR-ADM-08 | `ClassAssignment` (`FundCustodian`), `UserPermissionGrant` |
| FR-CLS-01 → FR-CLS-04 | `Class`, `ClassAssignment`, `Student`, `StudentClassEnrollment`, `ParentStudent` |
| FR-REC-01, FR-REC-02 | `CollectionCampaign`, `StudentDebt` |
| FR-REC-03, FR-REC-04, FR-REC-05 | `CollectionReceipt`, `ReceiptAllocation`, `StudentBalanceLedgerEntry` |
| FR-REC-06 | `CollectionReceipt` (`Method = StudentBalance`), `StudentBalance` |
| FR-REC-07 | `Exemption` |
| FR-REC-08 | `CollectionReceipt` (`PendingConfirmation`), `StudentDebt.ReservedAmount`, `Attachment` |
| FR-REC-09 | `FinancialAdjustment` (`Reversal`, `ReversedReceiptId`) |
| FR-REC-10 | `StudentDebt` (trạng thái suy ra) |
| FR-REC-11 | `Notification` |
| FR-REC-12 | `OtherIncome`, `Attachment` |
| FR-REC-13 | `RefundVoucher`, `StudentBalance.ReservedAmount`, `Attachment` |
| FR-REC-14, FR-REC-17 | `ReceiptAllocation` (`IsOnBehalf`), `CollectionReceipt` (`CollectingClassId`, `CashHolderUserId`) |
| FR-REC-15 | `StudentBalanceHandover` |
| FR-REC-16 | `CollectionReceipt.IsPostCloseSupplement`, `FundLedgerEntry.IsPostClose` |
| FR-REC-18 | `StudentDebt` (`CK_StudentDebt_Amounts`) |
| FR-REC-19 | `CashHandover`, `CashHandoverItem` |
| FR-REC-20 | `Discrepancy`, `DiscrepancyResolution`, `FinancialAdjustment`, `Attachment` |
| FR-EXP-01 → FR-EXP-06 | `ExpenseVoucher`, `ExpenseCategory`, `ClassFund.ReservedAmount`, `Attachment`, `FinancialAdjustment` |
| FR-RPT-01, FR-RPT-02, FR-RPT-04, FR-RPT-11 | `ClassFund`, `FundLedgerEntry` (`IsPostClose`) |
| FR-RPT-03, FR-RPT-05, FR-RPT-06 | `StudentDebt`, `ReceiptAllocation`, `StudentBalance`, `StudentBalanceLedgerEntry` |
| FR-RPT-07, FR-RPT-12, FR-RPT-14 | Đọc dữ liệu trên; phạm vi từ `UserRole`, `ClassAssignment`, `UserPermissionGrant` |
| FR-RPT-08 | `ClassFund` (`Status`, `ClosedAt`) |
| FR-RPT-09, FR-RPT-10 | `FundTransfer` (`IsPostCloseAdjustment`), `FundLedgerEntry` |
| FR-RPT-13 | `CollectionReceipt.CashHolderUserId`, `CashHandoverItem`, `CashHandover` |
| FR-RPT-15 | `CashHandover`, `StudentBalanceHandover`, `Discrepancy` |
| FR-SYS-01 | `AuditLog` (`Change`), `DocumentStatusHistory` |
| FR-SYS-02 | `AuditLog` |
| FR-SYS-03 | `Notification` |
| FR-SYS-04 | `AuditLog` (`Access`) |
| FR-SYS-05 | Cột trạng thái + [Cancel] của chứng từ, `FinancialAdjustment` |
| NFR-01, NFR-02 | Toàn bộ thiết kế; `Role`, `Permission`, `RolePermission` |
| NFR-03 | Phạm vi qua `ParentStudent`; `Report.ViewStudentIdentity` + `AuditLog` (`Access`) |
| NFR-04 | Trigger `TR_AppendOnly`, `TR_NoDelete`, `TR_FinalDocumentImmutable`, `TR_ClosedFundGuard` |
| NFR-05 | `bigint` VND, `date` |
| NFR-06 | Không ảnh hưởng database |
| NFR-07 | `Invitation` (`ExpiresAt`, `AcceptedAt`, `TokenHash`) |
| NFR-08 | Khóa dòng + `CHECK` + `UNIQUE` ([database-workflows.md](database-workflows.md)) |

## 7. Ngoài phạm vi tài liệu này

- Không có source code, entity C#, DbContext, migration hay database thật.
- Cơ chế xác thực (D-03, "tự định nghĩa" theo `training-doc.md`): chỉ lưu dữ liệu tối thiểu (`PasswordHash`, `Invitation.TokenHash`). Thuật toán băm, chính sách mật khẩu và phiên đăng nhập sẽ quyết định khi thiết kế bảo mật.
- Lưu trữ file đính kèm: chỉ lưu metadata (DQ-02).
