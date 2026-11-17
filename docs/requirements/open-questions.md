# Câu hỏi mở & quyết định

> **M0-01 đã duyệt (2026-11-17).** OQ-10, OQ-11, OQ-13, OQ-16 được nhóm quyết định khác đề xuất ban đầu; các OQ còn lại chốt theo đề xuất.
> Câu hỏi mới phát sinh khi làm module: thêm dòng `OQ-xx` với trạng thái `Chờ duyệt`.

| Mã | Câu hỏi | Quyết định | Ảnh hưởng | Trạng thái |
|---|---|---|---|---|
| OQ-01 | "Không tự động cộng tiền vào quỹ chỉ dựa trên thao tác xác nhận của thủ quỹ" – tiền thủ quỹ tự ghi nhận có tính vào số dư quỹ không? | Có ghi `LedgerEntry` thu với cờ `IsSelfDeclared = true`. Báo cáo hiển thị **tách hai dòng**: "Thu đã xác nhận" và "Thu thủ quỹ tự ghi nhận (chưa đối chiếu)"; số dư = tổng cả hai nhưng luôn kèm dòng chưa đối chiếu. | M6, M7, M13 | Đã chốt (theo đề xuất) |
| OQ-02 | Thủ quỹ khác có được xác nhận thanh toán của một thủ quỹ không? | Có, khi đó `ConfirmationMode = Verified`. | M6 | Đã chốt (theo đề xuất) |
| OQ-03 | Chia tiền thừa: chia đều hay theo tỷ lệ? | Thủ quỹ chọn khi quyết toán (`Equal` / `Proportional`). Làm tròn xuống tới đồng, phần dư lẻ chia lần lượt 1đ cho người đóng nhiều nhất (largest remainder), tổng chia luôn bằng tiền thừa. Áp dụng cho cả quyết toán sự kiện và tổng kết cuối năm. | M9, M14 | Đã chốt (theo đề xuất) |
| OQ-04 | Một sự kiện có nhiều khoản thu: điều kiện "hoàn thành nghĩa vụ" khi chốt danh sách? | Phải `Paid` **tất cả** khoản thu thường (Regular) của sự kiện có hạn ≤ thời điểm chốt. | M8 | Đã chốt (theo đề xuất) |
| OQ-05 | Khoản thu sự kiện tạo sau khi chốt danh sách áp cho ai? | Chỉ người tham gia thực tế (`IsFinal = true`). Trước khi chốt: người đã xác nhận tham gia. | M6, M8 | Đã chốt (theo đề xuất) |
| OQ-06 | Đóng một phần (trả nhiều lần) có cho phép không? | Không. Mỗi nghĩa vụ đóng đủ một lần; bị từ chối thì gửi lại. | M6 | Đã chốt (theo đề xuất) |
| OQ-07 | Học sinh có được tạo quỹ không? | Không. Chỉ tài khoản tự đăng ký (`SelfRegistered`) có `Fund.Create`. | M1, M2 | Đã chốt (theo đề xuất) |
| OQ-08 | Một người có thể là thành viên nhiều quỹ? | Có. Email là định danh toàn hệ thống. | M1–M3 | Đã chốt (theo đề xuất) |
| OQ-09 | Thủ quỹ chính có chuyển quyền Owner được không? | Ngoài phạm vi bản đầu. Mỗi quỹ đúng một Owner. | M4 | Đã chốt (theo đề xuất) |
| OQ-10 | Dùng số dư cá nhân đóng khoản thu chung? | **Có.** Số dư cá nhân dùng được để đóng **cả khoản thu chung và khoản thu sự kiện**. | M6, M10 | **Đã chốt – nhóm quyết định** |
| OQ-11 | Rút tiền: rút một phần hay toàn bộ? | **Rút một lần toàn bộ số dư.** Người dùng bấm nút "Rút tiền" → gửi yêu cầu (số tiền = toàn bộ số dư lúc gửi) sang thủ quỹ → thủ quỹ liên hệ, đưa lại tiền → xác nhận trên hệ thống. Trong khi chờ, số dư bị giữ (không dùng đóng khoản thu); mỗi người chỉ một yêu cầu đang chờ. | M10 | **Đã chốt – nhóm quyết định** |
| OQ-12 | "Khoản thu sắp đến hạn" – trước bao nhiêu ngày? Cơ chế gửi? | 2 ngày trước hạn, cho người còn `Unpaid`; tạo khi mở ứng dụng (idempotent theo ngày), chưa cần job nền. | M11 | Đã chốt (theo đề xuất) |
| OQ-13 | Tổng kết cuối năm có "khóa" quỹ không? Xử lý số dư cuối năm thế nào? | **Quyết toán quỹ chung tương tự quyết toán sự kiện, làm MỘT lần duy nhất, chia cho cả lớp** (mọi thành viên đang hoạt động, gồm thủ quỹ). Thừa → hoàn trực tiếp / cộng số dư cá nhân, chia đều / theo tỷ lệ đóng góp quỹ chung; thiếu → khoản thu bổ sung chung. Điều kiện: mọi sự kiện đã quyết toán hoặc hủy. Xong → quỹ `Archived` (chỉ xem; vẫn rút được số dư cá nhân). | M14 | **Đã chốt – nhóm quyết định** |
| OQ-14 | Học sinh xem được gì của quỹ chung? | Tổng thu, tổng chi, số dư, giao dịch chi kèm chứng từ, danh sách khoản thu; không xem trạng thái đóng của học sinh khác. | M7, M13 | Đã chốt (theo đề xuất) |
| OQ-15 | Xóa học sinh còn nợ / còn số dư? | Cho phép ngừng nhưng cảnh báo; nghĩa vụ `Unpaid` của khoản đang mở → `Cancelled`; số dư phải về 0 (rút) trước khi ngừng. | M3 | Đã chốt (theo đề xuất) |
| OQ-16 | Lưu file chứng từ ở đâu? | **Neon Object Storage** (S3-compatible) lưu **file thực tế**: ảnh hóa đơn, bill chuyển khoản, ảnh đại diện, tài liệu. DB chỉ lưu metadata + khóa. `Local` chỉ dùng cho test tự động. | M5 | **Đã chốt – nhóm quyết định** |
| OQ-17 | Thư viện .NET để gọi Neon Object Storage (S3 API)? `training-doc.md` chưa có package này. | Đề xuất `AWSSDK.S3` (client S3 chính thức, cấu hình `ServiceURL` = endpoint Neon, `ForcePathStyle = true`). Nhóm đồng ý (2026-11-17); package được cài trong task M5-T0. | M5 | **Đã chốt – nhóm quyết định** |
