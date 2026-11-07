# UI/UX – Câu hỏi cần xác nhận

> v4.0 · 2026-11-07 · Desktop Web only. Câu hỏi nghiệp vụ ảnh hưởng UI: PD-01 → PD-06, Q-01, Q-04, Q-06, Q-08 – xem [open-questions.md](../requirements/open-questions.md).

| Mã | Câu hỏi | Giả định tạm (đang vẽ trên Canvas) | Trạng thái |
|---|---|---|---|
| UQ-01 | Mặc định bộ lọc lịch sử là tháng hiện tại hay tất cả? | Tháng hiện tại, trạng thái "Đang hiệu lực" | Open |
| UQ-02 | Nhập số tiền có gợi ý nhanh (50.000, 100.000, …)? | Không | Open |
| UQ-03 | Nhận diện thương hiệu (logo, màu)? | **Đã chốt**: tên ClassFund, Kendo Default Theme, primary `#1d5fa8`, token `--cf-*` ([design-system.md](design-system.md)); logo chính thức chưa có | Closed |
| UQ-04 | Học sinh / phụ huynh có cần bảng 12 tháng trên mobile? | **Đã đóng**: bỏ phạm vi Mobile (D-36, v4.0) | Closed |
| UQ-05 | Định dạng mật khẩu tạm | **Đã chốt**: 16 ký tự ngẫu nhiên dạng `Xxxx-xxxx-0000-xxxx`, không ký tự dễ nhầm (D-27) | Closed |
| UQ-06 | Hiện lý do hủy cho phụ huynh / học sinh? | **Đã chốt**: không (X-02) | Closed |
| UQ-07 | Cảnh báo khi tắt "Công khai"? | **Đã chốt**: có, theo B1 = P1 | Closed |
| UQ-20 | Nút "Sao chép" thật và in phiếu thông tin đăng nhập cho học sinh? | Có nút Sao chép + tick xác nhận; chưa có in | Open |
| UQ-21 | Nhập học sinh hàng loạt (CSV)? | Không trong v1 | Open |
| UQ-22 | GVCN xem danh mục chỉ đọc trong "Thông tin quỹ"? | Chỉ ghi "Danh mục do thủ quỹ quản lý"; GVCN xem danh mục qua bộ lọc Thu chi | Open |
| UQ-23 | Khóa / mở tài khoản học sinh trên giao diện | Chưa có nút (chờ PD-03) | Pending Decision |
| UQ-24 | Đăng nhập có chọn vai trò? | **Đã chốt**: không – một ô "Email hoặc tên đăng nhập" (2026-11-05) | Closed |
