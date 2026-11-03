# UI/UX – Câu hỏi cần xác nhận

> Phiên bản: 0.1 · Ngày: 2026-11-03
> UQ-xx = câu hỏi giao diện. "Giả định tạm" là cách bản thiết kế hiện đang thể hiện, **chưa phải quyết định**. Câu hỏi nghiệp vụ Q-47 → Q-56 vẫn mở trong [open-questions.md](../requirements/open-questions.md); ảnh hưởng của chúng lên UI ở mục 2.

## 1. Câu hỏi UI

| Mã | Câu hỏi | Vì sao cần hỏi | Giả định tạm |
|---|---|---|---|
| UQ-01 | ADM-05 có cho **chọn nhiều học sinh** để lên lớp cùng lúc không? | BA mô tả chuyển lớp cho học sinh, chưa nói thao tác hàng loạt. Đây là tiện ích UI, không đổi nghiệp vụ: mỗi học sinh vẫn là một lần chuyển được Quản trị xác nhận | Cho chọn nhiều; bước kiểm tra liệt kê từng học sinh; một lần xác nhận áp dụng cho các học sinh đã chọn |
| UQ-02 | Phụ huynh nhiều con: **chuyển qua lại từng con** hay có trang **gộp** tất cả các con? | D-04 cho phép nhiều con | Chuyển từng con bằng bộ chọn; trang chủ có dòng nhắc "Con khác còn N khoản cần nộp" |
| UQ-03 | Thủ quỹ (cũng là phụ huynh) đăng nhập thì vào **trang thủ quỹ** hay **trang phụ huynh** trước? | Một tài khoản hai vai trò | Trang thủ quỹ; chuyển bằng bộ chọn phạm vi |
| UQ-04 | Các **chỉ số trên dashboard** (ADM-01, TCH-01, TRS-01) đã đủ / đúng ưu tiên chưa? | BA không quy định dashboard | Như screen-specifications |
| UQ-05 | **Nội dung nhắc nộp** cố định theo mẫu hay cho người gửi sửa? Gửi theo từng học sinh hay cả nhóm? | D-12 chỉ quy định kênh | Mẫu có sẵn, cho sửa trước khi gửi; gửi cho cả nhóm đã chọn |
| UQ-06 | Màn hình **Báo chuyển khoản** của phụ huynh có cần hiển thị **thông tin tài khoản nhận tiền** (ngân hàng, số tài khoản, tên chủ tài khoản, nội dung chuyển khoản gợi ý) không? | Phụ huynh cần biết chuyển tiền vào đâu, nhưng BA và database **chưa có** dữ liệu này. Thêm vào là bổ sung nghiệp vụ (ai nhập, ai sửa, lưu ở lớp nào) nên không tự thêm | Chưa hiển thị; phụ huynh chuyển theo hướng dẫn ngoài hệ thống |
| UQ-07 | Mẫu **xuất PDF / Excel**: dùng xuất có sẵn của Kendo Grid hay cần mẫu in riêng (logo, chữ ký, tiêu đề trường)? | BA chỉ nói "xuất Excel / PDF" | Xuất có sẵn của Kendo Grid, thêm tiêu đề báo cáo, phạm vi, thời điểm xuất |
| UQ-08 | **Ghi lượt truy cập danh tính** (D-46) ở mức nào: mỗi lần mở báo cáo, hay mỗi học sinh được hiển thị? | Ảnh hưởng thông báo trên UI và lượng log | Mỗi lần mở / đổi bộ lọc báo cáo có tên học sinh; dải thông báo "Lượt xem này được ghi lại" |
| UQ-09 | Có **nhận diện thương hiệu** của trường (logo, màu chính) không? | Theme Default cho phép đổi biến màu | Theme Default, màu mặc định; chỗ đặt logo ở AppBar |
| UQ-10 | **Mức hỗ trợ mobile** theo vai trò | Ảnh hưởng phạm vi thiết kế và kiểm thử | Phụ huynh, thủ quỹ: đầy đủ trên mobile; GVCN: duyệt và xem trên mobile, chốt sổ trên desktop; Admin: desktop |
| UQ-11 | Khi ghi thu cho học sinh có **nhiều khoản nợ**, thứ tự phân bổ mặc định là gì? | BA yêu cầu phiếu thu liên kết khoản nợ nhưng chưa quy định thứ tự; đây có thể là **quy tắc nghiệp vụ** | Gợi ý theo hạn nộp cũ nhất trước (nợ cũ trước), người thu sửa được |
| UQ-12 | Có cần **tìm kiếm toàn cục** (tìm học sinh / chứng từ từ AppBar) không? | Tiện ích | Chưa có; tìm trong từng màn hình |
| UQ-13 | Người dùng có cần **chọn ngày nộp / ngày chi trong quá khứ** không? Giới hạn bao xa? | Ảnh hưởng nhập liệu muộn và khóa sổ | Cho chọn ngày quá khứ trong năm học của quỹ; không cho ngày tương lai |

## 2. Câu hỏi nghiệp vụ đang mở ảnh hưởng UI

| Câu hỏi | Màn hình bị ảnh hưởng | UI đang thể hiện |
|---|---|---|
| Q-47 Ai đề xuất phương án chênh lệch | FND-07, DLG-04 | Nút "Đề xuất phương án" hiện cho người có `HandoverDiscrepancy.Propose` |
| Q-48 Ảnh hưởng số dư của 4 phương án | DLG-04, FND-07 | Hiển thị ảnh hưởng theo giả định của Database Design |
| Q-49 Ai tạo / duyệt đảo giao dịch, hủy sau duyệt | FND-06, DLG-06, COM-06, TRS-06 | Nút "Đảo giao dịch" và "Hủy sau duyệt" hiện theo permission `Reversal.*`, `Refund.CancelApproved` (chưa gán vai trò) |
| Q-50 Phiếu chi có bước "đã chi" | TRS-05, FND-06 | Không có bước; `Approved` là hoàn tất |
| Q-51 Miễn giảm khi đã nộp | DLG-02 | Số miễn giảm tối đa = nợ chưa thanh toán |
| Q-52 Số dư âm do đảo | DLG-06 | Cảnh báo và chặn |
| Q-53 Phiếu thu nhiều học sinh | TRS-02, PAR-03 | Một học sinh / phiếu |
| Q-54 Kết hợp nhiều phương án | FND-07 | Thanh tiến độ "đã xử lý X / chênh lệch" |
| Q-55 Ai duyệt miễn giảm do GVCN đề nghị | DLG-02, COM-06 | Nút duyệt bị vô hiệu với người đề nghị |
| Q-56 Hoàn trả đang chờ khi chuyển lớp | ADM-05, W-09 | Hiển thị cảnh báo, phiếu xử lý tiếp ở lớp cũ |
