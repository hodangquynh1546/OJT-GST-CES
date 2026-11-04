# Yêu cầu chức năng – School Fund Management

> Phiên bản: 0.7 (đã cập nhật theo quyết định D-01 → D-58) · Ngày: 2026-11-03
> Tài liệu liên quan: [Vai trò & Permission](../business-analysis/roles-and-permissions.md) · [Quy tắc & Quy trình nghiệp vụ](../business-analysis/business-rules-and-processes.md) · [Quyết định & Câu hỏi](open-questions.md)

## 1. Mục tiêu

Hệ thống giúp các lớp trong một trường quản lý **quỹ lớp** minh bạch:
- Ghi nhận các khoản **thu** (từ phụ huynh và nguồn khác) và các khoản **chi** của lớp.
- Theo dõi ai đã nộp, ai chưa nộp, số dư quỹ, tiền nộp thừa và **ai đang thực tế giữ tiền**.
- Cho phụ huynh xem tình hình quỹ mà không lộ thông tin nợ của gia đình khác.

## 2. Phạm vi

| Trong phạm vi | Ngoài phạm vi |
|---|---|
| **Một trường, nhiều lớp** (D-01) | Nhiều trường |
| Đợt thu, phiếu thu, miễn giảm, tiền thừa, hoàn trả, thu khác | Tích hợp ngân hàng / QR / cổng thanh toán (D-11) |
| Phiếu chi có duyệt, danh mục chi, chứng từ, giữ chỗ số dư | Kế toán nhà trường, học phí chính thức |
| Báo cáo quỹ, chốt sổ năm học, chuyển lớp / lên lớp | Đa ngôn ngữ – chỉ tiếng Việt (D-06) |
| Thu hộ công nợ cũ (kể cả sau khóa sổ), bàn giao tiền thu hộ, bàn giao tiền thừa, chuyển quỹ, người giữ quỹ lớp cũ | Email / SMS / Zalo nhắc nộp tiền (D-12), ứng dụng di động riêng |
| Mời tài khoản qua email, thông báo trong hệ thống | |

## 3. Thuật ngữ

| Thuật ngữ | Ý nghĩa |
|---|---|
| Năm học | Đơn vị quản lý quỹ (D-02) |
| Quỹ lớp | Tiền chung của **một lớp trong một năm học** (D-25) |
| Đợt thu | Khoản cần thu từ học sinh, thuộc một lớp – năm học |
| Phiếu thu | Một lần nộp tiền cho học sinh, **liên kết với khoản công nợ** được thanh toán (D-39) |
| Công nợ | Số tiền học sinh còn phải nộp cho một đợt thu; thuộc lớp – năm học của đợt thu đó (D-31) |
| Công nợ cũ | Công nợ của lớp – năm học trước, đi theo học sinh sang lớp mới (D-24) |
| Thu hộ | Thủ quỹ lớp mới thu công nợ cũ; tiền ghi sổ vào quỹ lớp – năm học gốc (D-31) |
| Chứng từ thu hộ bổ sung | Phiếu thu hộ cho công nợ thuộc năm học **đã khóa sổ** (D-36) |
| Bàn giao tiền thu hộ | Chuyển **tiền thực tế** thu hộ về người giữ quỹ lớp gốc (D-37) |
| Người giữ quỹ | Người đang thực tế giữ tiền quỹ của một lớp – năm học: thủ quỹ trong năm; sau khi kết thúc năm học là người được Quản trị phân công (D-37) |
| Thu khác | Khoản thu không gắn với học sinh (D-13) |
| Phiếu chi | Khoản chi từ quỹ, có chứng từ, cần GVCN duyệt |
| Giữ chỗ | Phần số tiền bị tạm giữ bởi chứng từ đang chờ: số dư quỹ (phiếu chi, chuyển quỹ – D-40), số dư cá nhân (hoàn trả – D-47), khoản nợ (phiếu thu chờ xác nhận – D-48) |
| Biên bản chênh lệch | Biên bản lập khi số tiền thực nhận khi bàn giao khác số dự kiến; bàn giao ở trạng thái `Discrepancy` (D-44) |
| Số dư quỹ / Số dư khả dụng | Công thức: mục 0 của [quy tắc nghiệp vụ](../business-analysis/business-rules-and-processes.md) |
| Số dư cá nhân học sinh | Tiền nộp thừa; **không thuộc quỹ lớp** (D-09, D-32) |
| Phiếu hoàn trả | Trả lại tiền thừa cho phụ huynh, cần GVCN duyệt (D-21) |
| Bàn giao tiền thừa | Chuyển số dư cá nhân học sinh cho thủ quỹ lớp mới quản lý (D-38) |
| Chuyển quỹ | Chuyển số dư quỹ giữa các lớp; GVCN lập, Quản trị duyệt (D-26, D-29) |
| Chứng từ điều chỉnh | Chứng từ riêng cho chuyển quỹ sau khóa sổ (D-30) và cho xử lý chênh lệch (D-51) |
| Đảo giao dịch | Chứng từ liên kết chứng từ gốc đã hoàn tất, ghi ngược ảnh hưởng của chứng từ gốc; chứng từ gốc không bị sửa / xóa (D-57) |

## 4. Tác nhân

| Vai trò | Mô tả ngắn |
|---|---|
| Quản trị hệ thống | Năm học, lớp, gán và mời GVCN, chuyển lớp / lên lớp, danh mục chi chung, duyệt chuyển quỹ, **phân công người giữ quỹ lớp cũ**, xem và xuất báo cáo mọi lớp; xem danh tính học sinh trong báo cáo nội bộ (D-46, D-52); không có `Receipt.View` (D-41, D-52) |
| Giáo viên chủ nhiệm (GVCN) | Quản lý lớp, mời phụ huynh, phân công thủ quỹ, duyệt chi / hoàn trả / miễn giảm, chốt sổ, lập đề nghị chuyển quỹ |
| Thủ quỹ | Một phụ huynh của lớp được GVCN phân công; ghi thu / chi, thu hộ nợ cũ, bàn giao tiền thu hộ, bàn giao / nhận tiền thừa (D-05) |
| Người giữ quỹ lớp cũ | Thủ quỹ cũ còn được phân công hoặc người khác do Quản trị chỉ định, cho một lớp – năm học đã kết thúc; xác nhận nhận tiền thu hộ và xem báo cáo trong phạm vi được giao; không mặc nhiên có quyền Thủ quỹ (D-37, D-45) |
| Phụ huynh | Nộp tiền, báo chuyển khoản, xem khoản nộp của con và sổ quỹ lớp |

Chi tiết quyền: [roles-and-permissions.md](../business-analysis/roles-and-permissions.md).

## 5. Yêu cầu chức năng

### 5.1 Quản trị & tài khoản (FR-ADM)

| Mã | Yêu cầu | Vai trò |
|---|---|---|
| FR-ADM-01 | Tạo / sửa năm học và lớp; gán GVCN cho lớp (D-22) | Quản trị |
| FR-ADM-02 | Gửi lời mời qua email cho GVCN (D-03) | Quản trị |
| FR-ADM-03 | Gửi lời mời qua email cho phụ huynh, gắn với học sinh của lớp (D-03) | GVCN |
| FR-ADM-04 | Người được mời mở liên kết, đặt mật khẩu và kích hoạt tài khoản | Người được mời |
| FR-ADM-05 | Đăng nhập bằng email + mật khẩu; đổi mật khẩu; quên mật khẩu qua email | Tất cả |
| FR-ADM-06 | Quản lý danh mục chi dùng chung toàn trường (D-18) | Quản trị |
| FR-ADM-07 | Chuyển lớp / lên lớp cho học sinh; **Quản trị xác nhận** là hoàn tất chuyển lớp, không cần GVCN lớp mới xác nhận; công nợ cũ, số dư cá nhân và liên kết phụ huynh đi theo học sinh; bàn giao số dư cá nhân là nghiệp vụ riêng (D-23, D-24, D-35, D-56) | Quản trị |
| FR-ADM-08 | **Phân công / thay đổi người giữ quỹ** cho lớp – năm học đã kết thúc: chọn thủ quỹ cũ hoặc người khác; ghi rõ lớp và năm học; cấp quyền riêng (xác nhận bàn giao, xem báo cáo, thu nợ nếu được cấp); lưu lịch sử phân công (D-37, D-39, D-45) | Quản trị |

### 5.2 Lớp & thành viên (FR-CLS)

| Mã | Yêu cầu | Vai trò |
|---|---|---|
| FR-CLS-01 | Xem thông tin lớp: tên lớp, năm học, GVCN, thủ quỹ, người giữ quỹ | Tất cả (trong phạm vi) |
| FR-CLS-02 | Quản lý học sinh trong lớp: thêm, sửa; đánh dấu nghỉ học, có lưu lịch sử (D-14). Chuyển lớp / lên lớp do Quản trị (FR-ADM-07) | GVCN |
| FR-CLS-03 | Liên kết phụ huynh – học sinh: một phụ huynh nhiều con, một học sinh nhiều phụ huynh (D-04) | GVCN |
| FR-CLS-04 | Phân công / thay đổi thủ quỹ: chọn **một** phụ huynh của lớp; đổi người không mất lịch sử (D-05, D-39) | GVCN |

### 5.3 Thu (FR-REC)

| Mã | Yêu cầu | Vai trò |
|---|---|---|
| FR-REC-01 | Tạo đợt thu: tên, mô tả, số tiền mỗi học sinh, hạn nộp, học sinh áp dụng (D-07, D-34) | GVCN, Thủ quỹ |
| FR-REC-02 | Sửa / hủy đợt thu khi chưa có phiếu thu hợp lệ; đóng đợt thu | GVCN, Thủ quỹ |
| FR-REC-03 | Ghi phiếu thu: học sinh, khoản công nợ, số tiền, ngày nộp, hình thức (tiền mặt / chuyển khoản), ghi chú (một phiếu cho nhiều học sinh: Q-53) | Thủ quỹ |
| FR-REC-04 | Nộp nhiều lần, nộp một phần (D-08) | Thủ quỹ |
| FR-REC-05 | Phần nộp vượt công nợ được ghi vào **số dư cá nhân học sinh**, kể cả khi thu hộ nợ cũ; phiếu thu tách phần thanh toán nợ và phần tiền thừa (D-09, D-48, D-54) | Hệ thống |
| FR-REC-06 | Khi học sinh có đợt thu mới, hệ thống **tự trừ** số dư cá nhân **đang khả dụng** vào công nợ; số dư đang chờ bàn giao **không** được trừ (D-09, D-38) | Hệ thống |
| FR-REC-07 | Đề nghị miễn giảm (toàn phần / một phần, có lý do) và duyệt (D-10, D-28) | Đề nghị: Thủ quỹ, GVCN · Duyệt: GVCN |
| FR-REC-08 | Phụ huynh báo "đã chuyển khoản" kèm ảnh biên lai; khoản nợ tương ứng được **giữ chỗ**; thủ quỹ xác nhận (cập nhật nợ còn lại) hoặc từ chối (giải phóng giữ chỗ); phụ huynh được hủy báo chuyển khoản của mình khi còn chờ (D-11, D-48, D-57) | Phụ huynh, Thủ quỹ |
| FR-REC-09 | Phiếu thu đã hợp lệ ghi sai: **đảo giao dịch** có lý do, liên kết phiếu gốc; không sửa / xóa phiếu gốc (D-27, D-57; người tạo / duyệt: Q-49) | Thủ quỹ (Q-49) |
| FR-REC-10 | Theo dõi tình trạng nộp từng học sinh | GVCN, Thủ quỹ |
| FR-REC-11 | Gửi nhắc nộp tiền bằng thông báo trong hệ thống (D-12) | GVCN, Thủ quỹ |
| FR-REC-12 | Ghi **thu khác** có nguồn và chứng từ, không cần duyệt (D-13) | Thủ quỹ |
| FR-REC-13 | Lập phiếu hoàn trả từ số dư cá nhân **khả dụng**; **giữ chỗ** ngay khi tạo; GVCN duyệt → **chờ thực hiện hoàn trả**; thủ quỹ xác nhận đã trả tiền kèm **minh chứng** → **hoàn tất** (giảm số dư thực tế); bị từ chối / hủy → giải phóng giữ chỗ; hủy sau khi duyệt theo quy trình riêng (D-21, D-47, D-53, D-57; quy trình hủy riêng: Q-49) | Thủ quỹ, GVCN |
| FR-REC-14 | **Thu hộ công nợ cũ** của học sinh đang học lớp mình: phiếu liên kết khoản công nợ gốc; tiền ghi sổ vào quỹ lớp – năm học gốc; lưu người thu hộ, lớp thu hộ, lớp – năm học sở hữu; lớp thu hộ chỉ hiển thị "Đã thu hộ" (D-31, D-39, D-42) | Thủ quỹ lớp mới |
| FR-REC-15 | **Bàn giao tiền thừa:** thủ quỹ lớp cũ lập chứng từ (số dư trước / sau, người bàn giao, người nhận, số tiền, thời gian); thủ quỹ lớp mới xác nhận; số dư chỉ dùng được ở lớp mới sau khi xác nhận; không khớp → biên bản chênh lệch (FR-REC-20) (D-32, D-38, D-44) | Thủ quỹ lớp cũ, Thủ quỹ lớp mới |
| FR-REC-16 | **Thu hộ sau khóa sổ:** chứng từ thu hộ bổ sung cho công nợ thuộc năm học đã khóa; không mở khóa / sửa giao dịch cũ; lưu người thu, thời gian, người xác nhận (người giữ quỹ lớp gốc – D-43), thông tin bàn giao tiền (D-36, D-43) | Thủ quỹ lớp mới |
| FR-REC-17 | **Thu công nợ của học sinh đã chuyển đi:** thủ quỹ lớp cũ thu khoản công nợ thuộc lớp cũ nếu còn được phân công và có permission; phần thu vượt thành số dư cá nhân và được **bàn giao số dư cá nhân** cho thủ quỹ lớp mới (D-39, D-55) | Thủ quỹ lớp cũ |
| FR-REC-18 | **Chống thu trùng & giữ chỗ khoản nợ:** số nợ còn có thể thu = nợ chưa thanh toán − nợ đang giữ chỗ; phân bổ vào khoản nợ không vượt số này, kể cả khi hai thủ quỹ cùng thu; phần vượt vào số dư cá nhân; **không cho tạo phiếu thanh toán nợ khi số còn có thể thu = 0** – tiền đã nhận thì ghi theo nghiệp vụ số dư cá nhân (D-39, D-48, D-54) | Hệ thống |
| FR-REC-19 | **Bàn giao tiền thu hộ:** thủ quỹ đã thu hộ lập chứng từ bàn giao tiền thực tế về người giữ quỹ lớp gốc (người giao, người nhận, số tiền, thời gian); **người giữ quỹ lớp gốc xác nhận** và ghi số tiền thực nhận; người thu hộ không được tự xác nhận; nếu người thu **đồng thời là** người giữ quỹ lớp gốc thì không lập bàn giao, chỉ ghi nhận người giữ tiền trên phiếu thu (D-37, D-43, D-58) | Thủ quỹ lớp mới, Người giữ quỹ lớp gốc |
| FR-REC-20 | **Biên bản chênh lệch bàn giao** (tiền thu hộ và tiền thừa): người nhận ghi số dự kiến, số thực nhận, chênh lệch, lý do, người bàn giao / nhận, đính kèm minh chứng; bàn giao sang `Discrepancy`; lập phương án xử lý thuộc 4 loại: sửa sai trước ghi nhận, bàn giao bổ sung, ghi nhận thiếu hụt, ghi nhận tiền thừa; **GVCN lớp gốc duyệt** (Quản trị nếu lớp đã khóa sổ / không còn GVCN); người đề xuất không tự duyệt; bị từ chối thì số dư không đổi; chỉ ghi điều chỉnh sau khi được duyệt (D-44, D-50, D-51; người đề xuất: Q-47; ảnh hưởng số dư: Q-48; kết hợp nhiều phương án: Q-54) | Người nhận, GVCN lớp gốc, Quản trị |

### 5.4 Chi (FR-EXP)

| Mã | Yêu cầu | Vai trò |
|---|---|---|
| FR-EXP-01 | Lập phiếu chi: nội dung, số tiền, ngày chi, người nhận, danh mục, **chứng từ bắt buộc** (D-16) | Thủ quỹ |
| FR-EXP-02 | Duyệt / từ chối phiếu chi có lý do (D-15) | GVCN |
| FR-EXP-03 | Sửa phiếu khi chưa duyệt hoặc bị từ chối (giữ chỗ cập nhật theo số mới sau khi kiểm tra hợp lệ – D-49); người tạo được hủy phiếu khi còn chờ; phiếu đã duyệt chỉ xử lý bằng **đảo giao dịch** có lý do (D-28, D-57; người tạo / duyệt đảo: Q-49; bước xác nhận đã chi: Q-50) | Thủ quỹ (sửa, hủy khi chờ), GVCN (đảo – Q-49) |
| FR-EXP-04 | Khi **lập** phiếu chi: hệ thống kiểm tra số tiền không vượt **số dư khả dụng** và **giữ chỗ** số tiền đó; khi duyệt: giữ chỗ thành giao dịch chính thức; khi bị từ chối / hủy: giải phóng giữ chỗ (D-17, D-40) | Hệ thống |
| FR-EXP-05 | Quản lý danh mục chi riêng của lớp, bên cạnh danh mục chung (D-18) | Thủ quỹ |
| FR-EXP-06 | Xem phiếu chi và chứng từ | GVCN, Thủ quỹ, Phụ huynh (D-19), Quản trị (D-33) |

### 5.5 Báo cáo, chốt sổ & chuyển quỹ (FR-RPT)

| Mã | Yêu cầu | Vai trò |
|---|---|---|
| FR-RPT-01 | Tổng quan quỹ: tổng thu, tổng chi, số dư quỹ, **số tiền đang giữ chỗ**, **số dư khả dụng** (D-40) | Tất cả trong phạm vi (phụ huynh: số dư quỹ) |
| FR-RPT-02 | Sổ thu chi theo thời gian, có số dư sau mỗi giao dịch | GVCN, Thủ quỹ: chi tiết · Quản trị: chi tiết, có danh tính học sinh (D-46, D-52) · Phụ huynh: phần thu chỉ hiện tổng theo đợt (D-19) |
| FR-RPT-03 | Báo cáo theo đợt thu: đã nộp / chưa nộp / miễn giảm / đã thu hộ, đã thu / còn thiếu | GVCN, Thủ quỹ, Quản trị (có danh tính học sinh – D-52) |
| FR-RPT-04 | Báo cáo chi theo danh mục và theo thời gian | GVCN, Thủ quỹ, Quản trị |
| FR-RPT-05 | Xem khoản phải nộp, đã nộp, công nợ cũ và số dư cá nhân của con mình, kể cả lịch sử ở lớp cũ (D-35) | Phụ huynh |
| FR-RPT-06 | Báo cáo số dư cá nhân học sinh, **tách riêng** khỏi số dư quỹ lớp; hiện số dư thực tế, đang giữ chỗ, khả dụng, đang chờ bàn giao (D-32, D-38, D-47) | GVCN, Thủ quỹ, Quản trị (có danh tính học sinh – D-52) |
| FR-RPT-07 | Xuất Excel / PDF các báo cáo thuộc phạm vi quyền xem (D-41) | GVCN, Thủ quỹ, Quản trị |
| FR-RPT-08 | Chốt sổ cuối năm (D-20). Công nợ chưa thanh toán và số dư cá nhân đi theo học sinh (D-24); số dư quỹ giữ lại tại lớp – năm học phát sinh (D-25) | GVCN |
| FR-RPT-09 | Chuyển quỹ giữa các lớp: GVCN lớp chuyển lập đề nghị (có giữ chỗ), Quản trị duyệt, GVCN lớp nhận nhận thông báo; ghi ở cả hai lớp, liên kết một giao dịch (D-26, D-29, D-35, D-40) | GVCN, Quản trị |
| FR-RPT-10 | Chuyển quỹ sau khi khóa sổ bằng chứng từ điều chỉnh riêng, có lý do; không sửa giao dịch đã khóa (D-30) | GVCN, Quản trị |
| FR-RPT-11 | Báo cáo **phân biệt** giao dịch phát sinh trước và sau khi khóa sổ (chuyển quỹ điều chỉnh, thu hộ bổ sung) (D-30, D-36) | GVCN, Thủ quỹ, Quản trị |
| FR-RPT-12 | Quản trị xem và xuất báo cáo quỹ của tất cả lớp, chỉ xem (D-33, D-41) | Quản trị |
| FR-RPT-13 | Báo cáo **tiền ghi sổ và người đang giữ tiền**: với mỗi lớp – năm học, số dư quỹ trên sổ, số tiền đang do người khác giữ (thu hộ chưa bàn giao), người đang giữ (D-37) | GVCN, Thủ quỹ, Quản trị, Người giữ quỹ (trong phạm vi được giao – D-45) |
| FR-RPT-14 | Người giữ quỹ lớp cũ xem báo cáo của lớp – năm học được giao; xuất báo cáo khi được cấp `Report.Export` (D-45, D-52) | Người giữ quỹ |
| FR-RPT-15 | Báo cáo bàn giao: chứng từ bàn giao theo trạng thái (chờ xác nhận, hoàn tất, `Discrepancy`) (D-38, D-43, D-44) | GVCN, Thủ quỹ, Người giữ quỹ, Quản trị |

### 5.6 Hệ thống (FR-SYS)

| Mã | Yêu cầu | Vai trò |
|---|---|---|
| FR-SYS-01 | Ghi lịch sử thao tác (ai, lúc nào, làm gì, lý do nếu có) cho mọi thao tác ảnh hưởng đến tiền: thu, chi, hoàn trả, miễn giảm, thu hộ, bàn giao, biên bản chênh lệch, chuyển quỹ, điều chỉnh, giữ chỗ / giải phóng, phân công (D-27, D-30, D-36, D-39, D-44) | Hệ thống |
| FR-SYS-02 | Xem lịch sử thao tác | Quản trị (có danh tính học sinh – D-52), GVCN (lớp mình) |
| FR-SYS-03 | Thông báo trong hệ thống: nhắc nộp, phiếu chờ duyệt, kết quả xác nhận chuyển khoản, chuyển quỹ đến lớp nhận, chứng từ bàn giao chờ xác nhận, bàn giao `Discrepancy` cần xử lý (D-12, D-29, D-37, D-38, D-44) | Hệ thống |
| FR-SYS-04 | Ghi **lịch sử truy cập** khi người dùng xem thông tin chi tiết có tên học sinh trong báo cáo (D-46) | Hệ thống |
| FR-SYS-05 | **Hủy & đảo chứng từ:** người tạo hủy chứng từ của mình khi còn chờ; người duyệt từ chối; chứng từ đã duyệt chưa hoàn tất hủy theo quy trình riêng; chứng từ đã hoàn tất chỉ xử lý bằng chứng từ điều chỉnh / đảo giao dịch liên kết chứng từ gốc (D-57; người thực hiện: Q-49) | Theo từng loại chứng từ |

## 6. User story chính

- **US-01** – Là **quản trị**, tôi muốn mời GVCN qua email để giáo viên tự kích hoạt tài khoản.
- **US-02** – Là **GVCN**, tôi muốn mời phụ huynh và phân công một phụ huynh làm thủ quỹ.
- **US-03** – Là **thủ quỹ**, tôi muốn tạo đợt thu và ghi tiền nộp để biết ngay ai còn thiếu.
- **US-04** – Là **thủ quỹ**, tôi muốn tiền nộp thừa tự trừ vào đợt sau để không phải theo dõi bằng tay.
- **US-05** – Là **thủ quỹ**, tôi muốn lập phiếu chi kèm chứng từ để GVCN duyệt.
- **US-06** – Là **GVCN**, tôi muốn duyệt chi, hoàn trả và miễn giảm để kiểm soát mọi khoản tiền ra.
- **US-07** – Là **phụ huynh**, tôi muốn báo đã chuyển khoản kèm biên lai để thủ quỹ xác nhận.
- **US-08** – Là **phụ huynh**, tôi muốn xem sổ thu chi và chứng từ để yên tâm về sự minh bạch.
- **US-09** – Là **GVCN**, tôi muốn chốt sổ cuối năm để khóa số liệu của năm học.
- **US-10** – Là **quản trị**, tôi muốn cho học sinh lên lớp mà công nợ và tiền thừa của các em đi theo, không bị mất.
- **US-11** – Là **thủ quỹ lớp mới**, tôi muốn thu hộ nợ cũ của học sinh chuyển đến mà tiền vẫn ghi đúng cho lớp cũ.
- **US-12** – Là **GVCN**, tôi muốn đề nghị chuyển số dư quỹ sang lớp khác và Quản trị duyệt để việc chuyển tiền minh bạch.
- **US-13** – Là **thủ quỹ lớp mới**, tôi muốn xác nhận đã nhận tiền thừa của học sinh chuyển đến để số dư được dùng cho đợt thu của lớp tôi.
- **US-14** – Là **quản trị**, tôi muốn phân công người giữ quỹ cho lớp đã kết thúc năm học để tiền thu hộ có người nhận.
- **US-15** – Là **thủ quỹ**, tôi muốn hệ thống giữ chỗ số dư khi tôi lập phiếu chi để không ai lập phiếu khác vượt quỹ.
- **US-16** – Là **người giữ quỹ lớp cũ**, tôi muốn xác nhận số tiền thực nhận khi được bàn giao, và lập biên bản nếu không khớp, để sổ quỹ chỉ được điều chỉnh khi đã có người duyệt.
- **US-17** – Là **thủ quỹ**, tôi muốn khoản nợ được giữ chỗ khi phụ huynh báo chuyển khoản để không bị thu trùng.

## 7. Yêu cầu phi chức năng (sơ bộ)

| Mã | Yêu cầu |
|---|---|
| NFR-01 | Tech Stack và kiến trúc theo `training-doc.md` |
| NFR-02 | Phân quyền theo **permission** kết hợp **phạm vi** (toàn trường cho Quản trị, theo lớp / phân công cho vai trò khác) (theo `training-doc.md`) |
| NFR-03 | Phụ huynh không xem được tên hay công nợ của học sinh khác (D-19); danh tính trong biên nhận chỉ xem được khi có `Receipt.View` (D-41); tên học sinh trong báo cáo nội bộ chỉ xem được khi có permission và được ghi lịch sử truy cập (D-46) |
| NFR-04 | Dữ liệu tài chính không bị xóa hẳn; giao dịch đã khóa sổ không bị sửa / ghi đè; mọi thay đổi đều có lịch sử (D-27, D-30, D-36) |
| NFR-05 | Chỉ tiếng Việt; tiền tệ VND; ngày dd/MM/yyyy (D-06) |
| NFR-06 | Giao diện responsive cho điện thoại |
| NFR-07 | Liên kết mời có thời hạn và chỉ dùng được một lần (D-28; thời hạn cụ thể: chốt khi thiết kế kỹ thuật) |
| NFR-08 | **Xử lý đồng thời:** nhiều yêu cầu chi / chuyển quỹ / hoàn trả / thu / thu hộ tạo cùng lúc không được làm vượt số dư quỹ khả dụng, số dư cá nhân khả dụng hoặc số nợ còn có thể thu (D-39, D-40, D-47, D-48; cơ chế kỹ thuật: chốt khi thiết kế) |
