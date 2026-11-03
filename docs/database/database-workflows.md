# Database Workflows – Trạng thái, Transaction, Concurrency

> Phiên bản: 0.1 (bản nháp, chờ duyệt) · Ngày: 2026-11-03
> Trạng thái nghiệp vụ: mục 2 của [business-rules-and-processes.md](../business-analysis/business-rules-and-processes.md). Tài liệu này thêm ảnh hưởng lên dữ liệu và ranh giới transaction.

## 1. Quy ước chung

- Mỗi chuyển trạng thái là **một transaction** gồm: khóa dòng → kiểm tra điều kiện → cập nhật chứng từ (có điều kiện trạng thái cũ) → cập nhật số liệu tổng hợp → sinh bút toán (nếu có) → ghi `DocumentStatusHistory` + `AuditLog` → commit. Thông báo (`Notification`) ghi trong cùng transaction.
- Isolation level: **Read Committed** (mặc định PostgreSQL) **+ khóa dòng tường minh** `SELECT … FOR UPDATE`. Không dùng Serializable cho toàn hệ thống (DD-10).
- **Thứ tự khóa** cố định để tránh deadlock: `ClassFund` (theo `Id` tăng dần) → `StudentDebt` (theo `Id`) → `StudentBalance` → dòng chứng từ.
- Mọi chuyển trạng thái dùng `UPDATE … WHERE Id = @id AND Status = @expected`; số dòng cập nhật = 0 ⇒ trả lỗi xung đột (người khác đã xử lý). Màn hình sửa dùng thêm `xmin` (optimistic concurrency của EF Core).
- Thao tác ghi vào quỹ đều lấy khóa trên dòng `ClassFund` liên quan (`FOR UPDATE` khi đổi số dư / giữ chỗ, `FOR SHARE` khi chỉ cần quỹ không bị khóa sổ đồng thời) – nhờ vậy **khóa sổ không chen được giữa chừng**. Chọn đúng mức khóa **ngay từ đầu** transaction; không lấy `FOR SHARE` rồi nâng lên cập nhật (dễ deadlock).

## 2. Trạng thái & ảnh hưởng dữ liệu

Ký hiệu: **R** = giữ chỗ, **L** = sinh bút toán sổ cái, **—** = không ảnh hưởng số liệu.

### 2.1 `CollectionReceipt`

| Từ | Đến | Ai | Ảnh hưởng |
|---|---|---|---|
| (mới) | `PendingConfirmation` | Phụ huynh (báo chuyển khoản) | **R** `StudentDebt.ReservedAmount += phân bổ` |
| (mới) | `Confirmed` | Thủ quỹ (tiền mặt / thu hộ); Hệ thống (`StudentBalance`) | `PaidAmount +=`; **L** quỹ + (mỗi phân bổ); **L** số dư cá nhân + phần thừa (hoặc − với `StudentBalance`) |
| `PendingConfirmation` | `Confirmed` | Thủ quỹ | `Reserved −=`, `Paid +=`; **L** như trên |
| `PendingConfirmation` | `Rejected` | Thủ quỹ | `Reserved −=` |
| `PendingConfirmation` | `Cancelled` | Người tạo | `Reserved −=` |
| `Confirmed` | (giữ nguyên) | — | Đảo bằng `FinancialAdjustment` (mục 2.8) |

### 2.2 `ExpenseVoucher`

| Từ | Đến | Ai | Ảnh hưởng |
|---|---|---|---|
| (mới) / `Rejected` | `PendingApproval` | Thủ quỹ (tạo / sửa & gửi lại) | **R** `ClassFund.ReservedAmount += Amount` (kiểm tra ≤ khả dụng) |
| `PendingApproval` | `PendingApproval` (sửa số tiền) | Thủ quỹ | **R** `+= (mới − cũ)` sau khi kiểm tra (D-49) |
| `PendingApproval` | `Approved` | GVCN (≠ người tạo) | `Reserved −=`, `Balance −=`; **L** quỹ − |
| `PendingApproval` | `Rejected` | GVCN | `Reserved −=` |
| `PendingApproval` | `Cancelled` | Người tạo | `Reserved −=` |
| `Approved` | (giữ nguyên) | — | Đảo bằng `FinancialAdjustment` (Q-49; bước "đã chi": Q-50) |

### 2.3 `RefundVoucher`

| Từ | Đến | Ai | Ảnh hưởng |
|---|---|---|---|
| (mới) | `PendingApproval` | Thủ quỹ đang quản lý số dư | **R** `StudentBalance.ReservedAmount += Amount` (kiểm tra ≤ khả dụng) |
| `PendingApproval` | `AwaitingPayment` | GVCN (≠ người tạo) | — (vẫn giữ chỗ – D-53) |
| `AwaitingPayment` | `Completed` | Thủ quỹ, kèm minh chứng | `Reserved −=`, `Actual −=`; **L** số dư cá nhân − |
| `PendingApproval` | `Rejected` | GVCN | `Reserved −=` |
| `PendingApproval` | `Cancelled` | Người tạo | `Reserved −=` |
| `AwaitingPayment` | `Cancelled` | Quy trình hủy riêng (Q-49) | `Reserved −=` |

### 2.4 `FundTransfer` (kể cả chứng từ điều chỉnh sau khóa sổ)

| Từ | Đến | Ai | Ảnh hưởng |
|---|---|---|---|
| (mới) / `Rejected` | `PendingApproval` | GVCN lớp chuyển | **R** quỹ chuyển `+= Amount` |
| `PendingApproval` | `Approved` | Quản trị | Quỹ chuyển `Reserved −=`, `Balance −=`; quỹ nhận `Balance +=`; **L** 2 bút toán; thông báo GVCN lớp nhận |
| `PendingApproval` | `Rejected` / `Cancelled` | Quản trị / người tạo | `Reserved −=` |

### 2.5 `Exemption`

| Từ | Đến | Ai | Ảnh hưởng |
|---|---|---|---|
| (mới) | `PendingApproval` | Thủ quỹ / GVCN | — [Giả định: không giữ chỗ nợ; Q-51] |
| `PendingApproval` | `Approved` | GVCN (≠ người tạo) | `StudentDebt.ExemptedAmount += Amount` (CHECK nợ) |
| `PendingApproval` | `Rejected` / `Cancelled` | GVCN / người tạo | — |

### 2.6 `CashHandover` và `StudentBalanceHandover`

| Từ | Đến | Ai | Ảnh hưởng |
|---|---|---|---|
| (mới) | `PendingConfirmation` | Người giao | Tạo `CashHandoverItem` (bàn giao tiền thu hộ) |
| `PendingConfirmation` | `Completed` | Người nhận (= `ToUserId`), số tiền khớp | Tiền thu hộ: — (chỉ đổi người giữ). Số dư cá nhân: `PendingHandoverAmount −= Amount` |
| `PendingConfirmation` | `Discrepancy` | Người nhận, không khớp | Tạo `Discrepancy`; — |
| `Discrepancy` | `Completed` | Hệ thống, khi `Discrepancy` → `Resolved` | Số dư cá nhân: `PendingHandoverAmount` giảm phần còn lại sau điều chỉnh |
| `PendingConfirmation` | `Cancelled` | Người tạo | `CashHandoverItem.IsActive = false` |

### 2.7 `Discrepancy` và `DiscrepancyResolution`

| Đối tượng | Từ | Đến | Ai | Ảnh hưởng |
|---|---|---|---|---|
| `DiscrepancyResolution` | (mới) | `Proposed` | Người đề xuất (Q-47) | — |
| | `Proposed` | `Approved` | GVCN lớp gốc / Quản trị (lớp đã khóa sổ / không còn GVCN), ≠ người đề xuất | Theo loại (financial-data-design mục 5.7): `ShortageWriteOff` / `SurplusRecognition` tạo `FinancialAdjustment` `Approved` + **L** |
| | `Proposed` | `Rejected` | Người duyệt | — (D-50) |
| | `Proposed` | `Cancelled` | Người đề xuất | — |
| `Discrepancy` | `Open` | `Resolved` | Hệ thống, khi Σ phương án `Approved` = \|chênh lệch\| (Q-54) | Bàn giao liên quan → `Completed` |

### 2.8 `FinancialAdjustment`

| Từ | Đến | Ai | Ảnh hưởng |
|---|---|---|---|
| (mới) | `PendingApproval` | Người tạo đảo (Q-49) | **R** nếu làm giảm quỹ (`ClassFund.ReservedAmount +=`) hoặc giảm số dư cá nhân (`StudentBalance.ReservedAmount +=`) |
| `PendingApproval` | `Approved` | Người duyệt (≠ người tạo) | Giải phóng **R**; cập nhật tổng hợp ngược với chứng từ gốc; **L** bút toán ngược |
| `PendingApproval` | `Rejected` / `Cancelled` | Người duyệt / người tạo | Giải phóng **R** |
| (mới) | `Approved` | Hệ thống, khi phương án chênh lệch được duyệt | **L** ngay |

### 2.9 Khác

| Đối tượng | Trạng thái |
|---|---|
| `ClassFund` | `Open` → `Closed` (GVCN, một chiều) |
| `CollectionCampaign` | `Open` → `Closed`; `Open` → `Cancelled` (chỉ khi chưa có phân bổ hợp lệ) |
| `Invitation` | `Pending` → `Accepted` / `Expired` / `Revoked` |
| `SchoolYear` | `Planned` → `Active` → `Ended` |

## 3. Transaction boundaries cho 7 tình huống

### 3.1 Hai người cùng xác nhận thanh toán một khoản nợ
Ví dụ: thủ quỹ lớp cũ và thủ quỹ lớp mới cùng ghi thu cho khoản nợ cũ N2 còn 200.000 đ (D-39).

```text
BEGIN
  SELECT … FROM "ClassFund" WHERE "Id" IN (quỹ của các khoản nợ) ORDER BY "Id" FOR UPDATE  -- phiếu Confirmed cập nhật BalanceAmount;
                                                                                        -- phiếu PendingConfirmation chỉ cần FOR SHARE
  SELECT … FROM "StudentDebt" WHERE "Id" IN (…) ORDER BY "Id" FOR UPDATE                    -- người thứ hai chờ ở đây
  kiểm tra: phân bổ ≤ OutstandingAmount − ReservedAmount; nếu số còn thu = 0 → từ chối (D-54)
  INSERT CollectionReceipt (IdempotencyKey UNIQUE), ReceiptAllocation
  UPDATE StudentDebt SET PaidAmount = PaidAmount + @a          -- CK_StudentDebt_Amounts là chốt cuối
  INSERT FundLedgerEntry (UNIQUE ReceiptAllocationId), StudentBalanceLedgerEntry (phần thừa)
  UPDATE ClassFund SET BalanceAmount = BalanceAmount + @a      -- đã khóa FOR UPDATE ở bước đầu (không nâng cấp khóa giữa chừng)
  INSERT DocumentStatusHistory, AuditLog
COMMIT
```
Người thứ hai, sau khi người thứ nhất commit, đọc lại `StudentDebt` thấy số còn có thể thu = 0 → bị từ chối. Xác nhận một phiếu `PendingConfirmation` hai lần: `UPDATE … WHERE Status = 'PendingConfirmation'` lần hai trả 0 dòng.

### 3.2 Hai người cùng tạo phiếu hoàn trả từ một số dư cá nhân
```text
BEGIN
  SELECT … FROM "StudentBalance" WHERE "StudentId" = @s FOR UPDATE
  kiểm tra: @amount ≤ ActualAmount − ReservedAmount − PendingHandoverAmount
  INSERT RefundVoucher (PendingApproval)
  UPDATE StudentBalance SET ReservedAmount = ReservedAmount + @amount   -- CK_StudentBalance_Amounts
COMMIT
```
Phiếu thứ hai thấy số khả dụng đã giảm → bị chặn (D-47).

### 3.3 Hai phiếu chi cùng dùng số dư quỹ
```text
BEGIN
  SELECT … FROM "ClassFund" WHERE "Id" = @f FOR UPDATE
  kiểm tra: Status = 'Open' AND @amount ≤ BalanceAmount − ReservedAmount
  INSERT ExpenseVoucher (PendingApproval)
  UPDATE ClassFund SET ReservedAmount = ReservedAmount + @amount        -- CK_ClassFund_Balance
COMMIT
```
Duyệt: cùng khóa `ClassFund`, `UPDATE ExpenseVoucher … WHERE Status = 'PendingApproval'`, chuyển giữ chỗ thành bút toán.

### 3.4 Chuyển quỹ giữa hai lớp (khi duyệt)
```text
BEGIN
  SELECT … FROM "ClassFund" WHERE "Id" IN (@from, @to) ORDER BY "Id" FOR UPDATE   -- thứ tự cố định chống deadlock
  UPDATE FundTransfer SET Status = 'Approved', … WHERE Id = @t AND Status = 'PendingApproval'
  kiểm tra quỹ nhận / chuyển: Open, hoặc IsPostCloseAdjustment = true (D-30)
  UPDATE ClassFund(@from) SET ReservedAmount -= @a, BalanceAmount -= @a
  UPDATE ClassFund(@to)   SET BalanceAmount += @a
  INSERT FundLedgerEntry ×2 (UNIQUE FundTransferId + EntryType; trigger TR_ClosedFundGuard)
  INSERT Notification (GVCN lớp nhận), DocumentStatusHistory, AuditLog
COMMIT
```

### 3.5 Bàn giao số dư cá nhân (khi người nhận xác nhận)
```text
BEGIN
  SELECT … FROM "StudentBalance" WHERE "StudentId" = @s FOR UPDATE
  SELECT … FROM "StudentBalanceHandover" WHERE "Id" = @h FOR UPDATE
  kiểm tra: ConfirmedBy = ToUserId; Status = 'PendingConfirmation'
  nếu ReceivedAmount = Amount:
     UPDATE handover → Completed; UPDATE StudentBalance SET PendingHandoverAmount -= Amount
  ngược lại:
     UPDATE handover → Discrepancy; INSERT Discrepancy (số dư vẫn chưa khả dụng)
COMMIT
```
Lúc **tạo** bàn giao khi Quản trị xác nhận chuyển lớp: cùng transaction với kết thúc `StudentClassEnrollment` cũ, tạo dòng mới, cập nhật `StudentBalance.ManagingClassId` và `PendingHandoverAmount`.

### 3.6 Xử lý chênh lệch (khi duyệt phương án)
```text
BEGIN
  SELECT … FROM "ClassFund" WHERE "Id" = @fundGoc FOR UPDATE          -- nếu loại ảnh hưởng quỹ
  hoặc SELECT … FROM "StudentBalance" WHERE "StudentId" = @s FOR UPDATE  -- nếu ảnh hưởng số dư cá nhân
  SELECT … FROM "Discrepancy" WHERE "Id" = @d FOR UPDATE
  UPDATE DiscrepancyResolution SET Status = 'Approved' … WHERE Id = @r AND Status = 'Proposed'   -- CK người duyệt ≠ người đề xuất
  nếu ShortageWriteOff / SurplusRecognition:
     INSERT FinancialAdjustment (Approved, UNIQUE DiscrepancyResolutionId, IsPostClose nếu quỹ đã khóa)
     INSERT FundLedgerEntry / StudentBalanceLedgerEntry; UPDATE tổng hợp
  nếu Σ phương án Approved = |DifferenceAmount|: Discrepancy → Resolved; bàn giao → Completed
COMMIT
```

### 3.7 Khóa sổ năm học (một quỹ lớp)
```text
BEGIN
  SELECT … FROM "ClassFund" WHERE "Id" = @f FOR UPDATE      -- chặn mọi giao dịch mới của quỹ (chúng cần FOR SHARE / FOR UPDATE)
  kiểm tra không còn chứng từ chờ của quỹ (BR-CLS-02):
     ExpenseVoucher PendingApproval; FundTransfer PendingApproval (From = @f);
     CollectionReceipt PendingConfirmation có phân bổ vào nợ của quỹ; Exemption PendingApproval của nợ thuộc quỹ;
     CashHandover PendingConfirmation / Discrepancy với OriginClassFundId = @f; FinancialAdjustment PendingApproval của quỹ
  kiểm tra đối soát: BalanceAmount = Σ FundLedgerEntry; ReservedAmount = 0
  UPDATE ClassFund SET Status = 'Closed', ClosedAt = now(), ClosedBy = @u WHERE Id = @f AND Status = 'Open'
  INSERT AuditLog, DocumentStatusHistory
COMMIT
```
Mọi giao dịch mới vào quỹ phải lấy khóa `ClassFund` trước → hoặc chạy xong trước khi khóa sổ, hoặc chờ và sau đó thấy `Closed` → bị trigger / kiểm tra chặn.

## 4. Chống ghi nhận trùng & race condition – tổng hợp

| Rủi ro | Phòng tuyến chính | Phòng tuyến dự phòng |
|---|---|---|
| Kiểm tra – rồi – ghi đồng thời (vượt số dư / vượt nợ) | `SELECT … FOR UPDATE` dòng tổng hợp | `CHECK` trên dòng tổng hợp |
| Hai người cùng chuyển trạng thái | `UPDATE … WHERE Status = expected` | `xmin` |
| Gửi form hai lần | `IdempotencyKey UNIQUE` | Trả lại kết quả của lần đầu |
| Bút toán sinh hai lần | Chỉ sinh trong transaction chuyển trạng thái | `UNIQUE` trên cột nguồn của sổ cái |
| Đảo hai lần | Kiểm tra trạng thái chứng từ gốc | `UQ_FinancialAdjustment_Reversed*` |
| Bàn giao hai lần | Chỉ chọn phân bổ chưa bàn giao | `UQ_CashHandoverItem_ActiveAllocation` |
| Deadlock | Thứ tự khóa cố định (mục 1) | Retry có giới hạn khi gặp `40P01` |
| Giao dịch chen vào lúc khóa sổ | Khóa `ClassFund` | `TR_ClosedFundGuard`, `TR_ClosedFundNoReservation` |

## 5. Audit trail

| Câu hỏi | Lấy từ |
|---|---|
| Ai tạo, lúc nào | `CreatedBy`, `CreatedAt` trên chứng từ |
| Ai sửa | `UpdatedBy/UpdatedAt` (lần cuối) + `AuditLog` (`Category = Change`, `OldValues/NewValues`) |
| Ai duyệt / từ chối / hủy / xác nhận / hoàn tất | Cột [Approve]/[Cancel]/`ConfirmedBy`/`CompletedBy` (lần cuối) + `DocumentStatusHistory` (mọi lần) |
| Ai xem danh tính học sinh (D-46) | `AuditLog` (`Category = Access`) |
| Ai phân công / thu hồi quyền | `ClassAssignment`, `UserRole`, `UserPermissionGrant` (cột thời gian) + `AuditLog` |

`AuditLog` và `DocumentStatusHistory` được ghi **trong cùng transaction** với thay đổi (không ghi bất đồng bộ), nên không có thay đổi tài chính nào thiếu dấu vết.
