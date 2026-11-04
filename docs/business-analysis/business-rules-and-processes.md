# Quy tắc & Quy trình nghiệp vụ

> Phiên bản: 0.7 (đã cập nhật theo quyết định D-01 → D-58) · Ngày: 2026-11-03
> Cột **Nguồn**: *D-xx* = quyết định đã được chủ dự án xác nhận ([open-questions.md](../requirements/open-questions.md)) · *Q-xx* = phần còn chờ xác nhận.

## 0. Số liệu tiền – không được trộn lẫn

### 0.1 Ba loại số liệu

| Loại | Thuộc về | Công thức | Nguồn |
|---|---|---|---|
| **Số dư quỹ lớp** | Một lớp – năm học | Σ tiền đã **phân bổ vào công nợ** của các đợt thu thuộc lớp – năm học đó (tiền mặt / chuyển khoản đã xác nhận, tiền tự trừ từ số dư cá nhân, tiền **thu hộ** bởi lớp khác, kể cả thu hộ sau khóa sổ) + Σ thu khác hợp lệ + Σ chuyển quỹ **đến** đã duyệt − Σ phiếu chi đã duyệt − Σ chuyển quỹ **đi** đã duyệt ± Σ điều chỉnh chênh lệch **đã được phê duyệt** (ảnh hưởng từng loại: Q-48) ± Σ đảo giao dịch đã duyệt | D-09, D-13, D-15, D-25, D-26, D-31, D-36, D-44, D-51, D-57 |
| **Công nợ học sinh** | Học sinh; **mỗi khoản thuộc lớp – năm học của đợt thu** | Nợ chưa thanh toán một đợt = số phải nộp − miễn giảm đã duyệt − Σ tiền đã phân bổ vào đợt đó | D-08, D-10, D-24, D-31, D-39 |
| **Số dư cá nhân học sinh (thực tế)** | Học sinh (không thuộc quỹ lớp nào) | Σ phần nộp thừa − Σ đã tự trừ vào công nợ − Σ hoàn trả **đã hoàn tất** (thủ quỹ xác nhận đã trả tiền – D-53). Bàn giao **không** đổi số tiền, chỉ đổi người quản lý và trạng thái | D-09, D-21, D-32, D-38, D-47 |

### 0.2 Giữ chỗ & số khả dụng

| Đối tượng | Giữ chỗ bởi | Số khả dụng | Khi duyệt / xác nhận | Khi từ chối / hủy | Nguồn |
|---|---|---|---|---|---|
| **Số dư quỹ lớp** | Phiếu chi, chuyển quỹ đi, chứng từ điều chỉnh **đang chờ duyệt** | Số dư khả dụng = số dư quỹ lớp − giữ chỗ | Giữ chỗ → giao dịch chính thức | Giải phóng | D-40 |
| **Số dư cá nhân** | Phiếu hoàn trả **đang chờ duyệt** hoặc **chờ thực hiện hoàn trả** (giữ chỗ ngay khi tạo, đến khi hoàn tất / bị từ chối / hủy) | Số dư cá nhân khả dụng = số dư thực tế − giữ chỗ; bằng **0** khi số dư đang **chờ bàn giao** | Hoàn trả hoàn tất → giảm số dư thực tế | Giải phóng | D-38, D-47 |
| **Khoản nợ** | Phiếu thu **đang chờ xác nhận** (phụ huynh báo chuyển khoản) | Số nợ còn có thể thu = nợ chưa thanh toán − nợ đang giữ chỗ | Cập nhật nợ còn lại | Giải phóng | D-48 |

Điều kiện chung: không cho tạo chứng từ có số tiền vượt số khả dụng tương ứng tại thời điểm tạo; kể cả khi nhiều yêu cầu được tạo đồng thời (nhóm 1.14).

### 0.3 Ghi sổ ≠ giữ tiền thực tế (D-37)

| Khái niệm | Ý nghĩa |
|---|---|
| **Số tiền ghi sổ** | Số dư quỹ lớp – năm học theo mục 0.1 |
| **Người đang giữ tiền** | Người thực tế cầm tiền: thủ quỹ của lớp khi năm học chưa kết thúc; người giữ quỹ do Quản trị phân công khi đã kết thúc (D-45, D-49); với tiền thu hộ chưa bàn giao là **thủ quỹ đã thu hộ** |
| **Tiền thu hộ chưa bàn giao** | Đã ghi sổ vào quỹ lớp gốc nhưng còn nằm ở thủ quỹ thu hộ; chỉ hết khi chứng từ bàn giao **hoàn tất**. Bàn giao ở trạng thái `Discrepancy` vẫn tính là chưa bàn giao xong (D-44) |

Hệ quả:
- Một phiếu thu tách thành phần **phân bổ vào công nợ** (vào quỹ lớp – năm học sở hữu công nợ) và phần **vượt** (vào số dư cá nhân). Phần vượt không bao giờ vào quỹ lớp, kể cả khi thu hộ nợ cũ. Ví dụ: nợ cũ 500.000 đ, thực thu 600.000 đ → 500.000 đ vào quỹ lớp gốc, 100.000 đ vào số dư cá nhân (D-48).
- Tiền tự trừ từ số dư cá nhân vào công nợ **trở thành** tiền của quỹ lớp – năm học sở hữu đợt thu.
- Hoàn trả chỉ giảm số dư cá nhân, không ảnh hưởng quỹ lớp.
- Thu hộ làm tăng quỹ lớp – năm học **gốc**, không làm tăng quỹ lớp thu hộ; lớp thu hộ chỉ hiển thị "Đã thu hộ" (D-42).
- Chênh lệch bàn giao **không** tự động làm thay đổi số dư nào; chỉ thay đổi khi điều chỉnh đã được phê duyệt (D-44).

## 1. Quy tắc nghiệp vụ

### 1.1 Chung

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-GEN-01 | Mỗi lớp có một quỹ cho mỗi năm học | D-02 |
| BR-GEN-02 | Tiền tệ VND, số tiền là số nguyên dương (> 0) | D-27 |
| BR-GEN-03 | Mọi chứng từ tài chính (thu, thu hộ, chi, hoàn trả, thu khác, miễn giảm, bàn giao tiền thừa, bàn giao tiền thu hộ, biên bản chênh lệch, chuyển quỹ, điều chỉnh, đảo giao dịch) **không bị xóa hẳn**. Chứng từ còn chờ: người tạo hủy, người duyệt từ chối. Chứng từ đã hoàn tất: **không sửa / xóa trực tiếp**, chỉ xử lý bằng chứng từ điều chỉnh hoặc **đảo giao dịch** liên kết chứng từ gốc, có lý do (nhóm 1.13) | D-27, D-44, D-57 |
| BR-GEN-04 | Mọi thao tác ảnh hưởng đến tiền (tạo / sửa / hủy / duyệt / xác nhận / giữ chỗ / giải phóng / điều chỉnh / phân công) đều lưu người thực hiện và thời điểm | D-27, D-39, D-40, D-44 |
| BR-GEN-05 | Số dư quỹ lớp, công nợ, số dư cá nhân, giữ chỗ và số khả dụng tính theo mục 0 | D-25, D-31, D-40, D-47, D-48 |
| BR-GEN-06 | Số dư cá nhân học sinh **không** được cộng vào số dư quỹ lớp | D-09, D-32 |
| BR-GEN-07 | Năm học đã khóa sổ thì **không** được thêm / sửa / hủy giao dịch của năm đó. **Hai ngoại lệ**, đều bằng chứng từ riêng và không sửa giao dịch cũ: (1) chuyển quỹ bằng chứng từ điều chỉnh (BR-ADJ-01); (2) thu hộ công nợ bằng chứng từ thu hộ bổ sung (BR-ADJ-05). Điều chỉnh chênh lệch bàn giao của lớp đã khóa sổ cũng không sửa chứng từ gốc (BR-DIS-07) | D-20, D-30, D-36, D-44 |

### 1.2 Tài khoản, lớp & học sinh

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-ACC-01 | Tài khoản chỉ được tạo qua lời mời: Quản trị mời GVCN, GVCN mời phụ huynh | D-03 |
| BR-ACC-02 | Đăng nhập bằng email + mật khẩu; email là duy nhất | D-03 |
| BR-ACC-03 | Lời mời phụ huynh phải gắn với ít nhất một học sinh của lớp | D-03, D-04 |
| BR-ACC-04 | Mỗi lớp có tối đa **một** thủ quỹ, phải là phụ huynh có con trong lớp | D-05 |
| BR-ACC-05 | Liên kết mời có thời hạn và chỉ dùng được một lần | D-28 |
| BR-ACC-06 | Đổi thủ quỹ / người giữ quỹ: quyền chuyển ngay sang người mới; phiếu người cũ đã ghi giữ nguyên tên; không mất lịch sử | D-28, D-39 |
| BR-ACC-07 | Chỉ Quản trị tạo năm học, tạo lớp và gán GVCN | D-22 |
| BR-ACC-08 | Chỉ Quản trị chuyển lớp / lên lớp cho học sinh; GVCN đánh dấu nghỉ học | D-14, D-23 |
| BR-ACC-09 | Học sinh đã chuyển lớp / nghỉ học: giữ toàn bộ lịch sử; không được áp dụng vào đợt thu mới của lớp cũ | D-14, D-28 |
| BR-ACC-10 | Khi chuyển lớp / lên lớp: công nợ cũ, số dư cá nhân và liên kết phụ huynh **đi theo học sinh** (chi tiết: nhóm 1.7, 1.10) | D-24, D-35 |
| BR-ACC-11 | Phụ huynh vẫn xem được lịch sử của con ở lớp cũ, trong phạm vi quyền | D-35 |
| BR-ACC-12 | Quản trị phân công **người giữ quỹ** cho mỗi lớp – năm học đã kết thúc, ghi rõ **lớp và năm học**; có thể là thủ quỹ cũ còn được phân công hoặc người khác do Quản trị chỉ định | D-37, D-45 |
| BR-ACC-13 | Người giữ quỹ lớp cũ có quyền xác nhận bàn giao và xem báo cáo trong phạm vi được giao; **không** mặc nhiên có toàn bộ quyền Thủ quỹ; chỉ được thu nợ cũ khi được cấp quyền thu tương ứng | D-45 |
| BR-ACC-14 | Khi năm học chưa kết thúc, **thủ quỹ hiện tại** là người giữ quỹ của lớp | D-49 |

### 1.3 Thu

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-REC-01 | Đợt thu phải có: tên, số tiền mỗi học sinh, hạn nộp, ít nhất một học sinh áp dụng | D-34 |
| BR-REC-02 | Đợt thu đã có phiếu thu hợp lệ thì không được sửa số tiền hoặc hủy; chỉ được đóng | D-27 |
| BR-REC-03 | Được nộp nhiều lần, nộp một phần | D-08 |
| BR-REC-04 | Phần nộp **vượt** công nợ được ghi vào số dư cá nhân học sinh, kể cả khi thu hộ nợ cũ; không ghi vào quỹ lớp | D-09, D-48 |
| BR-REC-05 | Khi học sinh được áp dụng đợt thu mới, **số dư cá nhân khả dụng** tự động trừ vào công nợ đợt đó. Số dư đang chờ bàn giao **không** được tự trừ | D-09, D-38, D-49 |
| BR-REC-06 | Tình trạng nộp: Chưa nộp · Nộp một phần · Đã nộp đủ · Được miễn; quá hạn mà chưa đủ thì đánh dấu "Quá hạn" | D-08, D-10 |
| BR-REC-07 | Miễn giảm: Thủ quỹ hoặc GVCN lập đề nghị có lý do, **GVCN duyệt**; hỗ trợ miễn toàn phần và giảm một phần (miễn giảm khi đã nộp một phần / đủ: Q-51; GVCN tự đề nghị thì ai duyệt: Q-55) | D-10, D-28 |
| BR-REC-08 | Báo chuyển khoản chưa được tính tiền cho đến khi thủ quỹ xác nhận; bắt buộc có ảnh biên lai; khi đang chờ, **giữ chỗ** khoản nợ tương ứng | D-11, D-48 |
| BR-REC-09 | Thu khác bắt buộc có nguồn thu và chứng từ; không cần duyệt; tính vào quỹ ngay | D-13 |
| BR-REC-10 | Hoàn trả chỉ lấy từ **số dư cá nhân khả dụng**; phiếu **giữ chỗ** ngay khi tạo; nhiều phiếu hoàn trả cộng lại không được vượt số dư khả dụng; bị từ chối / hủy thì giải phóng; GVCN duyệt → **chờ thực hiện hoàn trả** (vẫn giữ chỗ); thủ quỹ xác nhận đã trả tiền + **minh chứng** → **hoàn tất**, giảm số dư thực tế | D-21, D-38, D-47, D-53 |
| BR-REC-11 | Mỗi phiếu thu (kể cả thu hộ) **liên kết với khoản công nợ gốc** mà nó thanh toán | D-39 |
| BR-REC-12 | **Không cho tạo phiếu thanh toán nợ** khi số nợ còn có thể thu = 0; nếu thực tế đã nhận tiền thì ghi nhận theo nghiệp vụ **số dư cá nhân** (không gắn khoản nợ) | D-54 |
| BR-REC-13 | Thu vượt nợ: phiếu thu **tách** phần thanh toán nợ (vào quỹ lớp sở hữu nợ) và phần tiền thừa (vào số dư cá nhân) | D-48, D-54 |

### 1.4 Chi

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-EXP-01 | Phiếu chi phải có: nội dung, số tiền, ngày chi, danh mục, **chứng từ đính kèm** | D-16 |
| BR-EXP-02 | Mọi phiếu chi phải được GVCN duyệt mới tính vào quỹ | D-15 |
| BR-EXP-03 | Không được **tạo** phiếu chi có số tiền vượt số dư khả dụng; phiếu chờ duyệt **giữ chỗ** số tiền đó (mục 0.2) | D-17, D-40 |
| BR-EXP-04 | Phiếu đã duyệt không được sửa; sai thì lập **đảo giao dịch** có lý do rồi lập phiếu mới | D-27, D-57 |
| BR-EXP-05 | Thủ quỹ chỉ sửa phiếu chưa duyệt / bị từ chối, và được hủy phiếu của mình khi còn chờ; phiếu chi đã duyệt chỉ xử lý bằng đảo giao dịch (người tạo / duyệt: Q-49) | D-28, D-57 |
| BR-EXP-06 | Danh mục chi gồm danh mục chung (Quản trị) và danh mục riêng của lớp (Thủ quỹ) | D-18 |
| BR-EXP-07 | Sửa số tiền phiếu chi đang chờ duyệt: kiểm tra số mới không vượt số dư khả dụng, **sau khi hợp lệ** thì giữ chỗ cập nhật theo số mới | D-40, D-49 |

### 1.5 Báo cáo & minh bạch

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-RPT-01 | Phụ huynh thấy chi tiết phiếu thu, công nợ, số dư cá nhân của **con mình** | D-19 |
| BR-RPT-02 | Trong sổ thu chi, phụ huynh chỉ thấy phần thu dạng **tổng theo đợt**, không có tên người nộp, không thấy công nợ của học sinh khác | D-19 |
| BR-RPT-03 | Phụ huynh thấy chi tiết phiếu chi và chứng từ chi | D-19 |
| BR-RPT-04 | Báo cáo chỉ tính giao dịch hợp lệ: không tính phiếu chờ duyệt / xác nhận, bị từ chối, đã hủy (giữ chỗ hiển thị tách riêng – mục 0.2) | D-27, D-40 |
| BR-RPT-05 | Quản trị xem và xuất Excel / PDF báo cáo của tất cả lớp, có `Collection.View`, `Expense.View` toàn trường; chỉ xem; **không** mặc nhiên có `Receipt.View` | D-33, D-41, D-49, D-52 |
| BR-RPT-06 | Báo cáo phân biệt giao dịch phát sinh **trước** và **sau** khóa sổ (chuyển quỹ điều chỉnh, thu hộ bổ sung, điều chỉnh chênh lệch) | D-30, D-36, D-44 |
| BR-RPT-07 | Báo cáo quỹ lớp và báo cáo số dư cá nhân học sinh là **hai báo cáo tách riêng** | D-32 |
| BR-RPT-08 | Khoản thu hộ ghi vào sổ quỹ lớp – năm học gốc; tại lớp thu hộ chỉ hiển thị **"Đã thu hộ"**, không tính vào số dư quỹ lớp thu hộ | D-42 |
| BR-RPT-09 | Báo cáo hiển thị riêng **số tiền ghi sổ** và **người đang giữ tiền**, gồm tiền thu hộ chưa bàn giao và bàn giao đang `Discrepancy` | D-37, D-44 |
| BR-RPT-10 | Tên học sinh trong báo cáo thu, báo cáo số dư cá nhân, lịch sử thao tác chỉ hiện khi có `Report.ViewStudentIdentity` (Quản trị có mặc định); giới hạn của D-41 chỉ áp dụng cho biên nhận / chứng từ chia sẻ cho phụ huynh | D-41, D-46, D-52 |
| BR-RPT-11 | Mỗi lần xem thông tin chi tiết có tên học sinh trong báo cáo được **ghi lịch sử truy cập** | D-46 |
| BR-RPT-12 | Người giữ quỹ lớp cũ chỉ xem báo cáo của lớp – năm học được giao; xuất báo cáo khi được cấp `Report.Export` | D-45, D-52 |

### 1.6 Chốt sổ năm học

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-CLS-01 | GVCN chốt sổ cuối năm học | D-20 |
| BR-CLS-02 | Không được chốt khi còn phiếu chờ duyệt hoặc chờ xác nhận | D-28 |
| BR-CLS-03 | Khi chốt / lên lớp: công nợ chưa thanh toán và số dư cá nhân **đi theo học sinh** sang năm học sau | D-24, D-28 |
| BR-CLS-04 | Số dư quỹ lớp **không tự động chuyển** sang lớp khác; được giữ lại kèm lịch sử tại lớp – năm học phát sinh | D-25 |

### 1.7 Công nợ cũ & thu hộ

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-MOV-01 | Công nợ cũ vẫn **thuộc lớp – năm học phát sinh**, kể cả khi học sinh đã chuyển đi | D-31 |
| BR-MOV-02 | **Thủ quỹ lớp mới** được thu hộ công nợ cũ của học sinh đang học lớp mình; chỉ thấy công nợ cũ của các học sinh đó | D-31, D-39, D-42 |
| BR-MOV-03 | **Thủ quỹ lớp cũ** vẫn được thu công nợ thuộc lớp cũ nếu còn được phân công và có permission tương ứng | D-39 |
| BR-MOV-04 | Tiền thu hộ ghi vào quỹ lớp – năm học sở hữu công nợ; **không** ghi thành khoản thu của lớp hiện tại | D-31 |
| BR-MOV-05 | Phiếu thu hộ lưu: người thu hộ, lớp thu hộ, lớp – năm học sở hữu công nợ, khoản công nợ gốc | D-31, D-39 |
| BR-MOV-06 | **Chống thu trùng:** tiền phân bổ vào một khoản nợ không vượt **số nợ còn có thể thu** (đã trừ phần giữ chỗ), kể cả khi hai thủ quỹ thu cùng lúc; phần vượt vào số dư cá nhân. Số nợ còn có thể thu = 0 → không cho tạo phiếu thanh toán nợ (BR-REC-12) | D-39, D-48, D-54 |
| BR-MOV-07 | Thủ quỹ lớp cũ thu vượt nợ của học sinh đã chuyển đi: phần vượt là **số dư cá nhân**, không thuộc quỹ lớp cũ; thủ quỹ lớp cũ lập **bàn giao số dư cá nhân**, thủ quỹ lớp mới xác nhận; chỉ sau khi xác nhận mới khả dụng tại lớp mới | D-39, D-48, D-55 |

### 1.8 Bàn giao tiền thu hộ & người giữ quỹ

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-CASH-01 | Tiền thực tế thu hộ phải được **bàn giao** về người giữ quỹ của lớp gốc | D-37 |
| BR-CASH-02 | Chứng từ bàn giao tiền thu hộ lưu: người giao, người nhận, số tiền dự kiến, thời gian | D-37 |
| BR-CASH-03 | Không mặc định tiền quỹ lớp cũ thuộc quyền quản lý của thủ quỹ lớp mới | D-37 |
| BR-CASH-04 | Lớp – năm học đã kết thúc phải có người giữ quỹ được phân công thì bàn giao tiền thu hộ mới **hoàn tất** được | D-37, D-49 |
| BR-CASH-05 | **Người xác nhận** là người giữ quỹ của lớp gốc khi thực tế nhận tiền: năm học chưa kết thúc → thủ quỹ hiện tại của lớp gốc; đã kết thúc → người được Quản trị phân công | D-43 |
| BR-CASH-06 | **Người thu hộ không được tự xác nhận** bàn giao của chính mình | D-43 |
| BR-CASH-10 | Người thu **đồng thời là** người giữ quỹ lớp gốc: **không lập** bàn giao tiền thu hộ; phiếu thu vẫn ghi nhận người giữ tiền | D-58 |
| BR-CASH-07 | Khi xác nhận, lưu người xác nhận, thời gian, **số tiền thực nhận** | D-43 |
| BR-CASH-08 | `CashHandover.Confirm` chỉ cấp cho người nhận tiền được phân công | D-43 |
| BR-CASH-09 | Số tiền thực nhận khác số dự kiến → xử lý theo nhóm 1.9 | D-44 |

### 1.9 Chênh lệch khi bàn giao (tiền thu hộ và tiền thừa)

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-DIS-01 | **Không** tự động điều chỉnh bất kỳ số dư nào để khớp | D-44 |
| BR-DIS-02 | Lập **biên bản** ghi: số tiền dự kiến, số tiền thực nhận, chênh lệch, lý do, người bàn giao, người nhận; cho đính kèm minh chứng | D-44 |
| BR-DIS-03 | Bàn giao chuyển trạng thái **`Discrepancy`**, **chưa hoàn tất** | D-44 |
| BR-DIS-04 | Phương án xử lý được **GVCN lớp gốc duyệt** (người đề xuất: Q-47) | D-50 |
| BR-DIS-05 | Lớp gốc **đã khóa sổ** hoặc **không còn GVCN phụ trách** → **Quản trị** duyệt phương án. **Người đề xuất không tự duyệt.** Bị từ chối: chênh lệch vẫn chưa xử lý, **không** thay đổi số dư | D-44, D-50 |
| BR-DIS-06 | Chỉ ghi nhận điều chỉnh **sau khi** phương án được duyệt. Có **4 loại phương án**: sửa sai trước ghi nhận, bàn giao bổ sung, ghi nhận thiếu hụt, ghi nhận tiền thừa; mỗi loại có chứng từ, người duyệt, ảnh hưởng số dư riêng (ảnh hưởng cụ thể: Q-48; kết hợp nhiều phương án: Q-54) | D-44, D-51 |
| BR-DIS-07 | Không sửa trực tiếp chứng từ gốc đã hoàn tất / đã khóa sổ; điều chỉnh là chứng từ riêng | D-44, D-51 |
| BR-DIS-08 | Bàn giao tiền thừa ở trạng thái `Discrepancy`: số dư cá nhân vẫn **chưa khả dụng** ở lớp mới | D-38, D-44 |

### 1.10 Bàn giao tiền thừa cá nhân

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-BAL-01 | Khi học sinh chuyển lớp, số dư cá nhân được bàn giao cho thủ quỹ lớp mới bằng **chứng từ bàn giao riêng** | D-32 |
| BR-BAL-02 | **Thủ quỹ lớp cũ lập** chứng từ; **thủ quỹ lớp mới xác nhận** đã nhận tiền | D-38 |
| BR-BAL-03 | Không cần GVCN duyệt khi số tiền bàn giao **khớp** số dư trên hệ thống; không khớp → nhóm 1.9 | D-38, D-44 |
| BR-BAL-04 | Chứng từ lưu: số dư trước / sau bàn giao, người bàn giao, người nhận, số tiền, thời gian, trạng thái | D-32, D-38 |
| BR-BAL-05 | Số dư cá nhân chuyển sang **chờ bàn giao** sau khi **Quản trị xác nhận** việc chuyển lớp (không có bước xác nhận của GVCN lớp mới); khi chưa hoàn tất bàn giao, số dư **không dùng được** ở lớp mới (không tự trừ, không hoàn trả). Sau khi xác nhận khớp: chuyển sang **khả dụng** (phiếu hoàn trả đang chờ lúc chuyển lớp: Q-56) | D-38, D-49, D-56 |
| BR-BAL-06 | **Không** dùng nghiệp vụ chuyển quỹ lớp để ghi nhận tiền thừa cá nhân | D-32 |
| BR-BAL-07 | Phần thu vượt nợ do thủ quỹ lớp cũ thu sau khi học sinh đã chuyển đi cũng phải bàn giao số dư cá nhân như trên | D-55 |

### 1.11 Chuyển quỹ giữa các lớp

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-TRF-01 | Chuyển số dư quỹ giữa các lớp là **nghiệp vụ riêng**: có đề nghị, có phê duyệt, có ghi nhận | D-26 |
| BR-TRF-02 | **GVCN lớp chuyển** lập đề nghị; **Quản trị** phê duyệt; GVCN lớp nhận được thông báo; lớp nhận không bắt buộc xác nhận | D-29 |
| BR-TRF-03 | Phiếu chuyển quỹ ghi ở **cả hai** lớp, liên kết đến **cùng một giao dịch** | D-35 |
| BR-TRF-04 | Số tiền chuyển không vượt **số dư khả dụng** của lớp chuyển; đề nghị chờ duyệt **giữ chỗ** số tiền đó | D-35, D-40 |
| BR-TRF-05 | Chỉ dùng cho số dư quỹ lớp; không dùng cho số dư cá nhân học sinh | D-32 |

### 1.12 Điều chỉnh sau khóa sổ

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-ADJ-01 | Chuyển quỹ sau khi khóa sổ là **ngoại lệ**, phải tạo **chứng từ điều chỉnh** riêng | D-30 |
| BR-ADJ-02 | Không được mở khóa, sửa hoặc ghi đè giao dịch đã khóa sổ | D-30, D-36 |
| BR-ADJ-03 | Chứng từ điều chỉnh lưu: người thực hiện, người phê duyệt, thời gian, lý do, lịch sử điều chỉnh | D-30 |
| BR-ADJ-04 | Chứng từ điều chỉnh theo cùng luồng duyệt chuyển quỹ (GVCN lập, Quản trị duyệt) và có giữ chỗ | D-29, D-30, D-40 |
| BR-ADJ-05 | Thu hộ công nợ thuộc năm học đã khóa sổ là **ngoại lệ thứ hai**, ghi bằng **chứng từ thu hộ bổ sung** liên kết với khoản công nợ và lớp – năm học phát sinh | D-36 |
| BR-ADJ-06 | Chứng từ thu hộ bổ sung lưu: người thu, thời gian thu, **người xác nhận** (người giữ quỹ lớp gốc – BR-CASH-05), thông tin bàn giao tiền thực tế | D-36, D-43 |

### 1.13 Hủy, từ chối & đảo chứng từ

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-PND-01 | Chứng từ có giữ chỗ bị **từ chối hoặc hủy** thì giải phóng giữ chỗ tương ứng | D-47, D-48 |
| BR-PND-02 | **Người tạo** được hủy chứng từ của mình khi **còn chờ xử lý**; **người duyệt** được từ chối | D-57 |
| BR-PND-03 | Chứng từ **đã duyệt nhưng chưa hoàn tất** (phiếu hoàn trả chờ thực hiện) chỉ hủy theo **quy trình hủy riêng** (người đề nghị / duyệt: Q-49) | D-57 |
| BR-PND-04 | Chứng từ **đã hoàn tất** không được xóa / sửa trực tiếp; phải tạo **chứng từ điều chỉnh** hoặc **đảo giao dịch** liên kết chứng từ gốc, có lý do (người tạo / duyệt theo loại: Q-49) | D-57 |
| BR-PND-05 | Mỗi chứng từ gốc chỉ được đảo **một lần**; đảo giao dịch ghi ngược đúng ảnh hưởng của chứng từ gốc (quỹ lớp, công nợ, số dư cá nhân); đảo làm số dư quỹ âm: Q-52 | D-57 |

### 1.14 Xử lý đồng thời

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-CON-01 | Nhiều phiếu chi / chuyển quỹ tạo đồng thời không được làm tổng giữ chỗ + đã dùng vượt số dư quỹ lớp | D-40 |
| BR-CON-02 | Nhiều phiếu thu / thu hộ / báo chuyển khoản tạo đồng thời cho cùng khoản nợ không được phân bổ vượt số nợ còn có thể thu | D-39, D-48 |
| BR-CON-03 | Nhiều phiếu hoàn trả tạo đồng thời không được vượt số dư cá nhân khả dụng | D-47 |

## 2. Trạng thái chứng từ

Quy ước chung (D-57): chứng từ còn chờ → người tạo **hủy** (`Cancelled`) hoặc người duyệt **từ chối** (`Rejected`); chứng từ đã hoàn tất → không đổi trạng thái, chỉ được **đảo giao dịch** bằng chứng từ riêng liên kết chứng từ gốc. Tên trạng thái tiếng Anh dùng thống nhất với [database-workflows.md](../database/database-workflows.md).

**Phiếu thu (kể cả thu hộ, thu hộ bổ sung)**
```text
(PH báo chuyển khoản) --> PendingConfirmation [giữ chỗ khoản nợ]
        --thủ quỹ xác nhận--> Confirmed [cập nhật nợ; phần vượt → số dư cá nhân]
        --thủ quỹ từ chối--> Rejected [giải phóng]
        --phụ huynh hủy--> Cancelled [giải phóng]
(Thủ quỹ ghi trực tiếp) ----> Confirmed
Confirmed --(đảo giao dịch, Q-49)--> vẫn Confirmed; có chứng từ đảo liên kết
```

**Phiếu chi / Đề nghị chuyển quỹ / Chứng từ điều chỉnh chuyển quỹ** (giữ chỗ số dư quỹ lớp)
```text
PendingApproval [giữ chỗ] --duyệt--> Approved [chính thức, hoàn tất – phiếu chi: Q-50]
    --từ chối--> Rejected [giải phóng] --sửa & gửi lại--> PendingApproval [giữ chỗ]
    --người tạo hủy--> Cancelled [giải phóng]
Approved --(đảo giao dịch, Q-49)--> vẫn Approved; có chứng từ đảo liên kết
```

**Phiếu hoàn trả** (giữ chỗ số dư cá nhân – D-47, D-53)
```text
PendingApproval [giữ chỗ] --GVCN duyệt--> AwaitingPayment [vẫn giữ chỗ]
        --thủ quỹ xác nhận đã trả + minh chứng--> Completed [giảm số dư thực tế, giải phóng giữ chỗ]
        --quy trình hủy riêng (Q-49)--> Cancelled [giải phóng]
    --từ chối--> Rejected [giải phóng]
    --người tạo hủy--> Cancelled [giải phóng]
Completed --(đảo giao dịch, Q-49)--> vẫn Completed; có chứng từ đảo liên kết
```

**Đề nghị miễn giảm**
```text
PendingApproval --duyệt--> Approved --(đảo giao dịch, Q-49)--> vẫn Approved
    --từ chối--> Rejected --sửa & gửi lại--> PendingApproval
    --người tạo hủy--> Cancelled
```

**Chứng từ bàn giao tiền thừa / bàn giao tiền thu hộ**
```text
PendingConfirmation --người nhận xác nhận, khớp--> Completed
    --không khớp (lập biên bản)--> Discrepancy --các phương án được duyệt đủ chênh lệch--> Completed
    --người tạo hủy--> Cancelled
```

**Biên bản chênh lệch & phương án xử lý (D-50, D-51)**
```text
Biên bản:   Open --(phương án được duyệt, tổng = chênh lệch – Q-54)--> Resolved
Phương án:  Proposed --duyệt (không phải người đề xuất)--> Approved [ghi chứng từ điều chỉnh nếu loại có ảnh hưởng số dư]
                     --từ chối--> Rejected [biên bản vẫn Open, số dư không đổi]
                     --người đề xuất hủy--> Cancelled
```
Người duyệt: GVCN (phiếu chi, hoàn trả, miễn giảm, phương án chênh lệch) · Quản trị (chuyển quỹ, điều chỉnh sau khóa sổ, phương án chênh lệch khi lớp gốc đã khóa sổ / không còn GVCN).

**Thu khác:** Confirmed (không cần duyệt) → (đảo giao dịch, Q-49).

## 3. Quy trình nghiệp vụ

### P-01 Thiết lập năm học & mời tài khoản
1. Quản trị tạo năm học, tạo lớp và gán GVCN; mời GVCN qua email.
2. GVCN mở liên kết, đặt mật khẩu.
3. GVCN thêm học sinh và mời phụ huynh qua email, gắn với học sinh.
4. Phụ huynh kích hoạt tài khoản, thấy lớp và con mình.
5. GVCN phân công một phụ huynh làm thủ quỹ.

### P-02 Thu tiền theo đợt
1. GVCN hoặc thủ quỹ tạo đợt thu.
2. Hệ thống tạo công nợ cho từng học sinh, tự trừ số dư cá nhân **khả dụng** nếu có, và gửi thông báo cho phụ huynh.
3. Phụ huynh nộp tiền:
   - **Tiền mặt:** thủ quỹ ghi phiếu thu, liên kết khoản công nợ.
   - **Chuyển khoản:** phụ huynh báo kèm biên lai; hệ thống **giữ chỗ** khoản nợ; thủ quỹ đối chiếu rồi xác nhận (cập nhật nợ) hoặc từ chối (giải phóng).
4. Hệ thống phân bổ tiền vào công nợ (không vượt số nợ còn có thể thu); phần vượt vào số dư cá nhân; cập nhật tình trạng nộp.
5. Đến hạn: thủ quỹ hoặc GVCN gửi thông báo nhắc nộp.
6. Đóng đợt thu khi đã thu xong.

### P-03 Miễn giảm
1. Thủ quỹ hoặc GVCN lập đề nghị miễn giảm (toàn phần / một phần, có lý do).
2. GVCN duyệt; công nợ của học sinh được giảm tương ứng.

### P-04 Chi tiền
1. Thủ quỹ lập phiếu chi kèm chứng từ. Hệ thống kiểm tra số tiền ≤ số dư khả dụng rồi **giữ chỗ**.
2. Nếu thủ quỹ sửa số tiền khi phiếu đang chờ: kiểm tra lại, hợp lệ thì cập nhật giữ chỗ.
3. GVCN duyệt (giữ chỗ → chính thức) hoặc từ chối có lý do (giải phóng giữ chỗ).
4. Phiếu đã duyệt được tính vào quỹ; phụ huynh thấy phiếu và chứng từ.

### P-05 Thu khác
1. Thủ quỹ ghi khoản thu khác kèm nguồn và chứng từ, tính vào quỹ ngay.

### P-06 Hoàn trả tiền thừa
1. Thủ quỹ đang quản lý số dư cá nhân của học sinh lập phiếu hoàn trả; hệ thống kiểm tra số tiền ≤ số dư cá nhân khả dụng rồi **giữ chỗ**.
2. GVCN duyệt → **chờ thực hiện hoàn trả** (vẫn giữ chỗ); hoặc từ chối → giải phóng giữ chỗ.
3. Thủ quỹ trả tiền cho phụ huynh, **xác nhận đã trả** và đính kèm **minh chứng** → **hoàn tất**: số dư thực tế giảm, giải phóng giữ chỗ.
4. Hủy phiếu đã duyệt nhưng chưa trả: quy trình hủy riêng (Q-49). Số dư quỹ lớp không đổi trong mọi bước.

### P-07 Điều chỉnh sai sót (năm học chưa khóa sổ)
1. Chứng từ còn chờ: người tạo hủy, hoặc người duyệt từ chối.
2. Chứng từ đã hoàn tất: lập **đảo giao dịch** có lý do, liên kết chứng từ gốc (người tạo / duyệt: Q-49); chứng từ gốc giữ nguyên.
3. Lập chứng từ mới đúng; lịch sử giữ cả chứng từ gốc, chứng từ đảo và chứng từ mới.

### P-08 Chốt sổ năm học
1. GVCN đảm bảo không còn phiếu chờ duyệt / xác nhận.
2. GVCN đối chiếu báo cáo quỹ lớp, báo cáo số dư cá nhân và báo cáo người giữ tiền với tiền thực tế.
3. GVCN chốt sổ; năm học của lớp bị khóa.
4. Số dư quỹ lớp **giữ lại** tại lớp – năm học đó. Muốn chuyển đi thì dùng P-10 / P-13.
5. Quản trị phân công người giữ quỹ cho lớp – năm học đã kết thúc (P-16).

### P-09 Chuyển lớp / lên lớp
1. Quản trị chuyển học sinh sang lớp mới (giữa năm) hoặc lớp của năm học mới (lên lớp).
2. Liên kết phụ huynh đi theo học sinh; lịch sử ở lớp cũ được giữ nguyên.
3. Công nợ cũ đi theo học sinh nhưng vẫn thuộc lớp – năm học gốc → thu theo P-11 / P-14.
4. Thao tác của Quản trị là **xác nhận** chuyển lớp (D-56). Sau đó số dư cá nhân chuyển sang **chờ bàn giao** → bàn giao theo P-12 (nghiệp vụ riêng).

### P-10 Chuyển quỹ giữa các lớp (năm học chưa khóa sổ)
1. GVCN lớp chuyển lập đề nghị: lớp nhận, số tiền, lý do. Hệ thống kiểm tra số tiền ≤ số dư khả dụng rồi **giữ chỗ**.
2. Quản trị phê duyệt hoặc từ chối (giải phóng giữ chỗ).
3. Khi duyệt: ghi khoản ra ở lớp chuyển và khoản vào ở lớp nhận, liên kết cùng một giao dịch; GVCN lớp nhận nhận thông báo.

### P-11 Thu hộ công nợ cũ (năm học gốc chưa khóa sổ)
1. Thủ quỹ lớp mới xem công nợ cũ của học sinh đang học lớp mình.
2. Phụ huynh nộp tiền; thủ quỹ lớp mới ghi phiếu thu hộ, liên kết đúng khoản công nợ gốc.
3. Hệ thống phân bổ không vượt số nợ còn có thể thu (chống thu trùng, kể cả phần đang giữ chỗ); phần vượt vào số dư cá nhân (D-48); ghi tiền đã phân bổ vào quỹ lớp – năm học gốc; lưu người thu hộ, lớp thu hộ; lớp thu hộ hiển thị "Đã thu hộ".
4. Thủ quỹ lớp mới bàn giao tiền thực tế về lớp gốc theo P-15.
5. Thủ quỹ lớp cũ cũng có thể tự thu khoản công nợ này nếu còn được phân công (D-39); hệ thống chống thu trùng giữa hai bên. Thu vượt: phần vượt là số dư cá nhân, thủ quỹ lớp cũ lập bàn giao số dư cá nhân cho thủ quỹ lớp mới (D-55). Số nợ còn có thể thu = 0: không cho tạo phiếu thanh toán nợ; tiền đã nhận ghi theo nghiệp vụ số dư cá nhân (D-54).

### P-12 Bàn giao tiền thừa cá nhân
1. Học sinh chuyển lớp có số dư cá nhân > 0 → số dư ở trạng thái **chờ bàn giao**.
2. Thủ quỹ lớp cũ lập chứng từ bàn giao: số dư trước / sau, người bàn giao, người nhận (thủ quỹ lớp mới), số tiền, thời gian.
3. Thủ quỹ lớp mới nhận tiền:
   - **Khớp** số dư hệ thống → xác nhận → **hoàn tất**, không cần GVCN duyệt; số dư chuyển sang **khả dụng**.
   - **Không khớp** → lập biên bản chênh lệch → `Discrepancy` → xử lý theo P-17; số dư vẫn chưa khả dụng.

### P-13 Chuyển quỹ sau khi khóa sổ (chứng từ điều chỉnh)
1. GVCN lớp chuyển lập chứng từ điều chỉnh: lớp nhận, số tiền, **lý do**; hệ thống giữ chỗ.
2. Quản trị phê duyệt hoặc từ chối.
3. Khi duyệt: ghi nhận như P-10 bước 3, đánh dấu **sau khóa sổ**; không sửa giao dịch cũ; lưu lịch sử điều chỉnh.

### P-14 Thu hộ công nợ sau khi khóa sổ (chứng từ thu hộ bổ sung)
1. Thủ quỹ lớp mới thu tiền công nợ cũ thuộc năm học đã khóa sổ.
2. Lập **chứng từ thu hộ bổ sung**, liên kết khoản công nợ và lớp – năm học phát sinh; lưu người thu, thời gian thu.
3. Hệ thống ghi phần đã phân bổ vào quỹ lớp – năm học gốc, đánh dấu **sau khóa sổ**; không mở khóa / sửa giao dịch cũ; phần vượt vào số dư cá nhân.
4. Bàn giao tiền thực tế theo P-15; **người giữ quỹ lớp gốc** (người được Quản trị phân công) xác nhận; chứng từ lưu người xác nhận, thời gian, số tiền thực nhận.

### P-15 Bàn giao tiền thu hộ về lớp gốc
1. Thủ quỹ đã thu hộ lập chứng từ bàn giao: người giao, người nhận, số tiền dự kiến, thời gian.
2. Người nhận là **người giữ quỹ lớp gốc**: thủ quỹ hiện tại nếu năm học chưa kết thúc; người được Quản trị phân công nếu đã kết thúc. Người thu hộ **không** được tự xác nhận. Nếu người thu **chính là** người giữ quỹ lớp gốc: không lập bàn giao, phiếu thu ghi người giữ tiền là người đó (D-58).
3. Người nhận ghi số tiền thực nhận:
   - **Khớp** → xác nhận → **hoàn tất**. Lớp đã kết thúc mà chưa có người giữ quỹ → chưa hoàn tất được (BR-CASH-04).
   - **Không khớp** → lập biên bản chênh lệch → `Discrepancy` → P-17.
4. Báo cáo người giữ tiền cập nhật: tiền thu hộ chưa bàn giao giảm khi bàn giao hoàn tất.

### P-16 Phân công người giữ quỹ lớp – năm học đã kết thúc
1. Sau khi lớp – năm học kết thúc, Quản trị phân công người giữ quỹ: thủ quỹ cũ còn được phân công hoặc người khác; ghi rõ lớp và năm học.
2. Quyền mặc định của người giữ quỹ: xác nhận bàn giao (`CashHandover.Confirm`), xem báo cáo lớp – năm học được giao. Quyền thu nợ chỉ có khi được cấp riêng. Không có các quyền khác của Thủ quỹ.
3. Người giữ quỹ nhận bàn giao tiền thu hộ (P-15) cho lớp – năm học đó.
4. Thay đổi người giữ quỹ: lưu lịch sử phân công, không mất lịch sử giao dịch.

### P-17 Xử lý chênh lệch bàn giao
1. Người nhận lập **biên bản**: số dự kiến, số thực nhận, chênh lệch, lý do, người bàn giao / người nhận; đính kèm minh chứng nếu có. Bàn giao chuyển sang `Discrepancy`; không số dư nào bị tự động điều chỉnh.
2. Đề xuất **phương án** (người đề xuất: Q-47), thuộc một trong 4 loại: sửa sai trước ghi nhận · bàn giao bổ sung · ghi nhận thiếu hụt · ghi nhận tiền thừa (D-51; kết hợp nhiều phương án: Q-54).
3. Duyệt phương án: **GVCN lớp gốc**; lớp gốc đã khóa sổ hoặc không còn GVCN → **Quản trị**. Người đề xuất không tự duyệt (D-50).
4. Bị từ chối: biên bản vẫn mở, số dư không đổi; có thể đề xuất phương án khác.
5. Được duyệt: ghi chứng từ tương ứng với loại phương án (ảnh hưởng số dư: Q-48); không sửa chứng từ gốc. Khi chênh lệch được xử lý hết → biên bản `Resolved`, bàn giao `Completed`; lưu lịch sử đầy đủ.
