# Financial Data Design

> Phiên bản: 0.1 (bản nháp, chờ duyệt) · Ngày: 2026-11-03
> Căn cứ nghiệp vụ: mục 0 của [business-rules-and-processes.md](../business-analysis/business-rules-and-processes.md). Giả định tạm cho câu hỏi còn mở được đánh dấu **[Giả định Q-xx]**.

## 1. Ba lớp dữ liệu

| Lớp | Bảng | Đặc điểm | Ai ghi |
|---|---|---|---|
| **Dữ liệu gốc** (chứng từ) | `CollectionCampaign`, `StudentDebt.AmountDue`, `CollectionReceipt`, `ReceiptAllocation`, `OtherIncome`, `ExpenseVoucher`, `Exemption`, `RefundVoucher`, `FundTransfer`, `CashHandover(+Item)`, `StudentBalanceHandover`, `Discrepancy`, `DiscrepancyResolution`, `FinancialAdjustment` | Do người dùng lập; có trạng thái; sau khi hoàn tất thì bất biến | Ứng dụng, theo permission |
| **Dữ liệu phát sinh** (sổ cái) | `FundLedgerEntry`, `StudentBalanceLedgerEntry` | Hệ thống sinh **đúng một lần** khi chứng từ chuyển sang trạng thái có hiệu lực; append-only; có dấu | Chỉ trong transaction chuyển trạng thái |
| **Số liệu tổng hợp** (cache) | `ClassFund.BalanceAmount/ReservedAmount`, `StudentDebt.ExemptedAmount/PaidAmount/ReservedAmount/OutstandingAmount`, `StudentBalance.ActualAmount/ReservedAmount/PendingHandoverAmount` | Cập nhật **cùng transaction** với chứng từ / bút toán; dùng để khóa dòng và kiểm tra nhanh; luôn tính lại được từ hai lớp trên | Chỉ trong transaction chuyển trạng thái |

Nguyên tắc: báo cáo chính thức (sổ quỹ, số dư sau mỗi giao dịch) đọc từ **sổ cái**; kiểm tra điều kiện (đủ số dư không, còn nợ không) đọc từ **cột tổng hợp đã khóa dòng**. Đối soát định kỳ so sánh hai nguồn (mục 9).

## 2. Kiểu dữ liệu tiền

- Dùng `bigint` (C# `long`), đơn vị **đồng**. VND không có đơn vị nhỏ hơn đồng trong nghiệp vụ quỹ lớp; mọi số tiền là số nguyên (BR-GEN-02).
- Không dùng `real` / `double precision`: sai số nhị phân (ví dụ 0.1 + 0.2).
- Không dùng `numeric(18,0)`: đúng nhưng nặng hơn khi tính toán và map sang `decimal` trong C#, không cần thiết khi không có phần lẻ. Không dùng kiểu `money` của PostgreSQL: phụ thuộc `lc_monetary`, khó map EF Core.
- Phạm vi `bigint` (±9,2 × 10¹⁸ đồng) dư cho mọi tổng cộng.
- Số tiền chứng từ luôn **dương** (`CHECK > 0`); chiều tăng / giảm thể hiện bằng **dấu** của bút toán sổ cái hoặc cột `Direction`.

## 3. Công thức

### 3.1 Quỹ lớp (theo `ClassFund` – một lớp – năm học)

```text
BalanceAmount      = Σ FundLedgerEntry.Amount  (WHERE ClassFundId = quỹ)
ReservedAmount     = Σ ExpenseVoucher.Amount        (PendingApproval, cùng quỹ)
                   + Σ FundTransfer.Amount          (PendingApproval, FromClassFundId = quỹ)
                   + Σ FinancialAdjustment.Amount   (PendingApproval, Direction làm giảm quỹ, ClassFundId = quỹ)
AvailableAmount    = BalanceAmount − ReservedAmount            (không lưu)
```

Bút toán quỹ được sinh như sau:

| Sự kiện | Bút toán `FundLedgerEntry` | Quỹ nhận bút toán |
|---|---|---|
| Phiếu thu chuyển sang `Confirmed` | `+Amount` cho **mỗi** `ReceiptAllocation` (`DebtCollection`) | Quỹ của **đợt thu** sở hữu khoản nợ (`StudentDebt → CollectionCampaign → ClassFund`), **không** phải quỹ lớp của người thu |
| Thu khác | `+Amount` (`OtherIncome`) | Quỹ của chứng từ |
| Phiếu chi `Approved` | `−Amount` (`Expense`) | Quỹ của chứng từ |
| Chuyển quỹ `Approved` | `−Amount` (`TransferOut`) và `+Amount` (`TransferIn`), cùng `FundTransferId` | Lớp chuyển và lớp nhận |
| Điều chỉnh / đảo giao dịch `Approved` | `±Amount` (`Adjustment`), một bút toán cho mỗi quỹ bị ảnh hưởng | Quỹ của chứng từ gốc |
| Bàn giao tiền, bàn giao số dư cá nhân | **Không có bút toán** | — |
| Phần nộp thừa, hoàn trả | **Không có bút toán quỹ** (chỉ sổ số dư cá nhân) | — |

### 3.2 Khoản nợ (`StudentDebt`)

```text
AmountDue          = số phải nộp (gốc, chụp khi tạo khoản nợ)
ExemptedAmount     = Σ Exemption.Amount (Approved) − Σ đảo miễn giảm (Approved)
PaidAmount         = Σ ReceiptAllocation.Amount của phiếu Confirmed − Σ đảo phiếu thu (Approved)
ReservedAmount     = Σ ReceiptAllocation.Amount của phiếu PendingConfirmation
OutstandingAmount  = AmountDue − ExemptedAmount − PaidAmount          (generated column)
CollectableAmount  = OutstandingAmount − ReservedAmount               (không lưu; D-48)

CHECK: ExemptedAmount + PaidAmount + ReservedAmount ≤ AmountDue
```

Tình trạng nộp (BR-REC-06) suy ra, không lưu: `Exempted` nếu `ExemptedAmount = AmountDue`; `Paid` nếu `OutstandingAmount = 0`; `Partial` nếu `PaidAmount > 0`; `Unpaid` nếu ngược lại; `Overdue` = chưa đủ và quá `DueDate`.

### 3.3 Số dư cá nhân (`StudentBalance`)

```text
ActualAmount           = Σ StudentBalanceLedgerEntry.Amount (WHERE StudentId)
ReservedAmount         = Σ RefundVoucher.Amount (PendingApproval, AwaitingPayment)
PendingHandoverAmount  = Σ StudentBalanceHandover.Amount (PendingConfirmation, Discrepancy)
                         + phần chờ lập bàn giao ngay sau khi chuyển lớp
AvailableAmount        = ActualAmount − ReservedAmount − PendingHandoverAmount   (không lưu)

CHECK: ReservedAmount + PendingHandoverAmount ≤ ActualAmount
```

| Sự kiện | Bút toán `StudentBalanceLedgerEntry` |
|---|---|
| Phiếu thu `Confirmed` có phần thừa (`TotalAmount − Σ phân bổ > 0`) | `+phần thừa` (`Overpayment`), nguồn = phiếu thu |
| Phiếu thu phương thức `StudentBalance` `Confirmed` (tự trừ vào nợ) | `−TotalAmount` (`AppliedToDebt`), nguồn = phiếu thu |
| Phiếu hoàn trả `Completed` | `−Amount` (`RefundCompleted`) |
| Điều chỉnh / đảo giao dịch liên quan số dư cá nhân | `±Amount` (`Adjustment`) |
| Bàn giao số dư cá nhân | **Không có bút toán**; chỉ giảm `PendingHandoverAmount` khi hoàn tất |

### 3.4 Người đang giữ tiền (D-37, D-58)

Không lưu cột "người đang giữ" có thể sửa. Suy ra:

```text
Với mỗi ReceiptAllocation của phiếu Confirmed:
  nếu có CashHandoverItem (IsActive) thuộc CashHandover Completed  → người giữ = CashHandover.ToUserId
  ngược lại                                                       → người giữ = CollectionReceipt.CashHolderUserId
Tiền thu hộ chưa bàn giao của quỹ X = Σ phân bổ IsOnBehalf vào nợ của quỹ X mà người giữ ≠ người giữ quỹ X
```

D-58: khi người thu chính là người giữ quỹ lớp gốc, `CashHolderUserId` = người đó và không tạo `CashHandover`; phân bổ không bị tính là "chưa bàn giao".

## 4. Sáu lỗi phải chặn và cơ chế chặn

| Lỗi | Cơ chế |
|---|---|
| Tiền thừa của học sinh bị cộng vào quỹ lớp | Phần thừa không có `ReceiptAllocation` → không thể sinh `FundLedgerEntry` (bút toán quỹ chỉ có nguồn là phân bổ, thu khác, chi, chuyển quỹ, điều chỉnh). Tiền thừa chỉ vào `StudentBalanceLedgerEntry` |
| Thu nợ cũ bị ghi vào quỹ lớp mới | Quỹ của bút toán lấy từ `StudentDebt → CollectionCampaign.ClassFundId`, không lấy từ lớp người thu. `CollectingClassId` chỉ để hiển thị "Đã thu hộ" |
| Bàn giao tiền bị ghi thành khoản thu mới | `CashHandover` / `StudentBalanceHandover` không phải nguồn hợp lệ của bất kỳ bút toán nào (không có cột FK nguồn tương ứng trong sổ cái) |
| Một khoản thanh toán bị ghi hai lần | (1) `UNIQUE (ReceiptAllocationId)` trên `FundLedgerEntry`; (2) `UNIQUE (CollectionReceiptId)` trên `StudentBalanceLedgerEntry`; (3) `CHECK` trên `StudentDebt` không cho tổng đã trả + giữ chỗ vượt số phải nộp; (4) `IdempotencyKey UNIQUE` chặn gửi form hai lần; (5) chuyển trạng thái bằng câu lệnh có điều kiện trạng thái cũ (database-workflows mục 3) |
| Tiền giữ chỗ bị tính là khả dụng | Khả dụng luôn = số dư − giữ chỗ (− chờ bàn giao); giữ chỗ cập nhật **cùng transaction** với việc tạo / duyệt / từ chối / hủy chứng từ; `CHECK` giữ chỗ ≤ số dư |
| Điều chỉnh làm mất lịch sử chứng từ gốc | Chứng từ hoàn tất bất biến (trigger); điều chỉnh là dòng mới `FinancialAdjustment` có FK tới chứng từ gốc; sổ cái chỉ thêm bút toán ngược, không sửa bút toán cũ |

## 5. Từng loại chứng từ

### 5.1 Phiếu thu (`CollectionReceipt` + `ReceiptAllocation`)
- Một phiếu cho **một** học sinh [Giả định Q-53]; có thể phân bổ vào **nhiều** khoản nợ của học sinh đó (nhiều đợt thu, kể cả nợ cũ ở lớp khác).
- Khi tạo: mỗi dòng phân bổ ≤ số nợ còn có thể thu; tổng phân bổ ≤ `TotalAmount`. Không tạo dòng phân bổ cho khoản nợ có số còn có thể thu = 0 (D-54). Nếu thực tế đã nhận tiền mà không còn nợ: phiếu có Σ phân bổ = 0, toàn bộ là tiền vào số dư cá nhân.
- **Phụ huynh báo chuyển khoản** → `PendingConfirmation`: `StudentDebt.ReservedAmount += phân bổ` (giữ chỗ nợ – D-48).
- **Xác nhận** → `Confirmed`: với mỗi phân bổ: `Reserved −=`, `Paid +=`, sinh `FundLedgerEntry +` vào quỹ của khoản nợ; phần thừa sinh `StudentBalanceLedgerEntry +`.
- **Thủ quỹ ghi tiền mặt** → tạo thẳng `Confirmed` (không qua giữ chỗ).
- **Từ chối / hủy** → `Reserved −=` cho mỗi phân bổ; không bút toán.
- **Thu hộ** (`IsOnBehalf = true`): như trên; quỹ nhận là quỹ gốc. Nếu quỹ gốc đã khóa sổ: phiếu phải có `IsPostCloseSupplement = true`, bút toán có `IsPostClose = true` (D-36).
- **Tự trừ số dư cá nhân** (BR-REC-05): khi tạo khoản nợ mới cho học sinh có số dư khả dụng > 0, hệ thống tạo phiếu `Method = StudentBalance`, `Confirmed`, phân bổ `min(khả dụng, nợ)`; sinh `FundLedgerEntry +` và `StudentBalanceLedgerEntry −`.

### 5.2 Phiếu chi (`ExpenseVoucher`)
- Tạo `PendingApproval`: khóa `ClassFund`, kiểm tra `Amount ≤ Available`, `ReservedAmount += Amount`.
- Sửa số tiền khi đang chờ: `ReservedAmount += (mới − cũ)` sau khi kiểm tra `mới − cũ ≤ Available` (D-49).
- Duyệt: `ReservedAmount −= Amount`, sinh `FundLedgerEntry −Amount`, `BalanceAmount −= Amount`. Số dư khả dụng không đổi tại thời điểm duyệt.
- Từ chối / hủy: `ReservedAmount −= Amount`.

### 5.3 Phiếu hoàn trả (`RefundVoucher`)
- Tạo `PendingApproval`: khóa `StudentBalance`, kiểm tra `Amount ≤ Available`, `ReservedAmount += Amount` (D-47).
- GVCN duyệt → `AwaitingPayment`: **giữ nguyên** giữ chỗ (D-53).
- Thủ quỹ xác nhận đã trả + minh chứng → `Completed`: `ReservedAmount −= Amount`, sinh `StudentBalanceLedgerEntry −Amount`, `ActualAmount −= Amount`.
- Từ chối / hủy (kể cả hủy sau duyệt theo quy trình riêng – Q-49): `ReservedAmount −= Amount`.
- Quỹ lớp không đổi ở mọi bước.

### 5.4 Chuyển quỹ (`FundTransfer`)
- Tạo `PendingApproval`: khóa quỹ chuyển, kiểm tra `Amount ≤ Available`, giữ chỗ ở quỹ chuyển.
- Duyệt: khóa **cả hai** quỹ theo thứ tự `Id` tăng dần; quỹ chuyển `Reserved −=`, `Balance −=`; quỹ nhận `Balance +=`; hai bút toán `TransferOut` / `TransferIn` cùng `FundTransferId` (D-35).
- `IsPostCloseAdjustment = true` (D-30): cho phép ghi vào quỹ đã khóa; bút toán `IsPostClose = true`.

### 5.5 Bàn giao tiền thu hộ (`CashHandover`)
- Tạo: chọn các phân bổ thu hộ chưa bàn giao của cùng quỹ gốc → `CashHandoverItem` (partial unique chống bàn giao trùng); `ExpectedAmount` = Σ phân bổ. `ToUserId` = người giữ quỹ lớp gốc: thủ quỹ hiện tại nếu năm học chưa kết thúc, người giữ quỹ được phân công nếu đã kết thúc (D-43); khác `FromUserId`.
- Xác nhận khớp → `Completed`. **Không** bút toán (tiền đã nằm trong quỹ gốc từ lúc thu).
- Không khớp → `Discrepancy` + biên bản (mục 5.7).
- Người thu = người giữ quỹ lớp gốc → không tạo bàn giao (D-58).

### 5.6 Bàn giao số dư cá nhân (`StudentBalanceHandover`)
- Khi Quản trị xác nhận chuyển lớp (D-56): `PendingHandoverAmount = ActualAmount − ReservedAmount` [Giả định: phiếu hoàn trả đang chờ của lớp cũ vẫn giữ chỗ ở lớp cũ đến khi xử lý xong]; `ManagingClassId` = lớp mới; thủ quỹ lớp cũ lập chứng từ.
- Thủ quỹ lớp cũ thu vượt nợ sau khi học sinh đã chuyển (D-55): phần thừa → `ActualAmount +=`, `PendingHandoverAmount +=`, lập bàn giao `Trigger = OverpaymentAfterTransfer`.
- Xác nhận khớp → `Completed`: `PendingHandoverAmount −= Amount`. **Không** bút toán.
- Không khớp → `Discrepancy`; số dư vẫn chưa khả dụng (BR-DIS-08).

### 5.7 Chênh lệch & điều chỉnh (`Discrepancy`, `DiscrepancyResolution`, `FinancialAdjustment`)
- Biên bản lưu `ExpectedAmount`, `ReceivedAmount`, `DifferenceAmount` (generated). Không số dư nào đổi khi lập biên bản (D-44).
- Mỗi phương án (D-51) có số tiền; biên bản `Resolved` khi Σ phương án `Approved` = |chênh lệch| [Giả định Q-54].
- Ảnh hưởng số dư theo loại [Giả định Q-48]:

| Loại phương án | Bàn giao tiền thu hộ | Bàn giao số dư cá nhân |
|---|---|---|
| `CorrectionBeforePosting` – số liệu nhập sai, không có tiền chênh thật | Không đổi số dư | Không đổi số dư |
| `SupplementaryHandover` – người giao bàn giao thêm phần thiếu | Không đổi số dư; người nhận xác nhận đã nhận phần bổ sung | Không đổi số dư |
| `ShortageWriteOff` – ghi nhận thiếu hụt | `FinancialAdjustment` `Decrease` quỹ gốc → `FundLedgerEntry −` | `FinancialAdjustment` `Decrease` số dư cá nhân → `StudentBalanceLedgerEntry −`, `PendingHandoverAmount −=` |
| `SurplusRecognition` – ghi nhận tiền thừa | `FinancialAdjustment` `Increase` quỹ gốc → `FundLedgerEntry +` | `FinancialAdjustment` `Increase` số dư cá nhân → `StudentBalanceLedgerEntry +` |

- Quỹ gốc đã khóa sổ: bút toán điều chỉnh `IsPostClose = true` (ngoại lệ do Quản trị duyệt – D-50).

### 5.8 Đảo giao dịch (`FinancialAdjustment.AdjustmentType = Reversal`)

| Chứng từ gốc | Ảnh hưởng ngược |
|---|---|
| Phiếu thu `Confirmed` | Mỗi phân bổ: `PaidAmount −=`, `FundLedgerEntry −` ở quỹ của khoản nợ (gộp một bút toán mỗi quỹ). Phần thừa: `StudentBalanceLedgerEntry −` (cần số dư cá nhân khả dụng đủ). Phiếu `StudentBalance`: `StudentBalanceLedgerEntry +` |
| Thu khác | `FundLedgerEntry −` |
| Phiếu chi `Approved` | `FundLedgerEntry +` |
| Miễn giảm `Approved` | `ExemptedAmount −=` (cần `CHECK` của `StudentDebt` còn thỏa) |
| Chuyển quỹ `Approved` | `FundLedgerEntry +` quỹ chuyển, `−` quỹ nhận |
| Hoàn trả `Completed` | `StudentBalanceLedgerEntry +` |

Đảo làm số dư âm (quỹ hoặc số dư cá nhân): bị `CHECK` chặn [Giả định Q-52]. Người tạo / duyệt: Q-49.

## 6. Khóa sổ

- `ClassFund.Status = Closed`: trigger trên `FundLedgerEntry` từ chối bút toán mới vào quỹ này, **trừ** bút toán có `IsPostClose = true` và nguồn thuộc một trong ba ngoại lệ đã chốt: chuyển quỹ `IsPostCloseAdjustment` (D-30), phân bổ của phiếu `IsPostCloseSupplement` (D-36), điều chỉnh chênh lệch `IsPostClose` (D-44, D-50).
- Không tạo được chứng từ có giữ chỗ (phiếu chi, chuyển quỹ thường) vào quỹ đã khóa.
- Điều kiện khóa (BR-CLS-02): không còn chứng từ chờ của quỹ (phiếu chi, chuyển quỹ đi, phiếu thu chờ xác nhận có phân bổ vào quỹ, miễn giảm chờ duyệt, bàn giao chờ xác nhận vào quỹ). Kiểm tra trong cùng transaction đang khóa dòng `ClassFund`.
- Khóa sổ **không** chuyển số dư quỹ (D-25) và **không** sao chép khoản nợ: nợ chưa thanh toán vẫn ở quỹ cũ, vẫn thu được (thu hộ bổ sung).

## 7. Ví dụ tính toán

Giả sử: học sinh **An**, lớp **6A / 2026-2027** (quỹ Q6A), sau đó lên **7A / 2027-2028** (quỹ Q7A). Thủ quỹ 6A: **Bình**; thủ quỹ 7A: **Chi**; người giữ quỹ 6A sau khi kết thúc năm: **Dung**.

| # | Sự kiện | Q6A Balance / Reserved | Q7A Balance | Nợ An (Due / Exempt / Paid / Reserved → Outstanding) | Số dư cá nhân An (Actual / Reserved / PendingHO) |
|---|---|---|---|---|---|
| 1 | Đợt thu "Quỹ HK1" 500.000 đ → tạo nợ N1 | 0 / 0 | — | N1: 500k / 0 / 0 / 0 → 500k | 0 / 0 / 0 |
| 2 | An nộp tiền mặt 600.000 đ cho Bình: phân bổ 500k vào N1, thừa 100k | **500k** / 0 | — | N1: 500k / 0 / 500k / 0 → 0 | **100k** / 0 / 0 |
| 3 | Đợt thu "Quỹ HK2" 300.000 đ → nợ N2; tự trừ 100k từ số dư | **600k** / 0 | — | N2: 300k / 0 / 100k / 0 → 200k | **0** / 0 / 0 |
| 4 | Bình lập phiếu chi 250.000 đ (chờ duyệt) | 600k / **250k** (khả dụng 350k) | — | | |
| 5 | Phiếu chi thứ hai 400.000 đ | **bị chặn**: 400k > khả dụng 350k | | | |
| 6 | GVCN duyệt phiếu chi 250k | **350k** / 0 | — | | |
| 7 | GVCN 6A chốt sổ (không còn chứng từ chờ) | 350k, `Closed` | — | N2 vẫn thuộc Q6A, còn 200k | |
| 8 | An lên 7A (Quản trị xác nhận) | | 0 | N2 đi theo An, vẫn thuộc Q6A | 0 → không cần bàn giao |
| 9 | Mẹ An báo chuyển khoản 250.000 đ cho N2 (Chi là thủ quỹ đang quản lý) | | | N2: Reserved **200k** → còn có thể thu 0 | |
| 10 | Chi xác nhận: phân bổ 200k vào N2 (thu hộ, sau khóa sổ), thừa 50k | **550k** (bút toán `IsPostClose`) | **0** (không đổi – chỉ hiện "Đã thu hộ") | N2: Paid 300k, Reserved 0 → 0 | **50k** (Chi quản lý, không cần bàn giao) |
| 11 | Chi lập bàn giao 200k cho Dung (người giữ quỹ 6A) | 550k (không đổi) | | | |
| 12 | Dung đếm được 180k → `Discrepancy` −20k | 550k (không đổi) | | | |
| 13 | Phương án `ShortageWriteOff` 20k, Quản trị duyệt (Q6A đã khóa) | **530k** (điều chỉnh `IsPostClose`) | | | |
| 14 | Chi lập hoàn trả 50k → GVCN 7A duyệt → Chi trả tiền, đính minh chứng | | | | 50k / **50k** → `AwaitingPayment` vẫn 50k / 50k → `Completed`: **0** / 0 |

Kiểm tra: tổng nộp của An = 600k + 250k = 850k = quỹ 6A nhận (500k + 100k + 200k) + hoàn trả 50k; trong đó 100k đi qua số dư cá nhân rồi vào nợ N2. Thiếu hụt 20k là khoản riêng có biên bản và người duyệt.

## 8. Ví dụ chống ghi trùng

- Hai thủ quỹ (Bình – lớp cũ, Chi – lớp mới) cùng ghi thu 200k cho N2 còn 200k: transaction thứ hai chờ khóa dòng `StudentDebt`, đọc lại thấy số còn có thể thu = 0 → **từ chối tạo phiếu thanh toán nợ** (D-54). Nếu Bình thực tế đã cầm tiền: lập phiếu thu không phân bổ → 200k vào số dư cá nhân, lập bàn giao số dư cá nhân cho Chi (D-55).
- Bấm "Xác nhận" hai lần: lần hai không cập nhật được vì điều kiện `WHERE Status = 'PendingConfirmation'` không còn đúng; nếu có lọt, `UNIQUE (ReceiptAllocationId)` trên sổ cái chặn bút toán thứ hai.

## 9. Đối soát

Truy vấn kiểm tra (chạy định kỳ hoặc sau mỗi lần triển khai), kết quả phải rỗng:

```sql
-- Quỹ: số dư cache khác tổng sổ cái
SELECT f."Id" FROM "ClassFund" f
LEFT JOIN (SELECT "ClassFundId", SUM("Amount") s FROM "FundLedgerEntry" GROUP BY 1) l ON l."ClassFundId" = f."Id"
WHERE f."BalanceAmount" <> COALESCE(l.s, 0);

-- Số dư cá nhân: cache khác tổng sổ
SELECT b."StudentId" FROM "StudentBalance" b
LEFT JOIN (SELECT "StudentId", SUM("Amount") s FROM "StudentBalanceLedgerEntry" GROUP BY 1) l USING ("StudentId")
WHERE b."ActualAmount" <> COALESCE(l.s, 0);

-- Nợ: PaidAmount khác tổng phân bổ của phiếu Confirmed (trừ phiếu đã bị đảo)
-- Giữ chỗ quỹ: ReservedAmount khác tổng chứng từ đang chờ
```
