# Quy tắc nghiệp vụ & quy trình

> Mã `BR-xx` được trích trong code (comment), test (tên test) và module plan. Quy tắc đã có hàm dùng chung ở Domain ghi rõ ở cột "Cài đặt".

## 1. Quy tắc nghiệp vụ

### 1.1. Phạm vi tài chính

| Mã | Quy tắc | Cài đặt |
|---|---|---|
| BR-01 | Mỗi khoản thu, giao dịch, thông báo, dòng audit thuộc đúng **một** phạm vi: Quỹ chung (`EventId = null`) hoặc một Sự kiện (`EventId` có giá trị). | Cột `EventId` nullable – không thể "vừa chung vừa sự kiện". `FinancialScope` cho Notification/AuditLog. |
| BR-02 | Số liệu quỹ chung **không** gồm giao dịch sự kiện và ngược lại. Mọi truy vấn báo cáo phải lọc theo `EventId`. | Module M13 |
| BR-03 | Không lấy quỹ chung bù thiếu sự kiện, không chuyển tiền thừa sự kiện vào quỹ chung. Không có API chuyển tiền giữa hai phạm vi. | – |
| BR-04 | Quỹ chung không quyết toán theo hoạt động; cần thêm tiền thì tạo khoản thu chung mới; số dư giữ tới tổng kết cuối năm. | M6, M13 |

### 1.2. Tiền & chứng từ

| Mã | Quy tắc | Cài đặt |
|---|---|---|
| BR-05 | Tiền là số nguyên VND > 0 (tối đa 100 tỷ). | `MoneyRule`, CHECK trong DB |
| BR-06 | Giao dịch **> 500.000đ** bắt buộc chứng từ; ≤ 500.000đ tùy chọn. | `EvidenceRule.EnsureProvided` |
| BR-07 | File lưu trên Neon Object Storage, giới hạn theo loại: hóa đơn & bill chuyển khoản (ảnh/PDF ≤ 5 MB), ảnh đại diện (ảnh ≤ 2 MB), tài liệu (PDF/Word/Excel/ảnh ≤ 10 MB). Khóa lưu ngẫu nhiên `{kind}/{fundId}/{guid}.ext`, không dùng tên file gốc làm đường dẫn; tải file qua API có kiểm quyền. | `FileRule`, `StorageKeys`, `IFileStorage` |
| BR-08 | Sổ thu chi chỉ ghi thêm. Sửa sai = bút toán đảo (`ReversesEntryId`) + lý do; một dòng chỉ bị đảo một lần. | Trigger append-only (migration), M7 |
| BR-09 | Ngày giao dịch không ở tương lai (theo giờ VN). | `VietnamTime`, M7 |
| BR-38 | **Chi ≤ thu** (OQ-18): ghi chi bị từ chối nếu làm tổng chi của phạm vi (quỹ chung hoặc sự kiện) vượt tổng thu đã ghi sổ, tức số tiền chi ≤ số dư hiện có của phạm vi. Kiểm tra trong cùng transaction, khóa theo phạm vi để tránh ghi song song. | `SpendingRule.EnsureWithinBalance`, M7 |
| BR-39 | Ghi **thu** không nhập tay: thủ quỹ tạo khoản thu (FR-18 / FR-19); `LedgerEntry` thu chỉ sinh khi thanh toán được xác nhận (BR-10, BR-12, BR-15). | M6, M7 |

### 1.3. Thanh toán

| Mã | Quy tắc | Cài đặt |
|---|---|---|
| BR-10 | Học sinh: gửi xác nhận (Submitted) → thủ quỹ kiểm tra → Confirmed / Rejected (bắt buộc lý do). Chỉ Confirmed mới sinh `LedgerEntry` thu. | M6 |
| BR-11 | Chuyển khoản phải có ảnh minh chứng (Attachment `PaymentProof`); tiền mặt không bắt buộc. | M6 |
| BR-12 | Thủ quỹ tự nộp cho mình: gửi + tự xác nhận trong một thao tác, `ConfirmationMode = SelfDeclared`, `LedgerEntry.IsSelfDeclared = true`, audit `IsSelfAction = true`. Không cần thủ quỹ khác. | `PaymentConfirmationPolicy` |
| BR-13 | Báo cáo tách riêng thu tự ghi nhận của thủ quỹ (OQ-01). Thủ quỹ chịu trách nhiệm bù nếu khai sai. | M13 |
| BR-14 | Mỗi nghĩa vụ chỉ có một lần nộp đang chờ/đã xác nhận; bị từ chối được nộp lại (OQ-06). | Unique index `UX_Payment_OneActivePerAssignment` |
| BR-15 | Đóng bằng số dư cá nhân (khoản thu **chung hoặc sự kiện**, OQ-10): số dư ≥ số phải đóng và không có yêu cầu rút đang chờ; trừ số dư + `BalanceTransaction(ChargePayment)` + Payment `PersonalBalance` xác nhận ngay (tiền đã nằm trong tay thủ quỹ) + `LedgerEntry` thu vào đúng phạm vi của khoản thu – cùng một transaction. | `WithdrawalRule.EnsureCanSpend`, M6 + M10 |

### 1.4. Sự kiện & quyết toán

| Mã | Quy tắc | Cài đặt |
|---|---|---|
| BR-16 | Vòng đời (OQ-18): `Open → Completed → Settled`; `Open → Cancelled` (chỉ khi chưa có tiền thu/chi). **Không còn `ListLocked`.** Không quay lui. | `EventStatusRules` |
| BR-17 | Tạo sự kiện ⇒ mời mọi thành viên Active (EventParticipant `Pending`). Thành viên thêm sau được mời khi sự kiện còn `Open`. Đổi trả lời được khi còn `Open`. | M8 |
| BR-18 | Khoản thu sự kiện chỉ tạo khi `Open`, áp cho người `Accepted` (OQ-18). Màn quản lý chỉ liệt kê người `Accepted` (không hiện người từ chối / chưa trả lời). | M6, M8 |
| BR-19 | Kết thúc (`Open → Completed`, OQ-18): người `Accepted` **chưa đóng khoản nào** (không có nghĩa vụ `Paid`, không có thanh toán đang chờ – kể cả khi chưa có khoản thu nào áp cho họ; nhóm chốt 2026-11-19) bị loại (`IsFinal = false`, `ExclusionReason`), nghĩa vụ → `Excluded`, nhận thông báo. Người còn lại `IsFinal = true`; ai chưa đóng đủ nhận thông báo nhắc và vẫn nộp được / được xác nhận phần còn lại. | M8 |
| BR-20 | Từ `Completed`: không tạo khoản thu, không ghi chi mới (vẫn nhận thanh toán cho nghĩa vụ đã có). Sau `Settled` hoặc `Cancelled`: không nhận tiền nào nữa. | `EventStatusRules.AcceptsNewChargesAndExpenses`, `EventStatusRules.AcceptsPayments` |
| BR-21 | Quyết toán chỉ khi `Completed`; tính trên người `IsFinal`; tổng thu = thu đã xác nhận của sự kiện (gồm tự ghi nhận), tổng chi = chi của sự kiện (trừ bút toán đảo). Do BR-38, tổng thu ≥ tổng chi nên chỉ có tiền thừa (hoặc vừa đủ). | M9 |
| BR-22 | Tiền thừa chia **theo tỷ lệ số tiền mỗi người `IsFinal` đã đóng** cho sự kiện (OQ-18, không có chia đều); tổng chia = đúng số thừa (largest remainder). Hoàn trực tiếp → `LedgerEntry` chi `Source=Refund` khi thủ quỹ xác nhận đã hoàn từng người. Cộng số dư → `BalanceTransaction(SettlementCredit)` ngay. | M9 + M10 |
| BR-23 | ~~Thiếu: tạo khoản thu `Supplementary`~~ – **bỏ** (OQ-18). OQ-19 (đã duyệt): khi quyết toán, nghĩa vụ còn `Unpaid` → `Excluded`; phải xử lý hết (xác nhận hoặc từ chối) thanh toán đang chờ trước khi quyết toán, còn một khoản chờ thì chặn quyết toán. Tiền thừa chỉ chia theo số đã đóng thực tế, không truy thu phần nợ. | M9 |
| BR-24 | Mỗi sự kiện quyết toán một lần (unique `Settlement.EventId`). | DB |

### 1.4b. Quyết toán cuối năm quỹ chung (OQ-13)

| Mã | Quy tắc | Cài đặt |
|---|---|---|
| BR-34 | Quyết toán quỹ chung làm **một lần duy nhất** mỗi quỹ (`Settlement` có `EventId = null`, unique theo quỹ). | `UX_Settlement_OneYearEndPerFund` |
| BR-35 | Điều kiện: quỹ `Active` và mọi sự kiện đã `Settled` hoặc `Cancelled`. | `YearEndRule.EnsureCanClose` |
| BR-36 | Người được chia: mọi thành viên `Active` của quỹ (cả lớp, gồm thủ quỹ). Tổng thu/chi chỉ tính giao dịch quỹ chung. Thừa → hoàn trực tiếp / cộng số dư, chia theo tỷ lệ tổng đã đóng cả năm vào các khoản thu chung (OQ-19, đã duyệt 2026-11-18; trước đây có chọn chia đều và thiếu → khoản thu bổ sung chung). Cách tính dùng chung `SettlementCalculator` với quyết toán sự kiện. | M14 |
| BR-37 | Xong quyết toán → quỹ `Archived`: không nhận khoản thu, giao dịch, sự kiện, thành viên mới (trừ xác nhận hoàn tiền); vẫn xem báo cáo, **học sinh gửi yêu cầu rút số dư cá nhân và thủ quỹ xác nhận đã trả (OQ-11)**. | `YearEndRule.EnsureFundActive` |

### 1.5. Số dư cá nhân

| Mã | Quy tắc | Cài đặt |
|---|---|---|
| BR-25 | Số dư theo (quỹ, người), không âm, độc lập quỹ chung; mọi thay đổi có `BalanceTransaction` (append-only) ghi `BalanceAfter`. | CHECK + xmin concurrency |
| BR-26 | Rút tiền (OQ-11): rút **một lần toàn bộ** số dư; số tiền yêu cầu = số dư lúc gửi; mỗi người một yêu cầu đang chờ / quỹ; trong khi chờ số dư bị giữ (không dùng đóng khoản thu). Thủ quỹ xác nhận đã trả ⇒ trừ đúng số tiền yêu cầu + `BalanceTransaction(Withdrawal)` + audit; từ chối ⇒ bắt buộc lý do, số dư mở lại. **Không** ghi `LedgerEntry`. | `WithdrawalRule`, `UX_WithdrawalRequest_OnePendingPerUser`, M10 |

> Ghi chú BR-26: số dư cá nhân là tiền thủ quỹ **giữ hộ** (hình thành khi quyết toán sự kiện / cuối năm), không thuộc quỹ chung cũng không thuộc sự kiện. Báo cáo có mục riêng "Tiền giữ hộ = tổng số dư cá nhân". Khi số dư được dùng đóng khoản thu, tiền "chuyển" vào phạm vi của khoản thu đó (BR-15).

### 1.6. Tài khoản & thành viên

| Mã | Quy tắc | Cài đặt |
|---|---|---|
| BR-27 | Email là định danh, chuẩn hóa chữ thường, duy nhất toàn hệ thống. | `EmailAddress`, `UX_User_Email` |
| BR-28 | Mật khẩu 8–128 ký tự, có chữ và số. Mật khẩu tạm 16 ký tự ngẫu nhiên, hiển thị một lần. | `PasswordPolicy`, `Credentials.TemporaryPassword` |
| BR-29 | Đổi mật khẩu / cấp lại mật khẩu tạm / khóa tài khoản ⇒ đổi `SecurityStamp` ⇒ các phiên cũ mất hiệu lực. | `IdentityStore` |
| BR-30 | Ngừng thành viên không xóa dữ liệu lịch sử; thành viên `Removed` mất quyền truy cập quỹ. | `AccessService` |

### 1.7. Thông báo & audit

| Mã | Quy tắc | Cài đặt |
|---|---|---|
| BR-31 | Thông báo tạo cùng transaction với nghiệp vụ; tiêu đề có tiền tố `[Quỹ chung]` / `[Sự kiện: tên]`. | `Notifier` |
| BR-32 | Thông báo khoản thu sự kiện chỉ gửi người tham gia (người có nghĩa vụ). | M6 |
| BR-33 | Mọi thao tác ghi (trừ đọc) ghi `AuditLog` cùng transaction: actor, phạm vi, sự kiện, entity, before/after (JSON), `IsSelfAction`, lý do. Không ghi mật khẩu, hash, token. | `AuditTrail` |

## 2. Quy trình

### 2.1. Thanh toán khoản thu (BR-10 → BR-15)

```mermaid
flowchart LR
  A[Khoản thu tạo<br/>ChargeAssignment: Unpaid] --> B{Người nộp là ai?}
  B -- Học sinh --> C[Gửi xác nhận<br/>Payment: Submitted<br/>Assignment: AwaitingConfirmation]
  C --> D{Thủ quỹ kiểm tra<br/>tiền mặt / minh chứng}
  D -- Hợp lệ --> E[Confirmed · Verified<br/>Assignment: Paid<br/>LedgerEntry thu]
  D -- Không hợp lệ --> F[Rejected + lý do<br/>Assignment: Unpaid] --> C
  B -- Thủ quỹ tự nộp --> G[Tự ghi nhận<br/>Confirmed · SelfDeclared<br/>LedgerEntry IsSelfDeclared]
  B -- Dùng số dư<br/>(chung hoặc sự kiện) --> H[Trừ số dư + BalanceTransaction<br/>Confirmed]
  E & G & H --> I[AuditLog + Thông báo]
```

### 2.2. Vòng đời sự kiện

```mermaid
stateDiagram-v2
  [*] --> Open: tạo (mời mọi thành viên)
  Open --> Cancelled: hủy (chưa có tiền)
  Open --> Completed: kết thúc (loại người chưa đóng khoản nào)
  Completed --> Settled: quyết toán xong
  Settled --> [*]
  Cancelled --> [*]
```

Trong `Open`: học sinh trả lời tham gia; thủ quỹ tạo khoản thu, ghi chi (chi ≤ số dư sự kiện, BR-38); học sinh nộp tiền.
Khi **kết thúc** (BR-19): loại người chưa đóng khoản nào; thông báo nhắc người chưa đóng đủ.
Trong `Completed`: không tạo khoản thu, không ghi chi; người chưa đóng đủ vẫn nộp, thủ quỹ vẫn xác nhận thanh toán.

### 2.3. Quyết toán sự kiện

1. Sự kiện `Completed` → thủ quỹ mở màn hình quyết toán, hệ thống tính: số người tham gia (`IsFinal`), tổng thu, tổng chi, tiền thừa (≥ 0 do BR-38).
2. **Vừa đủ** (thừa = 0) → xác nhận → `Settled`.
3. **Thừa** → chọn phương án (hoàn trực tiếp / cộng số dư); phần mỗi người tính theo tỷ lệ số tiền đã đóng (BR-22) → xem trước → xác nhận:
   - Cộng số dư: cộng ngay, thông báo `BalanceCredited`.
   - Hoàn trực tiếp: `SettlementLine` chờ hoàn; thủ quỹ xác nhận từng người đã nhận tiền (ghi chi `Refund`).
4. Ghi `Settlement` (EventId = sự kiện), audit, thông báo `SettlementResult` cho người tham gia → sự kiện `Settled`.

### 2.4. Rút số dư (rút toàn bộ một lần)

1. Người dùng (học sinh hoặc thủ quỹ) mở "Số dư cá nhân", bấm **"Rút tiền"** → hệ thống tạo yêu cầu với số tiền = toàn bộ số dư; số dư chuyển trạng thái "đang chờ rút".
2. Thủ quỹ nhận thông báo, liên hệ người dùng và trả tiền mặt / chuyển khoản ngoài hệ thống.
3. Thủ quỹ xác nhận **"Đã hoàn tiền"** → trừ số dư, `BalanceTransaction(Withdrawal)`, audit; hoặc **từ chối** + lý do → số dư mở lại.
4. Người dùng nhận thông báo `WithdrawalProcessed`.

Thủ quỹ rút số dư của chính mình: tự xác nhận được (giống BR-12), audit `IsSelfAction = true`.

### 2.6. Quyết toán cuối năm quỹ chung

1. Mọi sự kiện đã quyết toán / hủy → thủ quỹ mở "Tổng kết cuối năm": hệ thống tổng hợp tổng thu, tổng chi, số dư quỹ chung, khoản chưa hoàn thành, số thành viên.
2. Chọn phương án như quyết toán sự kiện (mục 2.3) – người được chia là **cả lớp**, theo tỷ lệ số tiền đã đóng vào quỹ chung (OQ-19, đã duyệt), tính trên tổng đã đóng cả năm.
3. Xác nhận → `Settlement` (EventId null), thông báo `SettlementResult` cho cả lớp (phạm vi Quỹ chung), quỹ chuyển `Archived`.

### 2.5. Thêm học sinh

Thủ quỹ nhập email (+ họ tên) → email mới: tạo User `Provisioned` + mật khẩu tạm (hiện một lần) → FundMember `Student` → học sinh đăng nhập, bắt buộc đổi mật khẩu. Email đã có tài khoản: chỉ thêm FundMember (không cấp mật khẩu).
