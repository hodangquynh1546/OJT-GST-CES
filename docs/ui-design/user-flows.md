# User flows

> v3.1 · 2026-11-05 · Canvas: D-03 (13 luồng, từng bước có nhánh cảnh báo / lỗi). Trạng thái triển khai: **Not Implemented**. Quy trình nghiệp vụ tương ứng: P-01 → P-16 trong [business-rules-and-processes.md](../business-analysis/business-rules-and-processes.md).

| Mã | Vai trò | Luồng | Nhánh lỗi / cảnh báo |
|---|---|---|---|
| UF-01 | GVCN | A-01 họ tên, email, mật khẩu, xác nhận → đăng nhập luôn → G-01 trống → "+ Tạo quỹ" → quỹ mới (danh mục mặc định, "Chưa phân công thủ quỹ") | Email đã dùng; mật khẩu yếu; xác nhận không khớp |
| UF-02 | GVCN | G-02 "+ Thêm học sinh" (họ tên, email phụ huynh tùy chọn) → nếu tạo tài khoản phụ huynh mới: hộp thoại mật khẩu tạm một lần → GVCN tự chuyển | Email đã có tài khoản phụ huynh → chỉ liên kết; email giáo viên / học sinh → lỗi |
| UF-03 | GVCN → Học sinh | G-02 chi tiết → "Cấp tài khoản" (tên đăng nhập gợi ý) → mật khẩu tạm một lần → học sinh đăng nhập ở A-02 → A-03 đặt mật khẩu mới → "Đang hoạt động" | Tên đăng nhập trùng / sai định dạng; đã có tài khoản |
| UF-04 | GVCN | G-03 "+ Phân công thủ quỹ" → chọn học sinh đã kích hoạt → đổi người: phân công người mới trước → "Thu hồi" người cũ (xác nhận) → lịch sử cập nhật | Chưa kích hoạt / chưa có tài khoản / đang là thủ quỹ → không chọn được; thủ quỹ cuối → nút Thu hồi bị khóa |
| UF-05 | Thủ quỹ | S-01 / S-02 "+ Thêm khoản" → T-01 → Lưu → thông báo + số dư mới | Lỗi từng ô; số dư âm → cảnh báo, vẫn lưu; tắt công khai → cảnh báo P1; nhấn Lưu 2 lần không trùng |
| UF-06 | Thủ quỹ | S-02 chi tiết → Sửa (không đổi loại) / Hủy (lý do bắt buộc) → lịch sử thay đổi thêm dòng | Thiếu lý do; 409 khi thủ quỹ khác vừa hủy |
| UF-07 | Thủ quỹ | T-02 thêm / đổi tên / ẩn danh mục; T-03 xem thành viên (họ tên + vai trò) | Trùng tên danh mục; không có nút quản lý học sinh |
| UF-08 | GVCN | S-01 → S-02 (ghi chú, lý do hủy, người thực hiện, lịch sử) → S-03 | Không có nút ghi; gọi API ghi trực tiếp → 403 |
| UF-09 | Học sinh | A-02 tên đăng nhập → S-01 → S-03 bản công khai | Khoản không công khai → không hiện; "Khoản khác" không số lượng |
| UF-10 | Phụ huynh | A-02 email → G-01 "Quỹ của con" → chọn quỹ → S-01 → S-03 bản công khai | Con là thủ quỹ → phụ huynh vẫn không có nút ghi (403) |
| UF-11 | GVCN | G-02 chi tiết → "Cấp lại mật khẩu tạm" (xác nhận) → mật khẩu tạm mới một lần | Học sinh bị đăng xuất mọi nơi; thủ quỹ chưa đổi mật khẩu không ghi thu chi được |
| UF-12 | GVCN | Quỹ chuyển từ phiên bản cũ: S-01 / G-03 hiện "Chưa phân công thủ quỹ" → G-02 → G-03 | Dữ liệu cũ giữ nguyên; chưa ai ghi được tới khi có thủ quỹ |
| UF-13 | Mọi vai trò | A-04 sửa họ tên; đổi mật khẩu (8–128, chữ + số) → phiên khác đăng xuất; Đăng xuất → A-02 | Sai mật khẩu hiện tại |

Bản 2.x (UF-05 / UF-06 thủ quỹ tạo thành viên, đổi vai trò) đã thay thế – Git history.
