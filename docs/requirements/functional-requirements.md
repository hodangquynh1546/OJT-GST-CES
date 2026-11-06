# Yêu cầu chức năng – Quản lý quỹ lớp

> Phiên bản: 2.0 · 2026-11-05 – mô hình **Giáo viên · Học sinh · Phụ huynh** (D-24 → D-33). v1.2 (3 vai trò Thủ quỹ / GVCN / Phụ huynh trong một quỹ), v1.0 và v0.7 nằm trong Git history.
>
> Cột **Trạng thái** = trạng thái triển khai: `Implemented` · `Partially Implemented` · `Not Implemented` · `Pending Decision`. **2026-11-06: FR-20, FR-13, FR-21 (Auth), FR-22, FR-23, FR-11 (Fund Lifecycle), FR-24 → FR-26 (Học sinh & phụ huynh), FR-02 → FR-07 (Thu chi), FR-10 (Danh mục) đã có ở Backend, chưa có UI → `Partially Implemented (Backend)`; các yêu cầu khác xem cột Trạng thái** (chi tiết: [implementation-status.md](../implementation/implementation-status.md)). *2026-11-05: mọi yêu cầu Not Implemented* – mã nguồn Backend / Frontend cũ đã xóa ngày 2026-11-05; đặc tả giữ nguyên. Giao diện: prototype Canvas.

## 1. Mục tiêu

Giáo viên chủ nhiệm (GVCN) lập và giám sát **các quỹ lớp** của mình. Trong mỗi quỹ:
- **Học sinh được GVCN phân công làm thủ quỹ** ghi chép thu chi.
- **GVCN** quản lý học sinh, phụ huynh, phân công thủ quỹ và xem toàn bộ – nhưng không tự ghi thu chi.
- **Học sinh** khác và **phụ huynh** theo dõi số dư và các khoản được công khai.

Ghi một khoản phải nhanh: "Thêm khoản thu / chi" → nhập vài thông tin → "Lưu" → số dư đổi ngay.

## 2. Phạm vi

| Trong phạm vi | Ngoài phạm vi |
|---|---|
| Giáo viên tự đăng ký; một giáo viên nhiều quỹ; tạo quỹ, đổi tên quỹ | Quản lý trường, năm học, phê duyệt |
| Học sinh của quỹ; cấp tài khoản học sinh (username + mật khẩu tạm) | Học sinh tự đăng ký; học sinh thuộc nhiều quỹ |
| Liên kết phụ huynh theo email (một tài khoản phụ huynh, nhiều con, nhiều quỹ) | Gửi email; xác minh email; quên mật khẩu qua email |
| Phân công / thu hồi thủ quỹ (nhiều thủ quỹ / quỹ), lịch sử phân công | Đổi vai trò tự do; thủ quỹ là người lớn |
| Ghi, sửa, hủy (có lý do) khoản thu / chi; cờ "công khai" | Phụ huynh nộp tiền, báo / xác nhận chuyển khoản, đề nghị chi |
| Số dư, tổng thu / chi cộng dồn; báo cáo tháng; thống kê 12 tháng | Công nợ từng học sinh, miễn giảm, hoàn trả |
| Danh mục thu / chi (thêm, đổi tên, ẩn) | Tích hợp ngân hàng, đính kèm ảnh, xuất Excel / PDF |

## 3. Người dùng

| Loại tài khoản | Có tài khoản bằng cách | Phạm vi |
|---|---|---|
| **Giáo viên** (`Teacher`) | Tự đăng ký (D-24) hoặc người vận hành chạy `create-teacher` | Nhiều quỹ do mình tạo (vai trò GVCN) |
| **Học sinh** (`Student`) | GVCN thêm vào quỹ rồi cấp tài khoản (D-27) | Đúng một quỹ; vai trò *Học sinh*, hoặc *Thủ quỹ* khi được phân công |
| **Phụ huynh** (`Parent`) | GVCN liên kết email phụ huynh với học sinh (D-28) | Mọi quỹ có con được liên kết |
| Người vận hành | Không phải người dùng ứng dụng | Lệnh server: `create-teacher`, `reset-password`, `disable-account`, `enable-account` |

Vai trò trong quỹ (**GVCN / Thủ quỹ / Học sinh / Phụ huynh**) do backend suy ra từ dữ liệu ở mỗi request (D-26). *Quy tắc v1.2 "mỗi tài khoản thuộc đúng một quỹ" (dẫn sai là D-11) hết hiệu lực – D-31, xem `open-questions.md` mục 1c.*

## 4. Yêu cầu chức năng

Cột vai trò: **G** = GVCN · **T** = Thủ quỹ · **H** = Học sinh chưa phân công · **P** = Phụ huynh.

### 4.1 Tài khoản & quỹ

| Mã | Yêu cầu | G | T | H | P | Trạng thái |
|---|---|---|---|---|---|---|
| FR-20 | **Đăng ký giáo viên**: họ tên, email (duy nhất), mật khẩu, xác nhận; đăng nhập luôn sau khi đăng ký | – | – | – | – | Partially Implemented (Backend) |
| FR-13 | **Đăng nhập** bằng một ô "Email hoặc tên đăng nhập" (email: giáo viên, phụ huynh; tên đăng nhập: học sinh) – **không chọn vai trò**, vai trò do hệ thống xác định; sai → thông báo chung; đúng nhưng bị khóa → "liên hệ giáo viên chủ nhiệm". Đăng xuất, đổi mật khẩu; **bắt buộc đổi mật khẩu tạm** trước khi dùng chức năng khác (403 `password.change_required`) | ✔ | ✔ | ✔ | ✔ | Partially Implemented (Backend) |
| FR-21 | **Tài khoản cá nhân**: xem thông tin, đổi họ tên (`PUT /auth/me`) | ✔ | ✔ | ✔ | ✔ | Partially Implemented (Backend) |
| FR-22 | **Quỹ của tôi**: danh sách quỹ kèm vai trò; GVCN thấy thêm số học sinh, tên thủ quỹ, trạng thái "Chưa phân công / Đã có thủ quỹ"; phụ huynh thấy tên con | ✔ | ✔ | ✔ | ✔ | Partially Implemented (Backend) |
| FR-23 | **Tạo quỹ** (chỉ giáo viên): tên quỹ → quỹ mới + danh mục mặc định, chưa có thủ quỹ | ✔ | – | – | – | Partially Implemented (Backend) |
| FR-11 | **Thông tin quỹ**: đổi tên quỹ | ✔ | – | – | – | Partially Implemented (Backend) |
### 4.2 Học sinh, phụ huynh, thủ quỹ

| Mã | Yêu cầu | G | T | H | P | Trạng thái |
|---|---|---|---|---|---|---|
| FR-24 | **Học sinh**: danh sách (họ tên, tên đăng nhập, trạng thái tài khoản *Chưa cấp / Chờ kích hoạt / Đang hoạt động / Bị khóa*, thủ quỹ, phụ huynh); thêm (họ tên, email phụ huynh tùy chọn); sửa họ tên; chi tiết | ✔ | – | – | – | Partially Implemented (Backend) |
| FR-25 | **Cấp tài khoản học sinh**: tên đăng nhập (bỏ trống → hệ thống gợi ý) + mật khẩu tạm ngẫu nhiên, hiển thị một lần; **cấp lại mật khẩu tạm** (phiên cũ hết hiệu lực) | ✔ | – | – | – | Partially Implemented (Backend) |
| FR-26 | **Liên kết phụ huynh** theo email: có tài khoản phụ huynh → chỉ liên kết; chưa có → tạo tài khoản + mật khẩu tạm hiển thị một lần; ghi rõ hệ thống không gửi email; **gỡ liên kết** (giữ lịch sử) | ✔ | – | – | – | Partially Implemented (Backend) |
| FR-27 | **Phân công thủ quỹ**: chọn học sinh đã kích hoạt tài khoản; nhiều thủ quỹ; **thu hồi** (không thu hồi được thủ quỹ cuối cùng); lịch sử người / thời điểm phân công, thu hồi | ✔ | – | – | – | Not Implemented |
| FR-12 | **Thành viên**: chỉ họ tên + vai trò (GVCN, thủ quỹ, học sinh) – không email / tên đăng nhập / liên hệ. *(v1.2: thủ quỹ tạo / khóa / đổi vai trò thành viên – thay bằng FR-24 → FR-27)* | ✔ | ✔ (chỉ xem) | – | – | Not Implemented |
| FR-28 | GVCN khóa / mở khóa tài khoản học sinh; học sinh rời lớp; chuyển quỹ cho giáo viên khác | – | – | – | – | Pending Decision (PD-03 → PD-05) |

### 4.3 Thu chi & báo cáo

| Mã | Yêu cầu | G | T | H | P | Trạng thái |
|---|---|---|---|---|---|---|
| FR-01 | **Tổng quan**: cộng dồn (số dư, tổng thu, tổng chi) + tháng hiện tại; 5 giao dịch gần nhất (bản công khai với H, P); người quản lý (GVCN + thủ quỹ: họ tên, vai trò); trạng thái thủ quỹ; cảnh báo số dư âm | ✔ | ✔ | ✔ | ✔ | Not Implemented |
| FR-02 / FR-03 | **Thêm khoản thu / chi**: nội dung, số tiền, ngày, danh mục, ghi chú (tùy chọn), cờ công khai (mặc định bật) | – | ✔ | – | – | Partially Implemented (Backend) |
| FR-04 | **Sửa** (số tiền, ngày, danh mục, nội dung, ghi chú, cờ công khai); lưu người sửa | – | ✔ | – | – | Partially Implemented (Backend) |
| FR-05 | **Hủy** bắt buộc lý do; lưu người hủy; không tính vào số dư / báo cáo | – | ✔ | – | – | Partially Implemented (Backend) |
| FR-06 | **Lịch sử thu chi**: lọc ngày, loại, danh mục, từ khóa, trạng thái | ✔ | ✔ | ✔ công khai | ✔ công khai | Partially Implemented (Backend) |
| FR-07 | **Chi tiết** + lịch sử thay đổi (người, thời điểm, trước / sau). H, P: chỉ nội dung, ngày, danh mục, số tiền của khoản công khai; khoản không công khai → 404 | ✔ | ✔ | công khai | công khai | Partially Implemented (Backend) |
| FR-08 | **Báo cáo tháng** (mọi tháng): đầu / cuối tháng, tổng thu / chi, theo danh mục, danh sách. H, P: phần không công khai gộp "Khoản khác", không số lượng (P1) | ✔ | ✔ | ✔ | ✔ | Not Implemented |
| FR-09 | **Thống kê 12 tháng** | ✔ | ✔ | ✔ | ✔ | Not Implemented |
| FR-10 | **Danh mục**: thêm, đổi tên, ẩn / hiện. H, P chỉ thấy danh mục đang hiện có khoản công khai (X-01) | xem | ✔ | xem | xem | Partially Implemented (Backend) |
## 5. Yêu cầu phi chức năng

| Mã | Yêu cầu |
|---|---|
| NFR-01 | Tech Stack và kiến trúc theo `training-doc.md` |
| NFR-02 | Số tiền VND, số nguyên dương; ngày dd/MM/yyyy; ngày nghiệp vụ theo giờ Việt Nam; chỉ tiếng Việt |
| NFR-03 | Số dư luôn khớp lịch sử giao dịch, kể cả khi sửa / hủy; nhấn Lưu nhiều lần không ghi trùng |
| NFR-04 | **Phân quyền ở backend** theo permission (`[RequirePermission]`). Vai trò trong quỹ suy ra từ dữ liệu (người phụ trách, học sinh, phân công thủ quỹ, liên kết phụ huynh) của phiên đăng nhập; `fundId` trên đường dẫn chỉ chọn quỹ, không cấp quyền; không nhận `teacherId` / `role` từ client. Không thuộc quỹ → 404 *(sửa 2026-11-05)* |
| NFR-05 | Không xóa vĩnh viễn giao dịch; mọi thêm / sửa / hủy và thao tác tài khoản thành viên đều ghi nhật ký (ai, lúc nào, giá trị trước / sau) |
| NFR-06 | Giao diện dùng tốt trên điện thoại (phụ huynh chủ yếu dùng điện thoại) |
| NFR-07 | Frontend ẩn nút không có quyền. Backend vẫn chặn khi gọi API trực tiếp (403) |
| NFR-08 | Phụ huynh *(và học sinh chưa phân công – 2026-11-05)* không bao giờ nhận được ghi chú nội bộ, người ghi / sửa / hủy, lý do hủy, nhật ký, nội dung / ngày / danh mục / số lượng của khoản không công khai, hay email / liên hệ / trạng thái tài khoản khác – qua bất kỳ API nào (danh sách, chi tiết, tìm kiếm, phân trang, báo cáo, thống kê, danh mục). Dữ liệu bị loại ngay trong truy vấn, không chỉ ẩn ở giao diện |
| NFR-09 | Mỗi thao tác ghi lưu đúng người thực hiện (`CreatedBy`, `UpdatedBy`, `CancelledBy` + nhật ký). Thao tác đồng thời của nhiều thủ quỹ không làm sai số dư, không mất lịch sử, không để quỹ mất thủ quỹ hoạt động cuối cùng |
| NFR-10 | Không ghi mật khẩu (kể cả mật khẩu tạm), hash, cookie, token vào log hay nhật ký |
| NFR-11 | Response chứa mật khẩu tạm có `Cache-Control: no-store`; mật khẩu tạm không lưu dạng rõ, không xem lại được *(2026-11-05)* |
| NFR-12 | Migration bổ sung, giữ toàn bộ dữ liệu; không xóa cứng học sinh, liên kết phụ huynh, phân công thủ quỹ (trigger `TR_NoDelete`) *(2026-11-05)* |

## 6. Lịch sử thay đổi

| Ngày | Phiên bản | Thay đổi | Lý do |
|---|---|---|---|
| 2026-11-04 | 1.0 | Phạm vi một thủ quỹ | Review & Simplify |
| 2026-11-04 | 1.1 | Thêm 3 vai trò, FR-11 → FR-13, NFR-07/08 | Task điều chỉnh phạm vi |
| 2026-11-04 | 1.2 | FR-01 (B4, Q-12), FR-06 (B2, B6, U3), FR-07 (B2, B3), FR-08 (D-12, U1), FR-12 (Q-11, D-11), FR-13 (B7), NFR-08 mở rộng, thêm NFR-09, NFR-10 | PO duyệt D-10, D-11, D-12, Q-11, Q-12, B2–B7 |
| 2026-11-05 | 2.0 | Mô hình Giáo viên · Học sinh · Phụ huynh: viết lại mục 1 → 4; thêm FR-20 → FR-28, NFR-11, NFR-12; sửa FR-12, FR-13, NFR-04, NFR-08; thêm cột Trạng thái | PO duyệt D-24 → D-33 |
| 2026-11-05 | 2.1 | FR-13 đăng nhập không chọn vai trò; cột Trạng thái → Not Implemented (mã nguồn đã xóa) | PO |
| 2026-11-06 | 2.2 | Cột Trạng thái: FR-20, FR-13, FR-21, FR-22, FR-23, FR-11 → Partially Implemented (Backend) | Triển khai Auth, Fund Lifecycle (chưa có UI) |
| 2026-11-06 | 2.3 | Cột Trạng thái: FR-24, FR-25, FR-26 → Partially Implemented (Backend) | Triển khai Học sinh & phụ huynh (chưa có UI) |
