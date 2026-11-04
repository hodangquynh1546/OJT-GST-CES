# Screen Specifications

> Phiên bản: 0.1 (bản nháp, chờ duyệt) · Ngày: 2026-11-03
> Quy ước: **Quyền** = permission cần có để vào màn hình (hành động có permission riêng ghi trong ngoặc). **Phạm vi** = dữ liệu được thấy. Hành động "có điều kiện" bị vô hiệu kèm lý do khi điều kiện chưa đủ (ui-design-overview mục 4). Q-xx = câu hỏi nghiệp vụ đang mở; UQ-xx = câu hỏi UI.

---

## 1. Chung (COM)

### COM-01 Đăng nhập
- **Quyền:** không. **FR:** FR-ADM-05.
- **Hiển thị:** email, mật khẩu, "Quên mật khẩu?".
- **Thao tác:** đăng nhập; tài khoản `Locked` / `Disabled` → thông báo liên hệ quản trị.
- **Kendo:** `kendo-textbox`, `kendo-formfield`, `kendo-button`.

### COM-02 Kích hoạt tài khoản từ lời mời
- **Quyền:** liên kết mời hợp lệ (chưa hết hạn, chưa dùng – NFR-07). **FR:** FR-ADM-04.
- **Hiển thị:** email được mời (chỉ đọc), vai trò và lớp / học sinh được mời; họ tên, số điện thoại, mật khẩu, nhập lại mật khẩu.
- **Thao tác:** kích hoạt → vào trang đích. Liên kết hết hạn / đã dùng → thông báo, hướng dẫn xin lời mời mới.

### COM-03 Quên / đặt lại mật khẩu
- **Quyền:** không. **FR:** FR-ADM-05. Luôn trả cùng một thông báo, dù email có tồn tại hay không.

### COM-04 Hồ sơ cá nhân
- **Quyền:** đã đăng nhập. **FR:** FR-ADM-05, FR-CLS-01. **Hiển thị:** họ tên, email, số điện thoại, **danh sách vai trò và phạm vi hiện có** (chỉ đọc). **Thao tác:** sửa họ tên / số điện thoại, đổi mật khẩu.

### COM-05 Thông báo
- **Quyền:** đã đăng nhập. **FR:** FR-SYS-03, FR-REC-11.
- **Hiển thị:** danh sách (`kendo-listview`), chưa đọc in đậm, lọc theo loại.
- **Thao tác:** mở liên kết liên quan, đánh dấu đã đọc / tất cả đã đọc.

### COM-06 Việc cần xử lý
- **Quyền:** có ít nhất một permission duyệt / xác nhận. **Phạm vi:** theo permission và phạm vi của người dùng. **FR:** FR-EXP-02, FR-REC-07 (duyệt), FR-REC-08, FR-REC-13, FR-REC-19, FR-REC-20, FR-RPT-09.
- **Hiển thị:** `kendo-tabstrip`, mỗi tab có số đếm; chỉ hiện tab người dùng có quyền.

| Tab | Nguồn | Permission | Người thường thấy |
|---|---|---|---|
| Phiếu chi chờ duyệt | `ExpenseVoucher` `PendingApproval` | `Expense.Approve` | GVCN |
| Hoàn trả chờ duyệt | `RefundVoucher` `PendingApproval` | `Refund.Approve` | GVCN |
| Miễn giảm chờ duyệt | `Exemption` `PendingApproval` | `Exemption.Approve` | GVCN |
| Phương án chênh lệch chờ duyệt | `DiscrepancyResolution` `Proposed` | `HandoverDiscrepancy.Approve` | GVCN lớp gốc / Quản trị |
| Chuyển quỹ chờ duyệt | `FundTransfer` `PendingApproval` | `FundTransfer.Approve` | Quản trị |
| Đảo giao dịch chờ duyệt | `FinancialAdjustment` `PendingApproval` | `Reversal.Approve` | Q-49 |
| Chuyển khoản chờ xác nhận | `CollectionReceipt` `PendingConfirmation` | `Receipt.ConfirmTransfer` | Thủ quỹ |
| Bàn giao chờ tôi xác nhận | `CashHandover`, `StudentBalanceHandover` `PendingConfirmation` có `ToUserId` = tôi | `CashHandover.Confirm`, `BalanceHandover.Confirm` | Thủ quỹ, người giữ quỹ |
| Hoàn trả chờ thực hiện | `RefundVoucher` `AwaitingPayment` | `Refund.ConfirmPayment` | Thủ quỹ |

- **Thao tác:** bấm dòng → FND-06 / FND-07 để duyệt. Duyệt nhanh ngay trên lưới chỉ áp dụng cho phiếu chi, hoàn trả, miễn giảm, chuyển quỹ: vẫn mở `kendo-dialog` tóm tắt và yêu cầu xác nhận.
- **Quy tắc UI:** chứng từ do chính mình lập vẫn hiện nhưng nút duyệt bị vô hiệu: "Bạn là người lập" (nguyên tắc phân quyền 2, D-50).

### COM-07 Không có quyền / không tìm thấy
- Thông điệp chung, không tiết lộ dữ liệu có tồn tại hay không; nút về trang đích.

---

## 2. Admin (ADM)

### ADM-01 Tổng quan toàn trường
- **Quyền:** `Report.ViewSummary` (toàn trường). **FR:** FR-RPT-12. Chỉ số: UQ-04.
- **Hiển thị:**
  - Bộ lọc năm học.
  - Thẻ: số lớp, số học sinh, tổng số dư quỹ các lớp, số việc chờ Quản trị duyệt.
  - Lưới lớp: lớp, GVCN, thủ quỹ, trạng thái sổ (Mở / Đã khóa), số dư, giữ chỗ, khả dụng, tỷ lệ đã thu.
  - Cảnh báo: lớp đã kết thúc năm học nhưng **chưa có người giữ quỹ** (BR-CASH-04); bàn giao `Discrepancy` của lớp đã khóa sổ / không còn GVCN.
- **Thao tác:** mở lớp ở chế độ **chỉ xem** (FND-04, FND-05); xuất Excel (`Report.Export`).

### ADM-02 Năm học
- **Quyền:** `SchoolYear.Manage`. **FR:** FR-ADM-01.
- **Hiển thị:** lưới mã năm học, ngày bắt đầu / kết thúc, trạng thái (Dự kiến / Đang diễn ra / Đã kết thúc).
- **Thao tác:** tạo, sửa, chuyển trạng thái (xác nhận khi chuyển sang "Đã kết thúc").

### ADM-03 Lớp & phân công GVCN
- **Quyền:** `Class.Manage`. **FR:** FR-ADM-01.
- **Hiển thị:** lọc theo năm học; lưới tên lớp, GVCN hiện tại, thủ quỹ, sĩ số, trạng thái sổ. Bảng phụ "Lịch sử phân công" của từng lớp (`ClassAssignment`).
- **Thao tác:**
  - Tạo / sửa lớp.
  - Gán / đổi GVCN (`kendo-combobox` tìm tài khoản GVCN). Đổi GVCN thì kết thúc phân công cũ, lịch sử giữ nguyên (BR-ACC-06).
  - Nếu GVCN chưa có tài khoản → mời (ADM-04).

### ADM-04 Tài khoản & lời mời GVCN
- **Quyền:** `Invitation.SendTeacher`. **FR:** FR-ADM-02.
- **Hiển thị:** tab "Tài khoản" (họ tên, email, vai trò, trạng thái); tab "Lời mời" (email, lớp, trạng thái, hạn).
- **Thao tác:** mời GVCN (email + lớp), gửi lại / thu hồi lời mời đang chờ.

### ADM-05 Chuyển lớp / lên lớp
- **Quyền:** `Student.Transfer`. **FR:** FR-ADM-07. **BR:** BR-ACC-08, BR-ACC-10, BR-BAL-05.
- **Hiển thị:** chọn lớp nguồn → danh sách học sinh đang học. Mỗi học sinh có cột **số dư cá nhân** và **nợ chưa thanh toán** để Quản trị biết điều gì sẽ đi theo học sinh.
- **Thao tác:** chọn một hoặc nhiều học sinh (chọn nhiều: UQ-01) → chọn lớp đích, loại (Chuyển lớp giữa năm / Lên lớp), ngày hiệu lực → `kendo-stepper` 3 bước: Chọn → Kiểm tra ảnh hưởng → **Xác nhận** (DLG-10).
- **Quy tắc UI:**
  - Màn hình kiểm tra ảnh hưởng liệt kê: nợ cũ vẫn thuộc lớp gốc; số dư cá nhân chuyển sang "chờ bàn giao"; phiếu hoàn trả đang chờ (Q-56).
  - Thao tác này là **xác nhận cuối cùng** (D-56); không có bước GVCN lớp mới xác nhận.

### ADM-06 Người giữ quỹ lớp cũ & quyền cấp riêng
- **Quyền:** `FundCustodian.Assign`. **FR:** FR-ADM-08.
- **Hiển thị:**
  - Lưới các lớp – năm học **đã kết thúc**: người giữ quỹ hiện tại, tiền thu hộ chưa bàn giao về lớp này.
  - Cảnh báo lớp chưa có người giữ quỹ.
  - Lịch sử phân công.
- **Thao tác:**
  - Phân công / đổi người giữ quỹ: chọn thủ quỹ cũ hoặc tài khoản khác (D-45); ghi rõ lớp và năm học.
  - Khối **quyền cấp riêng** cho người này, theo phạm vi lớp đó: `Receipt.CollectFormerStudent`, `Report.Export`, `Report.ViewStudentIdentity`. Mỗi quyền là một công tắc (`kendo-switch`) kèm ô lý do; mặc định tắt (D-45, D-52).

### ADM-07 Danh mục chi chung
- **Quyền:** `ExpenseCategory.ManageShared`. **FR:** FR-ADM-06.
- **Hiển thị / thao tác:** lưới tên danh mục, trạng thái dùng; thêm, sửa, ngừng dùng (không xóa).

### ADM-08 Nhật ký thao tác & truy cập
- **Quyền:** `AuditLog.View`. **Phạm vi:** Quản trị toàn trường; GVCN lớp mình. **FR:** FR-SYS-02, FR-SYS-04.
- **Hiển thị:** lọc thời gian, người thực hiện, loại (Thay đổi / Truy cập / Bảo mật), đối tượng, lớp. Lưới: thời điểm, người, hành động, đối tượng (liên kết), lý do; mở rộng dòng để xem giá trị cũ / mới.
- **Quy tắc UI:** cột tên học sinh chỉ hiện khi có `Report.ViewStudentIdentity`; mở màn hình được ghi lượt truy cập (D-46).

---

## 3. GVCN (TCH)

### TCH-01 Tổng quan lớp (GVCN)
- **Quyền:** `Report.ViewSummary` (lớp chủ nhiệm). **FR:** FR-RPT-01, FR-CLS-01. Chỉ số: UQ-04.
- **Hiển thị:**
  - Khối số dư quỹ (số dư / giữ chỗ / **khả dụng**).
  - Thẻ: số việc chờ GVCN duyệt, tỷ lệ đã thu theo đợt đang mở, số học sinh quá hạn.
  - Biểu đồ thu / chi theo tháng (`kendo-chart`).
  - Thủ quỹ hiện tại; trạng thái sổ.
- **Thao tác:** đi tới COM-06, FND-01, FND-05; tạo đợt thu (`Collection.Manage`).

### TCH-02 Học sinh & phụ huynh
- **Quyền:** `Student.Manage`. **FR:** FR-CLS-02, FR-CLS-03, FR-ADM-03.
- **Hiển thị:** lưới mã, họ tên, ngày sinh, trạng thái, phụ huynh đã liên kết (số lượng + tên), trạng thái lời mời.
- **Thao tác:**
  - Thêm / sửa học sinh; **đánh dấu nghỉ học** (xác nhận, có lý do).
  - Mời phụ huynh (DLG-01); liên kết phụ huynh đã có tài khoản; gỡ liên kết (lịch sử giữ nguyên).
  - Không có thao tác chuyển lớp: hiện ghi chú "Chuyển lớp / lên lớp do Quản trị thực hiện" (D-23).

### TCH-03 Phân công thủ quỹ
- **Quyền:** `Treasurer.Assign`. **FR:** FR-CLS-04.
- **Hiển thị:** thủ quỹ hiện tại, lịch sử phân công.
- **Thao tác:** chọn **một** phụ huynh có con trong lớp (`kendo-combobox` chỉ liệt kê phụ huynh hợp lệ – BR-ACC-04); xác nhận đổi người (người cũ mất quyền ngay, phiếu đã ghi giữ tên – BR-ACC-06).

### TCH-04 Chuyển quỹ
- **Quyền:** `FundTransfer.Request`. **FR:** FR-RPT-09, FR-RPT-10.
- **Hiển thị:** lưới đề nghị chuyển đi / nhận về: lớp kia, số tiền, lý do, trạng thái, loại (Thường / **Điều chỉnh sau khóa sổ**).
- **Thao tác:**
  - Lập đề nghị: chọn lớp nhận (`kendo-combobox` toàn trường), số tiền, lý do (bắt buộc). Form hiển thị **khả dụng hiện tại** và khả dụng còn lại sau khi giữ chỗ.
  - Hủy đề nghị của mình khi còn chờ (DLG-08).
- **Quy tắc UI:** quỹ đã khóa sổ → form tự chuyển sang loại "Chứng từ điều chỉnh sau khóa sổ", hiện cảnh báo, lý do bắt buộc (D-30).

### TCH-05 Chốt sổ năm học
- **Quyền:** `Fund.Close`. **FR:** FR-RPT-08. **BR:** BR-CLS-01 → 04.
- **Hiển thị:** `kendo-stepper` 3 bước:
  1. **Kiểm tra chứng từ chờ:** danh sách theo loại (phiếu chi, chuyển quỹ, phiếu thu chờ xác nhận, miễn giảm, bàn giao, điều chỉnh). Mỗi loại có liên kết xử lý; còn chứng từ chờ thì không qua được bước 1.
  2. **Đối chiếu:** số dư quỹ, tổng số dư cá nhân học sinh của lớp, tiền thu hộ chưa bàn giao, tiền thủ quỹ đang giữ; ô xác nhận "Đã đối chiếu với tiền thực tế".
  3. **Xác nhận chốt sổ:** nêu rõ hệ quả: số dư quỹ **giữ lại** tại lớp – năm học; nợ chưa thanh toán và số dư cá nhân đi theo học sinh; không mở lại sổ được.
- **Thao tác:** chốt sổ (xác nhận lần hai bằng `kendo-dialog`).

---

## 4. Thủ quỹ (TRS)

### TRS-01 Tổng quan thủ quỹ
- **Quyền:** `Report.ViewSummary` (lớp làm thủ quỹ). **FR:** FR-RPT-01, FR-RPT-13, FR-CLS-01. Chỉ số: UQ-04.
- **Hiển thị:**
  - Khối số dư quỹ (số dư / giữ chỗ / **khả dụng**).
  - Thẻ "Tiền tôi đang giữ": tiền của quỹ lớp, tiền **thu hộ chưa bàn giao** (theo từng lớp gốc), tiền số dư cá nhân của học sinh.
  - Thẻ việc chờ: chuyển khoản chờ xác nhận, bàn giao chờ tôi xác nhận, hoàn trả chờ thực hiện.
  - Đợt thu đang mở (tỷ lệ đã thu).
- **Thao tác:** nút nổi "Ghi thu" (TRS-02); lối tắt tới TRS-03, TRS-05, TRS-07.

### TRS-02 Ghi phiếu thu (kể cả thu hộ, thu hộ sau khóa sổ)
- **Quyền:** `Receipt.Create` (nợ của lớp mình), `Receipt.CollectOnBehalf` (nợ cũ của học sinh đang học lớp mình), `Receipt.CollectFormerStudent` (nợ của lớp mình, học sinh đã chuyển đi). **FR:** FR-REC-03, FR-REC-04, FR-REC-05, FR-REC-14, FR-REC-16, FR-REC-17, FR-REC-18. Ngày nộp trong quá khứ: UQ-13.
- **Hiển thị / các bước** (một trang, chia khối):
  1. **Học sinh:** `kendo-combobox` tìm theo tên / mã, trong phạm vi quyền (Q-53: một học sinh / phiếu).
  2. **Khoản nợ:** lưới các khoản nợ còn có thể thu của học sinh, gồm:
     - Nợ lớp hiện tại.
     - **Nợ cũ**, có nhãn "Nợ cũ – 6A / 2026-2027"; nếu lớp gốc đã khóa sổ thêm nhãn "Sau khóa sổ".
     - Cột: đợt thu, phải nộp, đã nộp, đang chờ xác nhận, **còn có thể thu**.
     - Khoản có số còn thu = 0 bị **vô hiệu** (D-54).
  3. **Số tiền & phân bổ:** tổng tiền nhận, hình thức (Tiền mặt / Chuyển khoản), ngày nộp, người nộp. Phân bổ vào từng khoản đã chọn; gợi ý tự động (UQ-11), sửa được.
  4. **Xem trước ảnh hưởng:** "Trả nợ: 500.000 đ (quỹ 6A / 2026-2027 – thu hộ)" · "Vào số dư cá nhân: 100.000 đ". Nếu toàn bộ tiền vào số dư cá nhân (không còn nợ) → cảnh báo rõ (D-54).
  5. Ghi chú, đính kèm (bắt buộc với chuyển khoản).
- **Thao tác:** lưu → phiếu `Confirmed`; mở FND-06 của phiếu vừa tạo.
- **Quy tắc UI:**
  - Phân bổ của một khoản ≤ số còn có thể thu; tổng phân bổ ≤ tổng tiền nhận.
  - Số liệu được tải lại ngay trước khi lưu; nếu số còn có thể thu đã thay đổi (người khác vừa thu) → thông báo xung đột (ui-design-overview mục 8).
  - Khi người thu **chính là** người giữ quỹ lớp gốc (D-58): hiện ghi chú "Không cần bàn giao". Ngược lại: "Cần bàn giao X đ về lớp 6A / 2026-2027" kèm liên kết TRS-07.
  - Thủ quỹ lớp cũ thu vượt nợ của học sinh đã chuyển đi: cảnh báo "Phần vượt sẽ được bàn giao số dư cá nhân cho thủ quỹ lớp mới" (D-55).

### TRS-03 Xác nhận chuyển khoản
- **Quyền:** `Receipt.ConfirmTransfer`. **FR:** FR-REC-08.
- **Hiển thị:** danh sách báo chuyển khoản `PendingConfirmation` của học sinh trong phạm vi: học sinh, người báo, số tiền, ngày, khoản nợ, **ảnh biên lai** (xem phóng to); khoản nợ tương ứng đang được **giữ chỗ**.
- **Thao tác:** xác nhận (hiển thị phần trả nợ và phần vào số dư cá nhân trước khi xác nhận); từ chối (DLG-07).

### TRS-04 Thu khác
- **Quyền:** `OtherIncome.Create`. **FR:** FR-REC-12.
- **Hiển thị:** lưới nguồn thu, số tiền, ngày, chứng từ, trạng thái (kể cả "Đã đảo").
- **Thao tác:** thêm (nguồn, số tiền, ngày, **chứng từ bắt buộc**); không cần duyệt. Sai thì đảo giao dịch (DLG-06, Q-49).

### TRS-05 Phiếu chi
- **Quyền:** `Expense.Create` (lập), `Expense.View` (xem). **FR:** FR-EXP-01, FR-EXP-03, FR-EXP-04, FR-EXP-05. Ngày chi trong quá khứ: UQ-13.
- **Hiển thị:** lưới nội dung, danh mục, số tiền, ngày, trạng thái, người duyệt; lọc theo trạng thái / danh mục.
- **Thao tác:**
  - Lập phiếu: nội dung, người nhận, danh mục (chung + của lớp), số tiền, ngày chi, **chứng từ bắt buộc**. Hiển thị **khả dụng hiện tại** và "khả dụng sau khi giữ chỗ"; vượt thì chặn (D-17, D-40).
  - Sửa phiếu chờ duyệt / bị từ chối (gửi lại); hủy phiếu của mình khi còn chờ (DLG-08).
- **Quy tắc UI:** phiếu đã duyệt chỉ có "Đảo giao dịch" (Q-49); bước "đã chi tiền" chưa có (Q-50).

### TRS-06 Phiếu hoàn trả
- **Quyền:** `Refund.Create`, `Refund.ConfirmPayment`. **FR:** FR-REC-13.
- **Hiển thị:** lưới học sinh, số tiền, lý do, trạng thái (Chờ duyệt / **Chờ thực hiện hoàn trả** / Hoàn tất / …).
- **Thao tác:**
  - Lập phiếu: chọn học sinh có số dư cá nhân **khả dụng** > 0; số tiền ≤ khả dụng; hiển thị khối số dư cá nhân (thực tế / giữ chỗ / chờ bàn giao / khả dụng).
  - Hủy phiếu của mình khi còn chờ duyệt.
  - Với phiếu `AwaitingPayment`: "Xác nhận đã trả tiền" (DLG-05). Hủy sau duyệt: Q-49.

### TRS-07 Bàn giao tiền thu hộ
- **Quyền:** `CashHandover.Create` (lập), `CashHandover.Confirm` (xác nhận khi là người nhận). **FR:** FR-REC-19, FR-REC-20.
- **Hiển thị:** tab "Tôi cần bàn giao" (tiền thu hộ chưa bàn giao, nhóm theo lớp gốc); tab "Tôi đã bàn giao"; tab "Chờ tôi nhận" (khi là thủ quỹ lớp gốc).
- **Thao tác:**
  - Chọn các khoản thu hộ của **một** lớp gốc → lập bàn giao. Người nhận được điền sẵn: thủ quỹ hiện tại của lớp gốc, hoặc người giữ quỹ được phân công nếu năm học đã kết thúc (D-43). Lớp đã kết thúc mà chưa có người giữ quỹ → chặn, kèm hướng dẫn liên hệ Quản trị (BR-CASH-04).
  - Hủy bàn giao của mình khi còn chờ.
  - Người nhận: DLG-03.
- **Quy tắc UI:** không chọn được chính mình làm người nhận (D-43).

### TRS-08 Bàn giao số dư cá nhân
- **Quyền:** `BalanceHandover.Create` (thủ quỹ lớp cũ), `BalanceHandover.Confirm` (thủ quỹ lớp mới). **FR:** FR-REC-15, FR-REC-17.
- **Hiển thị:** tab "Cần lập bàn giao" (học sinh đã chuyển khỏi lớp có số dư chờ bàn giao; phần thu vượt sau khi chuyển – D-55); tab "Chờ tôi nhận"; tab "Lịch sử".
- **Thao tác:** lập bàn giao (số dư trước / sau, số tiền, người nhận điền sẵn = thủ quỹ lớp mới); người nhận: DLG-03.
- **Quy tắc UI:** số dư chỉ chuyển sang **khả dụng** ở lớp mới sau khi xác nhận khớp; đang `Discrepancy` vẫn chưa khả dụng (BR-DIS-08).

### TRS-09 Danh mục chi của lớp
- **Quyền:** `ExpenseCategory.ManageClass`. **FR:** FR-EXP-05. Như ADM-07 nhưng phạm vi lớp; danh mục chung hiển thị chỉ đọc.

---

## 5. Quỹ lớp – dùng chung (FND)

### FND-01 Đợt thu
- **Quyền:** `Collection.View`; tạo / sửa / đóng / hủy: `Collection.Manage`. **FR:** FR-REC-01, FR-REC-02, FR-REC-06 (tự trừ số dư cá nhân khi tạo khoản nợ).
- **Hiển thị:** lưới tên, số tiền mỗi học sinh, hạn, trạng thái, đã thu / phải thu (thanh tiến độ), số học sinh chưa đủ.
- **Thao tác:**
  - Tạo đợt thu (`kendo-window`): tên, mô tả, số tiền, hạn, **chọn học sinh áp dụng** (mặc định tất cả học sinh đang học; bỏ chọn được; ít nhất một – D-34). Xem trước "N học sinh có số dư cá nhân sẽ được tự trừ".
  - Sửa / hủy khi chưa có phiếu thu hợp lệ; đóng đợt thu.
- **Quy tắc UI:** quỹ đã khóa sổ → không tạo được đợt thu mới.

### FND-02 Chi tiết đợt thu
- **Quyền:** `Collection.View`; danh sách có tên học sinh cần `Receipt.View` hoặc `Report.ViewStudentIdentity`. **FR:** FR-RPT-03, FR-REC-10, FR-REC-11.
- **Hiển thị:**
  - Tổng: phải thu, đã thu, miễn giảm, đang chờ xác nhận, còn thiếu.
  - Lưới từng học sinh: phải nộp, miễn giảm, đã nộp, chờ xác nhận, còn lại, tình trạng (chip), "Đã thu hộ bởi lớp …" nếu có.
  - Lọc theo tình trạng.
- **Thao tác:**
  - Ghi thu cho học sinh → TRS-02 (thủ quỹ).
  - Đề nghị miễn giảm (DLG-02; `Exemption.Request`).
  - Gửi nhắc nộp (DLG-09; `Reminder.Send`).
  - Xuất Excel / PDF (`Report.Export`).

### FND-03 Hồ sơ tài chính học sinh
- **Quyền:** `Student.View` + `Receipt.View` (GVCN, thủ quỹ); Quản trị xem qua báo cáo (D-52). **FR:** FR-RPT-05 (bản phụ huynh: PAR-02), FR-RPT-06.
- **Hiển thị:** `kendo-tabstrip`:
  - **Khoản nợ**: nợ hiện tại + **nợ cũ** (nhãn lớp – năm học gốc).
  - **Phiếu thu**: kể cả thu hộ, tự trừ số dư.
  - **Số dư cá nhân**: khối số dư + sổ số dư cá nhân.
  - **Bàn giao & hoàn trả**.
  - **Lịch sử lớp**: từ `StudentClassEnrollment`.
- **Thao tác:** ghi thu, đề nghị miễn giảm, lập hoàn trả (theo permission).

### FND-04 Sổ quỹ
- **Quyền:** `Report.ViewDetail` (phụ huynh: PAR-04). **FR:** FR-RPT-02, FR-RPT-11.
- **Hiển thị:**
  - Khối số dư quỹ.
  - Lưới bút toán theo thứ tự: ngày, loại, diễn giải, chứng từ nguồn (liên kết), thu (+), chi (−), **số dư sau giao dịch**, nhãn "Sau khóa sổ".
  - Bút toán thu hộ ghi "Thu hộ bởi 7A / 2027-2028".
  - Lọc theo thời gian / loại.
- **Thao tác:** xuất Excel / PDF; mở chứng từ (FND-06).
- **Quy tắc UI:** tên học sinh trong diễn giải chỉ hiện khi có quyền xem danh tính; Quản trị xem thì có dải "Lượt xem được ghi lại".

### FND-05 Báo cáo
- **Quyền:** `Report.ViewDetail`; xuất: `Report.Export`. **FR:** FR-RPT-03, FR-RPT-04, FR-RPT-06, FR-RPT-07, FR-RPT-13, FR-RPT-15. Mẫu xuất: UQ-07. Mức ghi lượt truy cập: UQ-08.
- **Danh sách báo cáo** (chọn bằng `kendo-dropdownlist`):

| Báo cáo | Nội dung |
|---|---|
| Theo đợt thu | Như FND-02, gộp nhiều đợt |
| Chi theo danh mục | Biểu đồ tròn + bảng, theo khoảng thời gian |
| Số dư cá nhân học sinh | **Tách riêng** khỏi quỹ lớp: thực tế, giữ chỗ, chờ bàn giao, khả dụng, lớp đang quản lý |
| Tiền ghi sổ & người đang giữ | Số dư quỹ trên sổ; tiền thu hộ chưa bàn giao (ai đang giữ); tiền bàn giao đang chênh lệch |
| Bàn giao | Chứng từ bàn giao theo trạng thái (chờ xác nhận / hoàn tất / có chênh lệch) |
| Trước / sau khóa sổ | Giao dịch sau khóa sổ: chuyển quỹ điều chỉnh, thu hộ bổ sung, điều chỉnh chênh lệch |

- **Quy tắc UI:** báo cáo có tên học sinh hiện dải thông báo ghi lượt truy cập (D-46); người giữ quỹ chỉ chọn được lớp được giao.

### FND-06 Chi tiết chứng từ
- **Áp dụng cho:** phiếu thu, thu khác, phiếu chi, phiếu hoàn trả, miễn giảm, chuyển quỹ, bàn giao tiền thu hộ, bàn giao số dư cá nhân, đảo giao dịch / điều chỉnh.
- **Quyền:** permission xem tương ứng + phạm vi. **FR:** FR-SYS-01 (dòng thời gian), FR-SYS-05 (hủy / đảo), FR-REC-09, FR-EXP-02, FR-EXP-03.
- **Hiển thị:**
  - Tiêu đề: loại chứng từ, chip trạng thái, chip "Đã đảo" / "Sau khóa sổ".
  - Thông tin chính; **ảnh hưởng tiền** (quỹ nào, số dư cá nhân, khoản nợ nào).
  - Đính kèm (xem / tải).
  - **Dòng thời gian** (`kendo-timeline`, từ `DocumentStatusHistory`).
  - **Chứng từ liên kết**: chứng từ đảo, chứng từ gốc, bàn giao chứa phiếu thu này, biên bản chênh lệch.
- **Thao tác** (thanh hành động, theo trạng thái + permission):

| Trạng thái | Hành động khả dĩ |
|---|---|
| Chờ duyệt / chờ xác nhận | Duyệt / Xác nhận; Từ chối (DLG-07); Hủy – chỉ người tạo (DLG-08); Sửa – người tạo, nếu loại cho sửa |
| Chờ thực hiện hoàn trả | Xác nhận đã trả (DLG-05); Hủy sau duyệt (Q-49) |
| Đã hoàn tất | **Đảo giao dịch** (DLG-06; Q-49) – không có Sửa / Xóa |
| Bàn giao chờ tôi nhận | Xác nhận nhận (DLG-03) |

- **Quy tắc UI:**
  - Quản trị xem phiếu thu: **ẩn** người nộp / thông tin biên nhận (không có `Receipt.View` – D-41, D-52).
  - Người lập không thấy nút duyệt hoạt động.

### FND-07 Biên bản chênh lệch
- **Quyền:** xem: liên quan tới bàn giao hoặc `Report.ViewDetail`; đề xuất: `HandoverDiscrepancy.Propose` (Q-47); duyệt: `HandoverDiscrepancy.Approve`. **FR:** FR-REC-20.
- **Hiển thị:**
  - Bàn giao gốc; số dự kiến, số thực nhận, **chênh lệch** (thiếu / thừa); lý do; người bàn giao / nhận; minh chứng.
  - Thanh tiến độ "Đã xử lý X / |chênh lệch|" (Q-54).
  - Danh sách phương án: loại, số tiền, mô tả, người đề xuất, trạng thái, người duyệt.
- **Thao tác:** đề xuất phương án (DLG-04); duyệt / từ chối phương án (người duyệt ≠ người đề xuất – D-50).
- **Quy tắc UI:**
  - Hiện rõ **ai là người duyệt**: "GVCN lớp gốc" hoặc "Quản trị (lớp gốc đã khóa sổ / không còn GVCN)".
  - Mỗi phương án hiện ảnh hưởng số dư dự kiến (theo giả định Q-48).

---

## 6. Người giữ quỹ (CUS)

### CUS-01 Lớp được giao giữ quỹ
- **Quyền:** `CashHandover.Confirm` + phân công `FundCustodian`. **FR:** FR-RPT-14, FR-REC-19.
- **Hiển thị:** thẻ cho mỗi lớp – năm học được giao: số dư quỹ (đã khóa sổ), tiền thu hộ chưa bàn giao về (ai đang giữ), bàn giao chờ tôi xác nhận, biên bản đang mở.
- **Thao tác:** xác nhận bàn giao (DLG-03); xem sổ quỹ / báo cáo của lớp đó; ghi thu nợ cũ chỉ khi được cấp `Receipt.CollectFormerStudent`; xuất báo cáo chỉ khi được cấp `Report.Export`.
- **Quy tắc UI:** không hiện bất kỳ chức năng nào khác của Thủ quỹ (D-45).

---

## 7. Phụ huynh (PAR) – thiết kế mobile-first

### PAR-01 Trang chủ phụ huynh
- **Quyền:** `Student.ViewOwn`. **Phạm vi:** một con (bộ chọn con – UQ-02). **FR:** FR-RPT-05, FR-RPT-01.
- **Hiển thị:**
  - Thẻ con: tên, lớp hiện tại.
  - Thẻ **"Cần nộp"**: tổng còn phải nộp, khoản gần hạn nhất, quá hạn (đỏ).
  - Thẻ **số dư cá nhân của con** (nếu > 0).
  - Báo chuyển khoản đang chờ xác nhận.
  - Thẻ quỹ lớp: số dư quỹ (chỉ con số tổng).
- **Thao tác:** "Báo chuyển khoản" (PAR-03); xem chi tiết (PAR-02); xem sổ lớp (PAR-04).
- **Quy tắc UI:** không có tên hay công nợ của học sinh khác (D-19).

### PAR-02 Khoản nộp của con
- **Quyền:** `Receipt.ViewOwn`. **FR:** FR-RPT-05.
- **Hiển thị:** danh sách thẻ khoản nợ (đợt thu, phải nộp, đã nộp, miễn giảm, chờ xác nhận, còn lại, hạn, tình trạng), **gồm nợ cũ** có nhãn lớp – năm học; lịch sử phiếu thu của con; lịch sử số dư cá nhân; lịch sử lớp (D-35).
- **Thao tác:** báo chuyển khoản cho một khoản nợ; hủy báo chuyển khoản của mình khi còn chờ (DLG-08).

### PAR-03 Báo chuyển khoản
- **Quyền:** `Receipt.SubmitTransfer`. **FR:** FR-REC-08.
- **Hiển thị / form:** khoản nợ (chọn một hoặc nhiều của con, chỉ khoản có số còn có thể thu > 0), số tiền đã chuyển, ngày chuyển, **ảnh biên lai** (chụp từ camera – bắt buộc), ghi chú. Thông tin tài khoản nhận tiền: UQ-06.
- **Xem trước:** "Trả nợ X đ · Vào số dư cá nhân Y đ (nếu chuyển dư)".
- **Thao tác:** gửi → `PendingConfirmation`; màn hình xác nhận "Đang chờ thủ quỹ xác nhận".

### PAR-04 Sổ thu chi lớp
- **Quyền:** `Report.ViewSummary`, `Expense.View`. **FR:** FR-RPT-02 (bản phụ huynh), FR-EXP-06.
- **Hiển thị:**
  - Số dư quỹ lớp.
  - **Thu: tổng theo đợt** (không tên người nộp – D-19), thu khác.
  - **Chi: chi tiết từng phiếu** + xem chứng từ (ảnh).
  - Lọc theo tháng.
- **Thao tác:** xem chứng từ chi; không có xuất file.

---

## 8. Hộp thoại (DLG)

| Mã | Hộp thoại | Dùng ở | Nội dung chính | Ràng buộc |
|---|---|---|---|---|
| DLG-01 | Mời phụ huynh | TCH-02 | Email, học sinh (một hoặc nhiều con trong lớp) | `Invitation.SendParent`; email đã có tài khoản → đề xuất liên kết thay vì mời |
| DLG-02 | Đề nghị miễn giảm | FND-02, FND-03 | Loại (toàn phần / một phần), số tiền (≤ nợ chưa thanh toán – Q-51), **lý do bắt buộc** | `Exemption.Request`; người duyệt khi GVCN tự đề nghị: Q-55 |
| DLG-03 | Xác nhận nhận bàn giao | TRS-07, TRS-08, CUS-01, FND-06 | Số dự kiến (chỉ đọc), **số thực nhận**. Khớp → xác nhận. Không khớp → mở rộng form biên bản: lý do (bắt buộc), minh chứng | Chỉ người nhận (`ToUserId`); người thu hộ không tự xác nhận (D-43) |
| DLG-04 | Đề xuất phương án chênh lệch | FND-07 | Loại (sửa sai trước ghi nhận / bàn giao bổ sung / ghi nhận thiếu hụt / ghi nhận tiền thừa), số tiền (≤ phần chưa xử lý), mô tả, ảnh hưởng số dư dự kiến | `HandoverDiscrepancy.Propose` (Q-47, Q-48) |
| DLG-05 | Xác nhận đã trả hoàn trả | TRS-06, FND-06 | Ngày trả, ghi chú, **minh chứng bắt buộc** | `Refund.ConfirmPayment` (D-53) |
| DLG-06 | Đảo giao dịch | FND-06 | Chứng từ gốc (tóm tắt), ảnh hưởng ngược (quỹ / nợ / số dư cá nhân), **lý do bắt buộc**, cảnh báo nếu làm số dư âm (Q-52) | `Reversal.Create` (Q-49); chứng từ đã có đảo → nút ẩn |
| DLG-07 | Từ chối | COM-06, FND-06, FND-07, TRS-03 | **Lý do bắt buộc** | Người duyệt |
| DLG-08 | Hủy chứng từ | FND-06, TRS-05, TRS-06, TCH-04, PAR-02 | **Lý do bắt buộc**; nêu rõ giữ chỗ sẽ được giải phóng | Chỉ người tạo, chỉ khi còn chờ (D-57) |
| DLG-09 | Gửi nhắc nộp | FND-02 | Danh sách người nhận (phụ huynh của học sinh chưa đủ / quá hạn), nội dung mẫu (UQ-05) | `Reminder.Send`; chỉ thông báo trong hệ thống (D-12) |
| DLG-10 | Xác nhận chuyển lớp | ADM-05 | Tóm tắt học sinh, lớp đích, ngày hiệu lực, những gì đi theo học sinh | `Student.Transfer`; xác nhận cuối cùng (D-56) |
