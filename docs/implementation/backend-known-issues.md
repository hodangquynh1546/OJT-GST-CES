# Backend Known Issues

> Cập nhật 2026-11-05. Mã nguồn đã xóa – trạng thái **Not Implemented**. Các mục dưới là rủi ro / hạn chế của **thiết kế**, cần xử lý khi triển khai lại (KI-01, KI-09 chỉ còn là lịch sử). Historical: 124 / 124 test đạt trước khi xóa.

| Mã | Nội dung |
|---|---|
| KI-01 | DB dev đã áp 14 migration cũ không áp được `InitialClassFund` – cần tạo lại DB (xóa dữ liệu; người vận hành tự làm) |
| KI-02 | Sửa đồng thời cùng một giao dịch: lần ghi sau thắng (DD-06). *2026-11-05:* nhiều thủ quỹ / quỹ nên có thể xảy ra; nhật ký ghi đủ cả hai lần |
| KI-03 | Không có quên mật khẩu tự phục vụ. Học sinh: GVCN cấp lại. Giáo viên / phụ huynh: lệnh `reset-password` (Q-07, PD-02) |
| KI-04 | Prototype UI trên Design Canvas: cập nhật theo mô hình 2.0 (xem `docs/ui-design`) – vẫn là prototype, chưa có frontend |
| KI-05 | Câu hỏi nghiệp vụ Q-01 → Q-10 (`docs/requirements/open-questions.md`) |
| KI-06 | Đăng ký giáo viên chưa xác minh email → có thể chiếm trước email của phụ huynh (PD-01) |
| KI-07 | Thủ quỹ có tài khoản bị khóa / đang chờ đổi mật khẩu vẫn được tính là "thủ quỹ" khi kiểm tra thủ quỹ cuối cùng (PD-06) |
| KI-08 | GVCN thấy họ tên do giáo viên khác đặt cho tài khoản phụ huynh dùng chung (phụ huynh tự sửa được qua `PUT /auth/me`) |
| KI-09 | Chưa chạy API thủ công bằng `dotnet run` ở lượt 2026-11-05 (đã kiểm chứng bằng Integration Test) |
