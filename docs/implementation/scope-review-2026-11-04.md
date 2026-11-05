# Review & Simplify – 2026-11-04

> *Lịch sử (2026-11-05): tài liệu rà soát theo thời điểm 2026-11-04; mã nguồn được nhắc tới đã xóa (D-35). Hiện trạng: [implementation-status.md](implementation-status.md).*

## 1. Đánh giá thiết kế cũ (v0.7)
Thiết kế cũ là **hệ thống quản trị tài chính toàn trường**: 5 vai trò, 56 permission, 39 bảng, 16 loại trạng thái, 13 migration, 124 endpoint, ~10.900 dòng backend.
Mỗi khoản chi phải qua phiếu chi → giữ chỗ → GVCN duyệt → ghi sổ cái; mỗi khoản thu phải qua đợt thu → công nợ từng học sinh → phiếu thu / báo chuyển khoản → xác nhận → phân bổ → tiền thừa.
Đúng về kỹ thuật nhưng **sai bài toán**: thủ quỹ một lớp không làm kế toán nhiều cấp, không có người duyệt, không cần hiểu "giữ chỗ", "phân bổ", "bàn giao", "đảo giao dịch".

## 2. Đối chiếu từng tính năng

| Tính năng cũ | Quyết định | Lý do / thay thế |
|---|---|---|
| Đăng nhập email + mật khẩu, cookie, CSRF, rate limit | **Giữ** | Bảo vệ dữ liệu tiền của lớp |
| Hạ tầng M0 (ProblemDetails, correlation, log, versioning, Swagger, health, 2 tài khoản DB) | **Giữ** | Đang chạy tốt, không ảnh hưởng người dùng |
| Phân quyền theo permission | **Đơn giản hóa** | Giữ `[RequirePermission]` (training-doc) với 5 permission cố định cho thủ quỹ; bỏ bảng Role / Permission / UserRole / grant |
| Phiếu chi + giữ chỗ + duyệt / từ chối / gửi lại | **Thay** bằng "Thêm khoản chi" lưu là xong | Không có người duyệt trong thực tế |
| Phiếu thu tiền mặt, báo chuyển khoản, xác nhận, phân bổ, tiền thừa | **Thay** bằng "Thêm khoản thu" | Thủ quỹ chỉ cần ghi tiền đã vào quỹ |
| Thu khác | **Gộp** vào khoản thu + danh mục | Cùng một thao tác |
| Danh mục chi chung / riêng | **Đơn giản hóa**: danh mục thu + chi của quỹ, có mặc định, ẩn thay cho xóa | Mục 4.6 của task |
| Sổ cái + số dư cache + khóa dòng + trigger khóa sổ | **Thay** bằng số dư tính từ giao dịch | Luôn đúng, không cần khóa |
| Hủy / đảo giao dịch bằng chứng từ riêng | **Thay** bằng sửa trực tiếp + "Hủy giao dịch" (cờ) + nhật ký | Đơn giản, vẫn truy vết được |
| Báo cáo sổ quỹ, chi theo danh mục, tổng quan | **Đơn giản hóa**: tổng quan quỹ, báo cáo tháng, thống kê 12 tháng | Mục 4.1, 4.5 |
| Nhật ký thay đổi (AuditLog) | **Giữ** (ẩn với người dùng) | Truy vết sửa / hủy số tiền |
| Chống gửi trùng (idempotency) | **Giữ** | Nhấn Lưu nhiều lần |
| Năm học, lớp, học sinh, phụ huynh, GVCN, lời mời | **Loại bỏ** | Ngoài phạm vi (mục 2.1) |
| Đợt thu, công nợ, miễn giảm, nhắc nộp | **Loại bỏ** | Ngoài phạm vi; câu hỏi Q-04 nếu cần biết ai đã đóng |
| Số dư cá nhân, hoàn trả, bàn giao số dư | **Loại bỏ** | Ngoài phạm vi |
| Chuyển quỹ, khóa sổ năm học, người giữ quỹ, thu hộ, bàn giao tiền, chênh lệch | **Loại bỏ** | Chỉ có ý nghĩa khi quản lý nhiều lớp / năm |
| Thông báo, việc cần xử lý | **Loại bỏ** | Một người dùng, không có việc chờ người khác |
| File đính kèm | **Loại bỏ** | Không bắt buộc; câu hỏi Q-06 |
| Quên mật khẩu qua email, lời mời qua email | **Loại bỏ** | Chưa có email (PD-03); đặt lại bằng lệnh (Q-07) |

## 3. Flow trước / sau

| | Trước | Sau |
|---|---|---|
| Ghi một khoản chi | Lập phiếu + chứng từ → giữ chỗ → chờ GVCN → duyệt → ghi sổ (2 người, 4 trạng thái) | Thêm khoản chi → nhập → Lưu (1 người, có hiệu lực ngay) |
| Ghi một khoản thu | Đợt thu → công nợ → phiếu thu / báo CK → xác nhận → phân bổ | Thêm khoản thu → nhập → Lưu |
| Sửa nhầm | Phiếu đã duyệt không sửa được → đảo giao dịch (chờ duyệt) → lập phiếu mới | Mở giao dịch → sửa → Lưu |
| Xóa nhầm | Không có | Hủy giao dịch (giữ lịch sử) |

## 4. Số liệu sau tái cấu trúc
5 bảng · 1 migration · 17 endpoint · 1 trạng thái (đã hủy / chưa hủy) · 5 permission cố định · 2.632 dòng ở 48 file backend (gồm cả hạ tầng M0, không tính migration; trước: ~10.900 dòng ở 93 file).
