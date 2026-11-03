# Vai trò & Permission

> Phiên bản: 0.7 (đã cập nhật theo quyết định D-01 → D-58) · Ngày: 2026-11-03
> Theo `training-doc.md`, hệ thống phân quyền theo **permission** (`[RequirePermission]`). Vai trò chỉ là **nhóm permission mặc định**; code kiểm tra permission, không kiểm tra tên vai trò.

## 1. Mô tả vai trò

### 1.1 Quản trị hệ thống (D-01)
- **Trách nhiệm:** tạo năm học, tạo lớp, gán GVCN (D-22); mời GVCN (D-03); chuyển lớp / lên lớp (D-23); danh mục chi chung (D-18); phê duyệt chuyển quỹ và chứng từ điều chỉnh (D-29, D-30); **phân công người giữ quỹ** cho lớp – năm học đã kết thúc (D-37, D-45); **xác nhận chuyển lớp** (D-56); **duyệt phương án xử lý chênh lệch bàn giao** khi lớp gốc đã khóa sổ hoặc không còn GVCN (D-50).
- **Phạm vi dữ liệu:** toàn trường.
- **Báo cáo:** xem và xuất Excel / PDF báo cáo của tất cả lớp (D-33, D-41). Quyền xem / xuất **không** kèm quyền tạo, sửa, duyệt, hủy giao dịch. Quản trị **không** có `Receipt.View` (D-41, D-42, D-49). Có `Collection.View`, `Expense.View` toàn trường và **mặc định** có `Report.ViewStudentIdentity` để xem danh tính học sinh trong báo cáo nội bộ; mỗi lần xem được ghi lịch sử truy cập (D-46, D-52).

### 1.2 Giáo viên chủ nhiệm (GVCN)
- **Trách nhiệm:** quản lý học sinh trong lớp và đánh dấu nghỉ học (D-14); mời phụ huynh (D-03); phân công thủ quỹ (D-05); tạo đợt thu (D-07); duyệt miễn giảm (D-10), phiếu chi (D-15), hoàn trả (D-21); chốt sổ (D-20); lập đề nghị chuyển quỹ khi là GVCN lớp chuyển (D-29); **duyệt phương án xử lý chênh lệch bàn giao** khi là GVCN lớp gốc, trừ phương án do chính mình đề xuất (D-50).
- Khi là GVCN lớp nhận chuyển quỹ: được thông báo, không phải xác nhận (D-29).
- **Phạm vi dữ liệu:** các lớp mình chủ nhiệm.
- Không ghi phiếu thu / chi, để người ghi chép và người kiểm soát là hai người khác nhau.

### 1.3 Thủ quỹ (D-05)
- Là **một phụ huynh** của lớp, do GVCN phân công. Mỗi lớp **chỉ có một** thủ quỹ.
- **Trách nhiệm:** tạo đợt thu, ghi phiếu thu, xác nhận chuyển khoản, thu khác, phiếu chi, phiếu hoàn trả, danh mục chi riêng, nhắc nộp tiền; và các nghiệp vụ khi học sinh chuyển lớp:
  - **Thủ quỹ lớp mới:** thu hộ công nợ cũ (D-31, D-36), bàn giao tiền thu hộ về người giữ quỹ lớp gốc (D-37), xác nhận nhận tiền thừa (D-38).
  - **Thủ quỹ lớp cũ:** lập chứng từ bàn giao tiền thừa (D-38); thu công nợ thuộc lớp cũ của học sinh đã chuyển đi nếu còn được phân công (D-39).
  - **Thủ quỹ lớp gốc khi năm học chưa kết thúc:** là người giữ quỹ của lớp, **xác nhận nhận tiền thu hộ** trong phạm vi được giao (D-43, D-49).
- **Phạm vi dữ liệu:** lớp được phân công; công nợ cũ chỉ của học sinh đang học trong lớp mình (D-42).
- Tài khoản thủ quỹ đồng thời giữ quyền Phụ huynh của mình.

### 1.4 Người giữ quỹ lớp cũ (D-37, D-45)
- Do **Quản trị phân công**, ghi rõ **lớp và năm học**, cho một lớp – năm học đã kết thúc. Có thể là **thủ quỹ cũ** còn được phân công hoặc **người khác** do Quản trị chỉ định.
- **Trách nhiệm:** xác nhận nhận tiền thu hộ, kể cả thu hộ sau khóa sổ (D-43); lập biên bản chênh lệch nếu không khớp (D-44).
- **Quyền mặc định:** `CashHandover.Confirm`, xem báo cáo của lớp – năm học được giao (D-52). Xuất báo cáo cần được cấp `Report.Export`.
- **Không** mặc nhiên có toàn bộ quyền Thủ quỹ. Chỉ được thu nợ cũ khi được cấp riêng quyền thu tương ứng. Ba nhóm quyền xác nhận bàn giao, xem báo cáo, thu nợ được **tách riêng** (D-45).

### 1.5 Phụ huynh
- **Trách nhiệm:** nộp tiền, báo chuyển khoản, theo dõi khoản nộp của con và tình hình quỹ.
- **Phạm vi dữ liệu:** các lớp có con mình đang học (D-04); lịch sử của con ở lớp cũ (D-35). Thấy chi tiết của con mình; phần thu của lớp chỉ thấy **tổng theo đợt** (D-19).

## 2. Danh sách permission

Tên permission đặt ở mức nghiệp vụ, có thể đổi tên khi thiết kế kỹ thuật nhưng không đổi quyền.

| Permission | Mô tả | QT | GVCN | TQ | NGQ | PH |
|---|---|:-:|:-:|:-:|:-:|:-:|
| **Năm học, lớp, tài khoản** | | | | | | |
| `SchoolYear.Manage` | Tạo / sửa năm học (D-22) | ✔ | | | | |
| `Class.Manage` | Tạo / sửa lớp, gán GVCN (D-22) | ✔ | | | | |
| `Class.View` | Xem thông tin lớp | ✔ | ✔ | ✔ | | ✔ |
| `Invitation.SendTeacher` | Mời GVCN (D-03) | ✔ | | | | |
| `Invitation.SendParent` | Mời phụ huynh (D-03) | | ✔ | | | |
| `Treasurer.Assign` | Phân công / đổi thủ quỹ (D-05) | | ✔ | | | |
| `FundCustodian.Assign` | Phân công / đổi người giữ quỹ lớp – năm học đã kết thúc, ghi rõ lớp và năm học (D-37, D-45) | ✔ | | | | |
| **Học sinh** | | | | | | |
| `Student.View` | Xem danh sách học sinh lớp | ✔ | ✔ | ✔ | | |
| `Student.ViewOwn` | Xem thông tin con mình, kể cả lịch sử lớp cũ (D-35) | | | | | ✔ |
| `Student.Manage` | Thêm / sửa học sinh trong lớp, đánh dấu nghỉ học, liên kết phụ huynh | | ✔ | | | |
| `Student.Transfer` | Chuyển lớp / lên lớp; thao tác của Quản trị là xác nhận cuối cùng (D-23, D-56) | ✔ | | | | |
| **Thu** | | | | | | |
| `Collection.View` | Xem đợt thu (không có danh tính) | ✔ (D-33, D-49) | ✔ | ✔ | | ✔ |
| `Collection.Manage` | Tạo / sửa / hủy / đóng đợt thu (D-07) | | ✔ | ✔ | | |
| `Exemption.Request` | Đề nghị miễn giảm (D-28) | | ✔ | ✔ | | |
| `Exemption.Approve` | Duyệt / từ chối miễn giảm; miễn giảm đã duyệt chỉ xử lý bằng đảo giao dịch (D-10, D-57) | | ✔ | | | |
| `Receipt.View` | Xem phiếu thu **có danh tính** phụ huynh / học sinh. Permission riêng, không suy ra từ quyền xem báo cáo (D-41, D-49) | | ✔ | ✔ | | |
| `Receipt.ViewOwn` | Xem phiếu thu, công nợ, số dư cá nhân của con mình | | | | | ✔ |
| `Receipt.Create` | Ghi phiếu thu cho công nợ của lớp mình | | | ✔ | | |
| `Receipt.Cancel` | Hủy phiếu thu của mình khi còn chờ xác nhận (D-57) | | | ✔ | | ✔ (báo chuyển khoản của mình) |
| `Receipt.SubmitTransfer` | Báo chuyển khoản kèm biên lai (D-11) | | | | | ✔ |
| `Receipt.ConfirmTransfer` | Xác nhận / từ chối chuyển khoản; xác nhận cập nhật nợ còn lại, từ chối giải phóng giữ chỗ khoản nợ (D-11, D-48) | | | ✔ | | |
| `Receipt.CollectOnBehalf` | Thu hộ công nợ cũ của học sinh đang học lớp mình, kể cả sau khóa sổ (D-31, D-36, D-39) | | | ✔ | | |
| `Receipt.CollectFormerStudent` | Thu công nợ thuộc lớp mình của học sinh đã chuyển đi (D-39) | | | ✔ | cấp riêng (D-45) | |
| `OtherIncome.Create` | Ghi / hủy thu khác (D-13) | | | ✔ | | |
| **Tiền thừa & bàn giao** | | | | | | |
| `Refund.Create` | Lập phiếu hoàn trả từ số dư cá nhân khả dụng, có giữ chỗ; hủy phiếu của mình khi còn chờ duyệt (D-21, D-47, D-57) | | | ✔ | | |
| `Refund.Approve` | Duyệt (→ chờ thực hiện hoàn trả) / từ chối (giải phóng giữ chỗ) phiếu hoàn trả (D-21, D-47, D-53) | | ✔ | | | |
| `Refund.ConfirmPayment` | Xác nhận đã thực tế trả tiền, đính kèm minh chứng → hoàn tất, giảm số dư thực tế (D-53) | | | ✔ | | |
| `Refund.CancelApproved` | Hủy phiếu hoàn trả đã duyệt nhưng chưa hoàn tất theo quy trình riêng (D-57; người đề nghị / duyệt: Q-49) | | ? | ? | | |
| `BalanceHandover.Create` | Lập chứng từ bàn giao tiền thừa – thủ quỹ lớp cũ (D-38) | | | ✔ | | |
| `BalanceHandover.Confirm` | Xác nhận nhận tiền thừa, hoặc lập biên bản chênh lệch nếu không khớp – thủ quỹ lớp mới (D-38, D-44) | | | ✔ | | |
| `CashHandover.Create` | Lập chứng từ bàn giao tiền thu hộ về lớp gốc (D-37) | | | ✔ | | |
| `CashHandover.Confirm` | Xác nhận nhận tiền thu hộ (kể cả sau khóa sổ), ghi số tiền thực nhận, hoặc lập biên bản chênh lệch. Chỉ cấp cho người nhận được phân công; người thu hộ không tự xác nhận (D-37, D-43, D-44) | | | ✔ (thủ quỹ lớp gốc, năm chưa kết thúc – D-49) | ✔ (D-45) | |
| `HandoverDiscrepancy.Propose` | Đề xuất phương án xử lý chênh lệch (một trong 4 loại – D-51; ai đề xuất: Q-47) | | ? | ? | ? | |
| `HandoverDiscrepancy.Approve` | Duyệt / từ chối phương án xử lý chênh lệch; không duyệt phương án của chính mình (D-50) | ✔ (lớp gốc đã khóa sổ / không còn GVCN) | ✔ (GVCN lớp gốc) | | | |
| **Chi** | | | | | | |
| `Expense.View` | Xem phiếu chi và chứng từ | ✔ (D-33, D-49) | ✔ | ✔ | | ✔ |
| `Expense.Create` | Lập / sửa phiếu chi chưa duyệt hoặc bị từ chối; giữ chỗ cập nhật theo số mới sau khi kiểm tra hợp lệ (D-40, D-49) | | | ✔ | | |
| `Expense.Approve` | Duyệt / từ chối phiếu chi (D-15) | | ✔ | | | |
| `Expense.Cancel` | Hủy phiếu chi của mình khi còn chờ duyệt (D-57) | | | ✔ | | |
| `Reversal.Create` | Tạo chứng từ đảo giao dịch cho chứng từ đã hoàn tất, có lý do, liên kết chứng từ gốc (D-57; theo từng loại chứng từ: Q-49) | ? | ? | ? | | |
| `Reversal.Approve` | Duyệt / từ chối chứng từ đảo giao dịch (D-57; Q-49) | ? | ? | | | |
| `ExpenseCategory.ManageShared` | Quản lý danh mục chi chung (D-18) | ✔ | | | | |
| `ExpenseCategory.ManageClass` | Quản lý danh mục chi riêng của lớp (D-18) | | | ✔ | | |
| **Báo cáo, chốt sổ, chuyển quỹ** | | | | | | |
| `Report.ViewSummary` | Xem tổng thu / chi / số dư / giữ chỗ / khả dụng, sổ thu chi dạng tổng hợp | ✔ (D-33) | ✔ | ✔ | ✔ (lớp – năm được giao, D-45) | ✔ |
| `Report.ViewDetail` | Xem sổ chi tiết, báo cáo đợt thu, số dư cá nhân, trước / sau khóa sổ, tiền ghi sổ và người giữ tiền, báo cáo bàn giao. Tên học sinh chỉ hiện khi có `Report.ViewStudentIdentity` | ✔ (D-33) | ✔ | ✔ | ✔ (lớp – năm được giao, D-45) | |
| `Report.ViewStudentIdentity` | Xem **tên học sinh** trong báo cáo thu, báo cáo số dư cá nhân, lịch sử thao tác; mỗi lần xem được ghi lịch sử truy cập (D-46) | ✔ (D-52) | ✔ | ✔ | cấp riêng (D-45) | |
| `Report.Export` | Xuất Excel / PDF báo cáo trong phạm vi quyền xem (D-41, D-52) | ✔ | ✔ | ✔ | cấp riêng (D-52) | |
| `Reminder.Send` | Gửi nhắc nộp tiền (D-12) | | ✔ | ✔ | | |
| `Fund.Close` | Chốt sổ năm học (không chuyển số dư quỹ – D-25) | | ✔ | | | |
| `FundTransfer.Request` | Lập đề nghị chuyển quỹ từ lớp mình (có giữ chỗ), kể cả chứng từ điều chỉnh sau khóa sổ; hủy đề nghị của mình khi còn chờ (D-29, D-30, D-40, D-57) | | ✔ | | | |
| `FundTransfer.Approve` | Phê duyệt / từ chối đề nghị chuyển quỹ và chứng từ điều chỉnh (D-29, D-30) | ✔ | | | | |
| `AuditLog.View` | Xem lịch sử thao tác (tên học sinh chỉ hiện khi có `Report.ViewStudentIdentity` – D-46) | ✔ | ✔ | | | |

Ký hiệu: QT = Quản trị · TQ = Thủ quỹ · NGQ = Người giữ quỹ lớp cũ · PH = Phụ huynh · ✔ có quyền · trống = không có · ? = chờ xác nhận (Q-47 cho `HandoverDiscrepancy.Propose`; Q-49 cho `Reversal.*`, `Refund.CancelApproved`) · "cấp riêng" = không có mặc định, Quản trị cấp cho từng người (D-45).

## 3. Nguyên tắc phân quyền

1. **Permission + phạm vi:** Quản trị có phạm vi toàn trường. Các vai trò khác chỉ thao tác trong lớp mình liên quan (chủ nhiệm, làm thủ quỹ, có con học, được phân công giữ quỹ).
2. **Người lập và người duyệt / xác nhận là hai người khác nhau:** thủ quỹ lập phiếu chi, hoàn trả, đề nghị miễn giảm → GVCN duyệt; GVCN lập chuyển quỹ → Quản trị duyệt; thủ quỹ lớp cũ lập bàn giao tiền thừa → thủ quỹ lớp mới xác nhận; thủ quỹ thu hộ lập bàn giao tiền → người giữ quỹ lớp gốc xác nhận; người nhận lập biên bản chênh lệch → phương án được đề xuất (Q-47) → GVCN lớp gốc duyệt (Quản trị khi đã khóa sổ / không còn GVCN); **người đề xuất không tự duyệt** (D-50). **Người thu hộ không được tự xác nhận** bàn giao của mình (D-43); nếu người thu chính là người giữ quỹ thì không lập bàn giao (D-58).
3. **Hủy & đảo chứng từ:** người tạo hủy chứng từ của mình khi còn chờ; người duyệt từ chối; chứng từ đã hoàn tất chỉ xử lý bằng đảo giao dịch / điều chỉnh (D-57).
4. **Quyền xem không kèm quyền ghi:** `*.View`, `Report.*` chỉ cho xem / xuất (D-33, D-41).
5. **Danh tính trong biên nhận** chỉ được xem khi có `Receipt.View` (hoặc `Receipt.ViewOwn` cho con mình). **Tên học sinh trong báo cáo nội bộ** chỉ được xem khi có `Report.ViewStudentIdentity`, có ghi lịch sử truy cập. Quyền xem / xuất báo cáo không tự động cấp hai quyền này (D-41, D-46).
6. **Một tài khoản, nhiều vai trò:** quyền là hợp của các vai trò (thủ quỹ = Phụ huynh + Thủ quỹ trong lớp đó).
7. **Đổi người phụ trách** (thủ quỹ, người giữ quỹ): quyền chuyển ngay sang người mới; phiếu người cũ đã ghi giữ nguyên tên người thực hiện; không mất lịch sử (D-28, D-39).
8. **Chuyển lớp / lên lớp:** liên kết phụ huynh đi theo học sinh; phụ huynh vẫn xem được lịch sử của con ở lớp cũ trong phạm vi quyền (D-35).
9. **Người giữ quỹ lớp cũ** không mặc nhiên có quyền Thủ quỹ; xác nhận bàn giao, xem báo cáo, thu nợ là ba nhóm quyền tách riêng (D-45).
