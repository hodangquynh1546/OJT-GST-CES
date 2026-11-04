# Quyết định nghiệp vụ & Câu hỏi cần xác nhận

> Phiên bản: 0.7 · Ngày: 2026-11-03
> Các quyết định được chủ dự án xác nhận qua 6 lần duyệt cùng ngày 2026-11-03: lần 1 (D-01 → D-21), lần 2 (D-22 → D-28, điều chỉnh D-20), lần 3 (D-29 → D-35), lần 4 (D-36 → D-42, điều chỉnh D-33), lần 5 (D-43 → D-49, làm rõ D-41), lần 6 (D-50 → D-58, điều chỉnh D-28, D-44).

## 1. Quyết định đã chốt

### 1.1 Phạm vi, tài khoản, vai trò

| Mã | Từ câu hỏi | Quyết định |
|---|---|---|
| D-01 | Q-01 | Một trường, nhiều lớp. Có 4 vai trò: Quản trị hệ thống, GVCN, Thủ quỹ, Phụ huynh |
| D-02 | Q-02 | Quỹ quản lý theo năm học |
| D-03 | Q-14, F-1 | Tài khoản tạo qua lời mời email: Quản trị mời GVCN, GVCN mời phụ huynh. Đăng nhập bằng email + mật khẩu |
| D-04 | Q-04 | Một phụ huynh có thể có nhiều con; một học sinh có thể có nhiều phụ huynh |
| D-05 | Q-03, F-2 | Mỗi lớp có một thủ quỹ, là phụ huynh của lớp, do GVCN phân công trên hệ thống |
| D-06 | Q-15 | Chỉ hỗ trợ tiếng Việt |
| D-22 | Q-20 | Quản trị tạo năm học, tạo lớp và gán GVCN |
| D-23 | Q-20 | Quản trị cập nhật học sinh khi chuyển lớp hoặc lên lớp |
| D-33 | Q-24 | Quản trị được **xem** báo cáo quỹ của tất cả lớp; quyền xem báo cáo không cho phép tạo, sửa, duyệt hoặc hủy giao dịch. **Đã bổ sung bởi D-41** |
| D-41 | Q-31 | Quản trị được **xuất Excel / PDF** các báo cáo thuộc phạm vi quyền xem. Quyền xem / xuất báo cáo **không** tự động cấp quyền xem danh tính phụ huynh / học sinh trong từng biên nhận. `Receipt.View` là permission riêng, **không** cấp cho Quản trị khi chưa có quyết định riêng. **Làm rõ bởi D-46:** giới hạn danh tính chỉ áp dụng cho biên nhận / chứng từ chia sẻ cho phụ huynh, không cấm báo cáo nội bộ của Quản trị |
| D-46 | Q-35 | Quản trị được xem **tên học sinh** trong báo cáo thu, báo cáo số dư cá nhân và lịch sử thao tác **nếu có permission tương ứng**. Không tự động cấp quyền xem danh tính trong biên nhận. Việc xem thông tin chi tiết được kiểm soát bằng permission và có **lịch sử truy cập** |

### 1.2 Thu, miễn giảm, tiền thừa

| Mã | Từ câu hỏi | Quyết định |
|---|---|---|
| D-07 | Q-06, F-3 | Cả GVCN và thủ quỹ được tạo đợt thu |
| D-08 | Q-07 | Cho nộp nhiều lần và nộp một phần |
| D-09 | Q-07, F-4 | Tiền nộp thừa thành số dư riêng của học sinh, tự động trừ vào đợt thu sau; có thể hoàn trả |
| D-10 | Q-09, F-5 | Miễn giảm do GVCN duyệt, bắt buộc có lý do; hỗ trợ miễn toàn phần và giảm một phần |
| D-11 | Q-08 | Chuyển khoản do thủ quỹ xác nhận thủ công kèm ảnh biên lai; chưa tích hợp ngân hàng |
| D-12 | Q-10 | Nhắc nộp tiền bằng thông báo trong hệ thống |
| D-13 | Q-19, F-9 | Có thu khác (không gắn học sinh); không cần duyệt nhưng bắt buộc ghi nguồn và có chứng từ |
| D-21 | Phát sinh từ D-09 | Hoàn trả: thủ quỹ lập phiếu hoàn trả, GVCN duyệt |
| D-34 | Q-25 | Mỗi đợt thu bắt buộc có tên, số tiền, hạn nộp và ít nhất một học sinh (BR-REC-01) |

### 1.3 Chi & số dư khả dụng

| Mã | Từ câu hỏi | Quyết định |
|---|---|---|
| D-15 | Q-05 | Mọi phiếu chi phải được GVCN duyệt mới tính vào quỹ |
| D-16 | Q-17 | Phiếu chi bắt buộc có chứng từ |
| D-17 | Q-18 | Không cho chi vượt số dư quỹ |
| D-18 | Q-11, F-6 | Quản trị tạo danh mục chi chung; thủ quỹ được tạo danh mục riêng cho lớp |
| D-47 | Q-36 | **Giữ chỗ số dư cá nhân:** phiếu hoàn trả giữ chỗ ngay khi được tạo và chuyển sang chờ duyệt. Số dư cá nhân khả dụng = số dư thực tế − số đang giữ chỗ. Bị từ chối / hủy: giải phóng. Hoàn trả hoàn tất: giảm số dư thực tế. Không cho nhiều phiếu hoàn trả vượt số dư khả dụng |
| D-40 | Q-30 | **Số dư khả dụng** = số dư quỹ − các khoản **giữ chỗ** (phiếu chi và chuyển quỹ đang chờ duyệt). Khi duyệt: giữ chỗ thành giao dịch chính thức. Khi bị từ chối / hủy: giữ chỗ được giải phóng. Không cho tạo giao dịch làm tổng đã dùng + giữ chỗ vượt số dư quỹ. Phải xử lý trường hợp nhiều yêu cầu được tạo đồng thời |

### 1.4 Báo cáo, chốt sổ, chuyển lớp, chuyển quỹ

| Mã | Từ câu hỏi | Quyết định |
|---|---|---|
| D-14 | Q-16 | Học sinh chuyển lớp / nghỉ học: lưu lại lịch sử |
| D-19 | Q-12, F-7 | Phụ huynh xem sổ thu chi, khoản nộp của con, chứng từ chi; phần thu chỉ hiện tổng theo đợt, không có tên; không xem nợ của phụ huynh khác |
| D-20 | Q-13, F-8 | GVCN chốt sổ cuối năm; công nợ chưa thanh toán đi sang năm học sau. **Đã điều chỉnh bởi D-25:** số dư quỹ lớp không tự động chuyển |
| D-24 | Q-20 | Công nợ và số dư riêng của học sinh đi theo học sinh sang lớp mới / năm học mới |
| D-25 | Q-20 | Số dư quỹ lớp giữ lịch sử tại lớp và năm học phát sinh, không tự động chuyển sang lớp khác |
| D-26 | Q-20 | Chuyển số dư quỹ giữa các lớp là nghiệp vụ riêng, có ghi nhận và phê duyệt |
| D-29 | Q-21 | Chuyển quỹ: GVCN lớp chuyển lập đề nghị, Quản trị phê duyệt, GVCN lớp nhận được thông báo; lớp nhận không bắt buộc xác nhận |
| D-30 | Q-22 | Cho phép chuyển quỹ sau khi khóa sổ (ngoại lệ): chứng từ điều chỉnh riêng; không sửa / ghi đè giao dịch đã khóa; lưu người thực hiện, người phê duyệt, thời gian, lý do, lịch sử; báo cáo phân biệt trước / sau khóa sổ |

### 1.5 Công nợ cũ, thu hộ, bàn giao tiền

| Mã | Từ câu hỏi | Quyết định |
|---|---|---|
| D-31 | Q-23 (1) | Công nợ cũ đi theo học sinh. Thủ quỹ lớp mới có thể thu hộ; khoản thu ghi vào quỹ lớp – năm học phát sinh công nợ, không thành khoản thu mới của lớp hiện tại; lưu người thu hộ và lớp – năm học sở hữu công nợ |
| D-32 | Q-23 (2) | Tiền thừa là số dư cá nhân của học sinh, không thuộc quỹ lớp. Khi chuyển lớp, bàn giao cho thủ quỹ lớp mới bằng chứng từ bàn giao riêng. Không dùng nghiệp vụ chuyển quỹ; không cộng vào số dư quỹ lớp |
| D-36 | Q-26 | Cho phép **thu hộ công nợ cũ sau khi năm học đã khóa sổ** (ngoại lệ thứ hai). Không mở khóa / sửa giao dịch đã khóa. Ghi bằng **chứng từ thu hộ bổ sung**, liên kết với khoản công nợ và lớp – năm học phát sinh. Lưu người thu, thời gian thu, người xác nhận, thông tin bàn giao tiền thực tế. Báo cáo phân biệt trước / sau khóa sổ |
| D-37 | Q-27 | Tiền thu hộ ghi sổ vào quỹ lớp – năm học gốc. **Tiền thực tế** phải bàn giao về **người giữ quỹ của lớp gốc** hoặc người được Quản trị phân công, bằng chứng từ (người giao, người nhận, số tiền, thời gian). **Quản trị phân công người giữ quỹ** cho lớp – năm học đã kết thúc. Không mặc định tiền quỹ cũ do thủ quỹ lớp mới quản lý. Phân biệt số tiền ghi sổ với người đang thực tế giữ tiền |
| D-38 | Q-28 | Bàn giao tiền thừa: **thủ quỹ lớp cũ lập**, **thủ quỹ lớp mới xác nhận** đã nhận. Không bắt buộc GVCN duyệt **nếu số tiền khớp** số dư trên hệ thống. Lưu người bàn giao, người nhận, số tiền, thời gian, trạng thái. Khi chưa hoàn tất: số dư **chưa được tự trừ** vào đợt thu lớp mới; chỉ sau khi người nhận xác nhận mới dùng được |
| D-43 | Q-32 | **Xác nhận nhận tiền thu hộ** (kể cả sau khóa sổ) là **người giữ quỹ của lớp gốc** khi thực tế nhận tiền: năm học chưa kết thúc → thủ quỹ hiện tại của lớp gốc; đã kết thúc → người được Quản trị phân công. **Người thu hộ không được tự xác nhận** bàn giao của mình. Lưu người xác nhận, thời gian, số tiền thực nhận. `CashHandover.Confirm` chỉ cấp cho người nhận tiền được phân công |
| D-44 | Q-33 | **Chênh lệch khi bàn giao tiền:** không tự động điều chỉnh số dư. Lập **biên bản** ghi số dự kiến, số thực nhận, chênh lệch, lý do, người bàn giao / người nhận; cho đính kèm minh chứng. Bàn giao chuyển trạng thái **`Discrepancy`**, chưa hoàn tất. GVCN lớp gốc xem xét và đề xuất xử lý; nếu lớp gốc đã khóa sổ hoặc không còn GVCN phụ trách → Quản trị phê duyệt phương án. Chỉ ghi điều chỉnh sau khi được phê duyệt. Không sửa trực tiếp chứng từ gốc đã khóa sổ |
| D-45 | Q-34 | **Người giữ quỹ lớp cũ:** là thủ quỹ cũ còn được phân công hoặc người khác do Quản trị chỉ định; phân công rõ lớp và năm học. Có quyền xác nhận bàn giao và xem báo cáo trong phạm vi được giao. Chỉ được thu nợ cũ khi có quyền thu tương ứng. Không mặc nhiên có toàn bộ quyền Thủ quỹ. Tách riêng `CashHandover.Confirm`, quyền xem báo cáo và quyền thu nợ |
| D-48 | Q-37 | **Giữ chỗ khoản nợ:** phiếu thu đang chờ xác nhận giữ chỗ khoản nợ tương ứng. Số nợ còn có thể thu = nợ chưa thanh toán − nợ đang giữ chỗ. Bị từ chối / hủy: giải phóng. Xác nhận thành công: cập nhật nợ còn lại. Thu vượt nợ cũ: phần vượt vào **số dư cá nhân** theo D-09, không vào quỹ lớp. Ví dụ: nợ cũ 500.000 đ, thực thu 600.000 đ → 500.000 đ trả nợ cũ, 100.000 đ thành số dư cá nhân |
| D-39 | Q-29 | Thủ quỹ lớp mới thu hộ công nợ cũ của học sinh đang học lớp mình. **Thủ quỹ lớp cũ vẫn được thu** khoản công nợ thuộc lớp cũ nếu còn được phân công và có permission tương ứng. Phải **chống thu trùng** một khoản công nợ. Mỗi khoản thu liên kết với công nợ gốc. Đổi người phụ trách không làm mất lịch sử |

### 1.6 Quy tắc thông lệ & điểm suy ra đã xác nhận

| Mã | Nội dung |
|---|---|
| D-27 | 6 quy tắc thông lệ: BR-GEN-02, BR-GEN-03, BR-GEN-04, BR-REC-02, BR-EXP-04, BR-RPT-04 |
| D-28 | 7 điểm suy ra: BR-CLS-02, BR-CLS-03, BR-EXP-05, BR-REC-07 (người đề nghị), BR-ACC-05, BR-ACC-06, BR-ACC-09 |
| D-35 | 3 điểm suy ra: liên kết phụ huynh đi theo học sinh và xem lịch sử lớp cũ; phiếu chuyển quỹ ghi ở hai lớp, liên kết một giao dịch; chuyển quỹ không vượt số dư khả dụng |
| D-49 | 5 điểm suy ra: (1) khi năm học chưa kết thúc, thủ quỹ hiện tại là người giữ quỹ của lớp và có `CashHandover.Confirm` trong phạm vi được giao; (2) lớp đã kết thúc năm học phải có người giữ quỹ được phân công mới **hoàn tất** được bàn giao tiền thu hộ; (3) sửa số tiền phiếu chi chờ duyệt thì giữ chỗ cập nhật theo số mới **sau khi kiểm tra hợp lệ**; (4) khi chuyển lớp, số dư cá nhân chuyển sang **chờ bàn giao sau khi việc chuyển lớp được xác nhận**, không dùng được ở lớp mới trước khi bàn giao hoàn tất; (5) Quản trị có `Collection.View`, `Expense.View`, nhưng không mặc nhiên có `Receipt.View` hay quyền xem danh tính trong biên nhận |
| D-42 | 3 điểm suy ra: (1) thủ quỹ chỉ xem công nợ cũ của học sinh đang học lớp mình; (2) không mặc định cấp `Receipt.View` cho Quản trị, tách khỏi quyền xem báo cáo; (3) thu hộ ghi vào sổ quỹ lớp gốc, lớp thu hộ chỉ hiển thị "Đã thu hộ", không tính vào số dư quỹ lớp thu hộ |

### 1.7 Quyết định lần 6 (đóng Q-38 → Q-46)

| Mã | Từ câu hỏi | Quyết định |
|---|---|---|
| D-50 | Q-38 | **Duyệt phương án xử lý chênh lệch:** GVCN lớp gốc duyệt; lớp đã khóa sổ hoặc không còn GVCN phụ trách → Quản trị duyệt. **Người đề xuất không tự duyệt.** Bị từ chối: chênh lệch vẫn chưa xử lý, không làm thay đổi số dư. (Điều chỉnh D-44: GVCN lớp gốc là người **duyệt**, người đề xuất: Q-47) |
| D-51 | Q-39 | **4 phương án xử lý chênh lệch:** sửa sai trước ghi nhận, bàn giao bổ sung, ghi nhận thiếu hụt, ghi nhận tiền thừa. Mỗi phương án có chứng từ, người duyệt và ảnh hưởng số dư riêng. Không sửa trực tiếp chứng từ gốc đã hoàn tất (ảnh hưởng số dư cụ thể của từng phương án: Q-48) |
| D-52 | Q-40 | Quản trị có `Collection.View`, `Expense.View` toàn trường và **được xem danh tính học sinh trong báo cáo nội bộ**. Người giữ quỹ xem báo cáo theo lớp – năm học được phân công. Xuất báo cáo cần `Report.Export`. Không mặc nhiên cấp `Receipt.View` |
| D-53 | Q-41 | **Phiếu hoàn trả** chỉ `Completed` sau khi **thủ quỹ xác nhận đã thực tế trả tiền** và lưu minh chứng. GVCN duyệt chỉ đưa phiếu sang **chờ thực hiện hoàn trả** |
| D-54 | Q-42 | **Không cho tạo phiếu thanh toán nợ** khi số nợ còn có thể thu = 0. Nếu thực tế đã nhận tiền: ghi nhận theo nghiệp vụ **số dư cá nhân**. Thu vượt nợ: tách phần thanh toán nợ và phần tiền thừa |
| D-55 | Q-43 | Tiền vượt nợ (kể cả do thủ quỹ lớp cũ thu) thuộc **số dư cá nhân**, không thuộc quỹ lớp cũ. Thủ quỹ lớp cũ lập **bàn giao số dư cá nhân**; thủ quỹ lớp mới xác nhận; chỉ sau khi xác nhận mới khả dụng tại lớp mới |
| D-56 | Q-44 | **Quản trị xác nhận** việc chuyển lớp; không có bước xác nhận riêng từ GVCN lớp mới. Bàn giao số dư cá nhân là nghiệp vụ riêng |
| D-57 | Q-45 | **Hủy chứng từ:** người tạo được hủy chứng từ của mình khi **còn chờ xử lý**; người duyệt được **từ chối**. Chứng từ **đã duyệt nhưng chưa hoàn tất** phải có **quy trình hủy riêng**. Chứng từ **đã hoàn tất** không được xóa / sửa trực tiếp; phải tạo **chứng từ điều chỉnh** hoặc **đảo giao dịch** có liên kết chứng từ gốc. (Điều chỉnh D-27/D-28: "hủy phiếu đã duyệt / hợp lệ" nay thực hiện bằng đảo giao dịch; người tạo / duyệt: Q-49) |
| D-58 | Q-46 | Người thu **đồng thời là** người giữ quỹ lớp gốc: **không cần** bàn giao tiền thu hộ vì không có chuyển giao vật lý. Phiếu thu và thông tin người giữ tiền vẫn phải được ghi nhận |

## 2. Câu hỏi đã chốt (lưu vết)

| Mã | Kết quả |
|---|---|
| Q-01 → Q-19, F-1 → F-9 | D-01 → D-21 |
| Q-20 | D-22 → D-26 |
| Q-21 · Q-22 · Q-23 · Q-24 · Q-25 | D-29 · D-30 · D-31, D-32 · D-33 · D-34 |
| Q-26 · Q-27 · Q-28 · Q-29 · Q-30 · Q-31 | D-36 · D-37 · D-38 · D-39 · D-40 · D-41 |
| Q-32 · Q-33 · Q-34 · Q-35 · Q-36 · Q-37 | D-43 · D-44 · D-45 · D-46 · D-47 · D-48 |
| Q-38 · Q-39 · Q-40 · Q-41 · Q-42 · Q-43 · Q-44 · Q-45 · Q-46 | D-50 · D-51 · D-52 · D-53 · D-54 · D-55 · D-56 · D-57 · D-58 |

## 3. Câu hỏi còn mở

Phát sinh khi đối chiếu D-50 → D-58 với tài liệu hiện có và khi thiết kế database. "Đề xuất của BA" chỉ là gợi ý, **chưa phải quyết định**; thiết kế database hiện dùng đề xuất này làm **giả định tạm** (đánh dấu trong [database-design-decisions.md](../database/database-design-decisions.md)).

| Mã | Câu hỏi | Chỗ trống / mâu thuẫn | Đề xuất của BA | Trạng thái |
|---|---|---|---|---|
| Q-47 | **Ai đề xuất** phương án xử lý chênh lệch? | D-44 nói GVCN lớp gốc đề xuất; D-50 nói GVCN lớp gốc duyệt và người đề xuất không tự duyệt → GVCN không thể vừa đề xuất vừa duyệt | Người nhận bàn giao (người lập biên bản) đề xuất; GVCN lớp gốc (hoặc Quản trị) duyệt | Mở |
| Q-48 | **Ảnh hưởng số dư** của 4 phương án (D-51), nhất là: thiếu hụt làm giảm quỹ lớp gốc hay số dư cá nhân, có ghi nợ cho người bàn giao không; tiền thừa ghi tăng vào đâu | D-51 nói "ảnh hưởng số dư riêng" nhưng chưa nêu cụ thể | Sửa sai: không đổi số dư · Bàn giao bổ sung: không đổi số dư, chỉ hoàn tất bàn giao · Thiếu hụt: giảm quỹ lớp gốc (bàn giao tiền thu hộ) hoặc giảm số dư cá nhân (bàn giao tiền thừa) · Tiền thừa: tăng tương ứng ở cùng đối tượng | Mở |
| Q-49 | (a) **Quy trình hủy riêng** cho chứng từ đã duyệt nhưng chưa hoàn tất (phiếu hoàn trả chờ thực hiện): ai đề nghị, ai duyệt? (b) **Đảo giao dịch** cho từng loại chứng từ đã hoàn tất (phiếu thu, thu khác, phiếu chi, miễn giảm, chuyển quỹ, hoàn trả): ai tạo, ai duyệt? | D-57 yêu cầu quy trình riêng nhưng chưa nêu người thực hiện | (a) Thủ quỹ đề nghị, GVCN duyệt. (b) Giữ nguyên người có quyền "hủy" trước đây: thủ quỹ tạo đảo phiếu thu / thu khác (GVCN duyệt); GVCN tạo đảo phiếu chi / miễn giảm / hoàn trả (Quản trị duyệt); chuyển quỹ: GVCN lớp chuyển tạo, Quản trị duyệt | Mở |
| Q-50 | **Phiếu chi** đã duyệt có cần bước "thủ quỹ xác nhận đã chi tiền" như phiếu hoàn trả (D-53) không? | D-53 thêm bước thực hiện cho hoàn trả, phiếu chi chưa có | Không: phiếu chi hoàn tất khi GVCN duyệt (giữ D-15) | Mở |
| Q-51 | **Miễn giảm** được duyệt khi học sinh đã nộp một phần / đủ: phần đã nộp vượt số phải nộp mới xử lý thế nào? | Chưa có quy tắc; miễn giảm có thể làm số đã nộp lớn hơn số phải nộp | Chặn: số miễn giảm ≤ nợ chưa thanh toán − nợ đang giữ chỗ | Mở |
| Q-52 | Đảo giao dịch / ghi nhận thiếu hụt có được làm **số dư quỹ lớp âm** không (ví dụ đảo phiếu thu sau khi tiền đã chi)? | D-17 chỉ cấm phiếu chi vượt số dư | Không cho âm: chặn đảo / thiếu hụt nếu vượt số dư khả dụng; phải xử lý bằng nghiệp vụ khác | Mở |
| Q-53 | Một phiếu thu có được nộp cho **nhiều học sinh** (anh chị em) không? | Chưa quy định | Không: mỗi phiếu thu cho một học sinh; phụ huynh nhiều con lập nhiều phiếu | Mở |
| Q-54 | Một chênh lệch có được xử lý bằng **nhiều phương án kết hợp** (ví dụ bàn giao bổ sung một phần + ghi nhận thiếu hụt phần còn lại) không? | D-51 liệt kê phương án, chưa nói có kết hợp | Có: nhiều phương án đã duyệt cộng lại bằng đúng số chênh lệch thì chênh lệch mới được đóng | Mở |
| Q-55 | **Miễn giảm do GVCN đề nghị** thì ai duyệt? | BR-REC-07 cho GVCN vừa đề nghị vừa duyệt, nhưng nguyên tắc phân quyền 2 yêu cầu người lập ≠ người duyệt (phát sinh khi thiết kế CHECK `ApprovedBy <> CreatedBy`) | Quản trị duyệt khi người đề nghị là GVCN | Mở |
| Q-56 | Học sinh **chuyển lớp khi đang có phiếu hoàn trả chờ** (chờ duyệt / chờ thực hiện) ở lớp cũ: phiếu xử lý tiếp ở lớp cũ hay hủy để lớp mới lập lại? | D-38 / D-56 chưa nói trường hợp này (phát sinh khi thiết kế `PendingHandoverAmount`) | Xử lý tiếp ở lớp cũ (vẫn giữ chỗ); chỉ phần số dư còn lại chuyển sang chờ bàn giao | Mở |
