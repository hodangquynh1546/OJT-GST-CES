# Quy tắc & Quy trình nghiệp vụ

> Phiên bản: 2.0 · 2026-11-05 (mô hình Giáo viên · Học sinh · Phụ huynh, D-24 → D-33; BR-28 → BR-37, P-11 → P-16) · 1.2 · 2026-11-04 (áp dụng D-10, D-11, D-12, D-16 → D-23) · v1.1: thêm 3 vai trò, công khai, thành viên · Quyết định: [open-questions.md](../requirements/open-questions.md) · Quyền: [roles-and-permissions.md](roles-and-permissions.md).
> **Trạng thái triển khai (2026-11-05): Not Implemented** – mã nguồn Backend / Frontend cũ đã xóa ngày 2026-11-05; đặc tả dưới đây giữ nguyên và là Source of Truth. Xem [implementation-status.md](../implementation/implementation-status.md).

## 1. Khái niệm

| Khái niệm | Ý nghĩa |
|---|---|
| **Quỹ lớp** | Tiền chung của lớp; học sinh được phân công thủ quỹ ghi chép, GVCN (người lập quỹ) giám sát, học sinh và phụ huynh theo dõi *(sửa 2026-11-05)* |
| **Khoản thu** | Tiền vào quỹ (đóng quỹ, ủng hộ…) |
| **Khoản chi** | Tiền ra khỏi quỹ (mua đồ, liên hoan…) |
| **Danh mục** | Nhóm khoản thu hoặc nhóm khoản chi, để thống kê |
| **Thành viên** | *(v1.2: tài khoản thuộc quỹ với một vai trò – thay 2026-11-05)* GVCN, học sinh (có thể là thủ quỹ) của quỹ; phụ huynh liên kết qua học sinh |
| **Học sinh** | Dòng trong danh sách lớp của một quỹ; có tài khoản khi GVCN cấp (tên đăng nhập + mật khẩu tạm) |
| **Phân công thủ quỹ** | Bản ghi GVCN giao một học sinh làm thủ quỹ; thu hồi = đánh dấu, giữ lịch sử |
| **Liên kết phụ huynh** | Bản ghi nối tài khoản phụ huynh (theo email) với một học sinh; gỡ = đánh dấu |
| **Khoản công khai** | Khoản thu / chi đang hiệu lực mà thủ quỹ để cờ "Công khai cho phụ huynh" |

## 2. Quy tắc

| Mã | Quy tắc | Nguồn |
|---|---|---|
| BR-01 | Số tiền là số nguyên VND **lớn hơn 0**; khoản thu tăng số dư, khoản chi giảm số dư | Task 2026-11-04 mục 5 |
| BR-02 | **Số dư quỹ = tổng khoản thu − tổng khoản chi chưa hủy**. Không lưu số dư riêng → sửa / hủy không thể làm số dư lệch với lịch sử | Mục 5 |
| BR-03 | Thêm khoản thu / chi: **lưu là có hiệu lực ngay**; không có yêu cầu, phiếu, duyệt, xác nhận | D-05 |
| BR-04 | Khoản chi vượt số dư vẫn được lưu; kết quả trả kèm cảnh báo số dư âm | D-03 |
| BR-05 | Sửa được số tiền, ngày, danh mục, nội dung, ghi chú, cờ công khai; **không đổi** thu ↔ chi (muốn đổi: hủy rồi nhập lại); không sửa khoản đã hủy | – |
| BR-06 | **Hủy** thay cho xóa, **bắt buộc lý do** (≤ 500 ký tự): khoản đã hủy giữ nguyên trong lịch sử (lọc "Đã hủy"), không tính vào số dư / báo cáo; không hủy lại, không sửa | Mục 4.2, 4.3; D-14 |
| BR-07 | Ngày giao dịch không ở tương lai (giờ Việt Nam) | Q-02 (tạm) |
| BR-08 | Danh mục thuộc đúng một loại (thu hoặc chi) và đúng một quỹ; tên không trùng trong cùng loại (không phân biệt hoa thường); **ẩn** thay cho xóa – danh mục đã ẩn không chọn được cho khoản mới nhưng khoản cũ vẫn giữ | Mục 4.6 |
| BR-09 | Tháng chỉ là kỳ thống kê; số dư cộng dồn liên tục, không reset | D-04 |
| BR-10 | **Số dư đầu tháng** = số dư đến hết ngày cuối tháng trước; **số dư cuối tháng** = đầu tháng + thu − chi trong tháng → cuối tháng trước luôn bằng đầu tháng sau, kể cả sau khi sửa / hủy giao dịch cũ | Mục 4.5 |
| BR-11 | Mọi thêm / sửa / hủy giao dịch và mọi thao tác tài khoản thành viên đều ghi nhật ký: ai, lúc nào, giá trị trước / sau. Thủ quỹ và GVCN xem được lịch sử thay đổi của từng giao dịch | Mục 5 |
| BR-12 | Nhấn "Lưu" nhiều lần không tạo giao dịch trùng (mỗi lần mở form có một khóa chống lưu trùng) | Mục 5 |
| BR-13 | ~~Mỗi tài khoản thuộc đúng một quỹ với một vai trò~~ *(Thay 2026-11-05 – nguồn cũ dẫn sai là D-11; xem open-questions mục 1c)*: học sinh thuộc đúng một quỹ; giáo viên, phụ huynh liên quan nhiều quỹ. Vai trò trong từng quỹ suy ra từ dữ liệu; chỉ xem / thao tác trên quỹ mình có vai trò ([roles-and-permissions.md](roles-and-permissions.md)) | D-26, D-31 |
| BR-14 | Khoản mới mặc định **công khai**. Phụ huynh chỉ thấy khoản công khai (nội dung, ngày, danh mục, số tiền). *Sửa 2026-11-04 (B2):* khoản công khai đã hủy vẫn hiện cho phụ huynh với nhãn "Đã hủy" + ngày hủy, không tính vào số dư, không có lý do / người hủy | D-09, D-18 |
| BR-15 | Số dư, tổng thu, tổng chi, số liệu tháng **giống nhau ở mọi vai trò** (tính trên mọi khoản đang hiệu lực). Với phụ huynh, phần không công khai được gộp thành dòng "Khoản khác (không công khai chi tiết)" theo thu / chi, **không** kèm số lượng, ngày, danh mục → tổng các dòng luôn khớp số tổng. *Cách thể hiện số tiền gộp chờ chốt B1* (open-questions mục 4) | D-09, B1 |
| BR-16 | *(Thay 2026-11-05 bằng BR-29, BR-30)* Thủ quỹ tạo thành viên bằng họ tên, email (duy nhất toàn hệ thống), vai trò. Hệ thống sinh mật khẩu tạm, chỉ hiển thị một lần | D-10 |
| BR-17 | Người dùng có mật khẩu tạm (mới tạo hoặc vừa được đặt lại) phải đổi mật khẩu trước khi dùng chức năng khác | D-10 |
| BR-18 | Khóa thay cho xóa tài khoản. Khóa, đặt lại mật khẩu hay đổi mật khẩu đều chấm dứt mọi phiên của tài khoản đó ngay | – |
| BR-19 | *(Thay 2026-11-05 bằng BR-32)* Quỹ luôn có ít nhất 1 thủ quỹ đang hoạt động (vai trò Thủ quỹ + tài khoản Active). *Sửa 2026-11-04:* áp dụng cho cả khóa **và hạ vai trò**; kiểm tra trong cùng giao dịch DB với thao tác (hai thủ quỹ thao tác cùng lúc: người sau bị từ chối `member.last_treasurer`). Không ai tự khóa / tự đặt lại mật khẩu / **tự đổi vai trò** của mình qua mục Thành viên | D-11, D-16 |
| BR-20 | GVCN và phụ huynh không thêm / sửa / hủy giao dịch, không quản lý danh mục. *(Sửa 2026-11-05: GVCN **quản lý** tên quỹ, học sinh, phụ huynh, phân công thủ quỹ; học sinh chưa phân công giống phụ huynh)* | D-13, D-26 |
| BR-21 | *(Thay 2026-11-05 bằng BR-31, BR-32 – không còn đổi vai trò)* Thủ quỹ đổi được vai trò của thành viên khác (Thủ quỹ ↔ GVCN ↔ Phụ huynh). Nhật ký ghi vai trò cũ, vai trò mới, người thực hiện, thời điểm. Quyền mới có hiệu lực ngay ở request kế tiếp (backend đọc vai trò từ DB mỗi request) | D-16 |
| BR-22 | Mỗi thao tác ghi lưu đúng người thực hiện: `CreatedBy` khi thêm, `UpdatedBy` khi sửa, `CancelledBy` khi hủy; `AssignedBy` / `RevokedBy` khi phân công / thu hồi thủ quỹ; `CreatedBy` / `RemovedBy` cho liên kết phụ huynh; kèm nhật ký trước / sau *(sửa 2026-11-05: bỏ `FundMember.UpdatedBy`)* | D-11, D-19, D-29 |
| BR-23 | Thông tin quỹ hiển thị họ tên + vai trò của mọi thủ quỹ và GVCN; không hiển thị email, số điện thoại, liên hệ | D-17 |
| BR-24 | Tổng quan tách **cộng dồn từ khi lập quỹ** (số dư, tổng thu, tổng chi) và **tháng hiện tại** (thu, chi, chênh lệch) | D-20 |
| BR-25 | Bộ lọc danh mục của phụ huynh chỉ gồm danh mục đang hiện và có ít nhất một khoản công khai; dòng "Khoản khác" không bị bộ lọc làm biến mất | D-22 |
| BR-26 | Đăng nhập: sai email / tên đăng nhập / mật khẩu → thông báo chung; mật khẩu đúng nhưng tài khoản bị khóa → "Tài khoản đang bị khóa, liên hệ giáo viên chủ nhiệm" *(sửa 2026-11-05)* | D-23 |
| BR-27 | Mật khẩu tạm sinh ngẫu nhiên an toàn, chỉ trả về một lần trong response, không ghi vào log / nhật ký | D-10 |
| BR-28 | Giáo viên tự đăng ký bằng họ tên, email (duy nhất, không phân biệt hoa thường), mật khẩu (8–128 ký tự, có chữ và số) và xác nhận. Tài khoản luôn là giáo viên; mọi trường "vai trò" trong request bị bỏ qua | D-24 |
| BR-29 | GVCN cấp tài khoản học sinh: tên đăng nhập duy nhất (3–60 ký tự `a-z 0-9 . _ -`; bỏ trống → gợi ý `ten.ho.123`) + mật khẩu tạm 16 ký tự ngẫu nhiên (RandomNumberGenerator), chỉ hiển thị một lần, không lưu dạng rõ, không ghi log / nhật ký. Mỗi học sinh tối đa một tài khoản | D-27, BR-27 |
| BR-30 | Liên kết phụ huynh theo email: một học sinh tối đa một liên kết đang hiệu lực (v1). Email thuộc tài khoản phụ huynh → chỉ liên kết; chưa có → tạo tài khoản phụ huynh + mật khẩu tạm hiển thị một lần; email thuộc giáo viên / học sinh → từ chối. Hệ thống không gửi email | D-28, D-33 |
| BR-31 | Chỉ phân công thủ quỹ cho học sinh **của quỹ**, có tài khoản **đã kích hoạt** (đã đổi mật khẩu tạm) và không bị khóa; học sinh đang là thủ quỹ không phân công lại | D-27, D-29 |
| BR-32 | Không thu hồi được thủ quỹ cuối cùng của quỹ (`treasurer.last`). Đổi người: phân công người mới trước, thu hồi người cũ sau. Kiểm tra trong cùng transaction, khóa dòng quỹ | D-11, D-29 |
| BR-33 | Thu hồi / gỡ liên kết là đánh dấu (`RevokedAt`, `RemovedAt` + người thực hiện); không xóa – lịch sử luôn xem được | D-29, NFR-12 |
| BR-34 | Học sinh chưa phân công thấy đúng phạm vi của phụ huynh (BR-14, BR-15, BR-25) | D-26, D-27 |
| BR-35 | GVCN cấp lại mật khẩu tạm cho học sinh: mật khẩu cũ và mọi phiên hết hiệu lực; học sinh phải đổi mật khẩu trước khi dùng tiếp (kể cả khi đang là thủ quỹ) | D-27 |
| BR-36 | Quỹ chưa có phân công thủ quỹ hiển thị "Chưa phân công thủ quỹ"; không ai ghi thu chi được cho tới khi có thủ quỹ | D-25, D-30 |
| BR-37 | Tạo quỹ, sửa tên quỹ, thêm / sửa học sinh, cấp / cấp lại tài khoản, liên kết / gỡ phụ huynh, phân công / thu hồi đều ghi AuditLog (không chứa mật khẩu) | BR-11, NFR-10 |

## 3. Trạng thái

Khoản thu / chi chỉ có **một** thông tin trạng thái: **Đang có hiệu lực** hoặc **Đã hủy**. Không có state machine.

## 4. Quy trình

### P-01 Thêm khoản thu / chi
1. Chọn "Thêm khoản thu" (hoặc "Thêm khoản chi").
2. Nhập nội dung, số tiền, ngày (mặc định hôm nay), danh mục; ghi chú nếu cần.
3. Nhấn "Lưu" → giao dịch xuất hiện trong lịch sử, số dư cập nhật ngay (cảnh báo nếu âm).

### P-02 Sửa
Mở giao dịch trong lịch sử → sửa → "Lưu" → số dư và báo cáo các tháng liên quan tự tính lại.

### P-03 Hủy khoản nhập nhầm
Mở giao dịch → "Hủy giao dịch" → nhập lý do (bắt buộc) → xác nhận → giao dịch chuyển sang "Đã hủy", số dư cập nhật.

### P-04 Xem báo cáo tháng
Chọn tháng → xem số dư đầu tháng, tổng thu, tổng chi, chênh lệch, số dư cuối tháng, tổng theo danh mục và danh sách giao dịch.

### P-05 Danh mục
Thêm / đổi tên / ẩn danh mục thu hoặc chi.

### P-06 Tạo quỹ (người vận hành) *(Thay 2026-11-05 bằng P-11, P-12; lệnh `create-treasurer` → `create-teacher`)*
Chạy lệnh `create-treasurer` → tạo tài khoản thủ quỹ đầu tiên, quỹ lớp, bản ghi thành viên (Thủ quỹ) và danh mục mặc định. Lệnh `reset-password`, `disable-account`, `enable-account` vẫn dùng được khi không còn thủ quỹ nào đăng nhập được.

### P-07 Thêm thành viên (thủ quỹ) *(Thay 2026-11-05 bằng P-13 → P-15)*
Thành viên → "+ Thêm thành viên" → họ tên, email, vai trò → "Tạo tài khoản" → sao chép mật khẩu tạm (chỉ hiển thị một lần) → gửi cho người dùng qua kênh ngoài hệ thống.

### P-08 Khóa / mở / đặt lại mật khẩu / đổi vai trò (thủ quỹ) *(Thay 2026-11-05 bằng P-15, P-16; khóa / mở chỉ bằng lệnh server – PD-03)*
Thành viên → chọn dòng (không phải chính mình) → Khóa (người đó bị đăng xuất ngay) / Mở khóa / Đặt lại MK (sinh mật khẩu tạm mới, bắt buộc đổi ở lần đăng nhập sau) / **Đổi vai trò** (chọn vai trò mới → Lưu; bị từ chối nếu làm quỹ mất thủ quỹ hoạt động cuối cùng).

### P-09 Đăng nhập lần đầu
*(2026-11-05: đăng nhập dùng một ô "Email hoặc tên đăng nhập", không chọn vai trò – D-34.)*
Đăng nhập bằng mật khẩu tạm → màn hình "Đặt mật khẩu mới" → lưu → vào Tổng quan theo vai trò.

### P-10 Xem quỹ (GVCN, học sinh, phụ huynh)
Tổng quan → Thu chi (lọc, tìm, mở chi tiết) → Báo cáo (tháng / 12 tháng). Không có nút thêm, sửa, hủy.

### P-11 Giáo viên đăng ký *(2026-11-05)*
Màn hình đăng ký → họ tên, email, mật khẩu, xác nhận → "Đăng ký" → vào "Quỹ của tôi" (trống) → "Tạo quỹ".

### P-12 Tạo quỹ (giáo viên)
"Quỹ của tôi" → "+ Tạo quỹ" → tên quỹ → quỹ mới có danh mục mặc định, nhãn "Chưa phân công thủ quỹ".

### P-13 Thêm học sinh và liên kết phụ huynh (GVCN)
Quỹ → Học sinh → "+ Thêm học sinh" → họ tên, email phụ huynh (tùy chọn) → "Lưu". Nếu tạo tài khoản phụ huynh mới: hộp thoại hiện email + mật khẩu tạm **một lần**, ghi "Hệ thống không gửi email – hãy tự chuyển"; đã có tài khoản: "Đã liên kết với tài khoản phụ huynh hiện có".

### P-14 Cấp tài khoản học sinh (GVCN)
Chi tiết học sinh → "Cấp tài khoản" → tên đăng nhập (gợi ý sẵn) → hộp thoại hiện tên đăng nhập + mật khẩu tạm **một lần** → GVCN tự chuyển cho học sinh → học sinh đăng nhập, đặt mật khẩu mới (P-09) → trạng thái "Đang hoạt động".

### P-15 Phân công / đổi thủ quỹ (GVCN)
Quỹ → Thủ quỹ → "Phân công" → chọn học sinh đã kích hoạt → xác nhận. Đổi người: phân công người mới **trước**, rồi "Thu hồi" người cũ (nút thu hồi bị khóa khi chỉ còn một thủ quỹ). Lịch sử phân công hiện bên dưới.

### P-16 Cấp lại mật khẩu học sinh (GVCN)
Chi tiết học sinh → "Cấp lại mật khẩu" → xác nhận → mật khẩu tạm mới hiển thị một lần; học sinh bị đăng xuất mọi nơi.

## 5. Lịch sử thay đổi

| Ngày | Phiên bản | Thay đổi | Lý do |
|---|---|---|---|
| 2026-11-04 | 1.1 | Thêm BR-14 → BR-20, P-07 → P-10; sửa BR-05, BR-06, BR-11, BR-13 | 3 vai trò |
| 2026-11-04 | 1.2 | Sửa BR-14 (B2), BR-15 (bỏ số lượng, chờ B1), BR-19 (thêm hạ vai trò, đồng thời); thêm BR-21 → BR-27; P-08 thêm đổi vai trò. Nội dung cũ ghi rõ "Sửa 2026-11-04" | PO duyệt D-10, D-11, D-16 → D-23 |
| 2026-11-05 | 2.0 | Thêm BR-28 → BR-37, P-11 → P-16; sửa BR-13 (sai nguồn D-11), BR-20, BR-22, BR-26; đánh dấu BR-16, BR-19, BR-21, P-06 → P-08 bị thay. Không xóa dòng cũ | PO duyệt D-24 → D-33 |
