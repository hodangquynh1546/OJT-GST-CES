# Test case nghiệp vụ

> Mỗi module bổ sung khi làm. Mã: `TC-<module>-<số>`. Cột "Tự động" ghi tên test tương ứng.
> Dưới đây là các test case **bắt buộc** đã xác định từ yêu cầu – module phải phủ.

| Mã | Mô tả | FR/BR | Kỳ vọng | Tự động |
|---|---|---|---|---|
| TC-M1-01 | Đăng ký tài khoản mới | FR-01 | 201, đăng nhập luôn, `accountPermissions` có `Fund.Create` | |
| TC-M1-02 | Học sinh đăng nhập bằng mật khẩu tạm rồi gọi API khác | FR-03 | 403 `password.change_required` | |
| TC-M1-03 | Đổi mật khẩu xong, cookie cũ ở trình duyệt khác | BR-29 | 401 | |
| TC-M3-01 | Thêm học sinh bằng email chưa có tài khoản | FR-09 | Trả mật khẩu tạm một lần; AuditLog không chứa mật khẩu | |
| TC-M4-01 | Thủ quỹ (không phải Owner) thu hồi thủ quỹ khác | FR-13 | 403 | |
| TC-M6-01 | Học sinh nộp chuyển khoản không có minh chứng | BR-11 | 422 | |
| TC-M6-02 | Thủ quỹ xác nhận thanh toán của học sinh | BR-10 | Paid, LedgerEntry thu, audit `IsSelfAction=false`, thông báo học sinh | |
| TC-M6-03 | Thủ quỹ tự nộp và tự xác nhận | BR-12 | `SelfDeclared`, `IsSelfDeclared=true`, audit `IsSelfAction=true` | |
| TC-M6-04 | Từ chối không có lý do | FR-21 | 400 | |
| TC-M6-05 | Khoản thu sự kiện tạo ra | BR-18, BR-32 | Chỉ người tham gia có nghĩa vụ và nhận thông báo | |
| TC-M7-01 | Ghi chi 600.000đ không chứng từ | BR-06 | 422 `evidence.required` | |
| TC-M7-02 | Ghi chi 500.000đ không chứng từ | BR-06 | 201 | |
| TC-M7-03 | Tổng quan quỹ chung khi có giao dịch sự kiện | BR-02 | Không cộng giao dịch sự kiện | |
| TC-M8-01 | Chốt danh sách khi có người chưa đóng đủ | BR-19 | Người đó bị loại, nghĩa vụ `Excluded` | |
| TC-M9-01 | Thừa 150.000đ, 15 người, chia đều, cộng số dư | BR-22 | Mỗi người 10.000đ, số dư tăng, BalanceTransaction, thông báo | |
| TC-M9-02 | Thừa 100.000đ, 3 người chia đều | BR-22, OQ-03 | 33.334 + 33.333 + 33.333 = 100.000 | |
| TC-M9-03 | Thiếu 100.000đ | BR-23 | Khoản thu bổ sung cho người thực tế, sự kiện Settled | |
| TC-M9-04 | Quyết toán không chạm quỹ chung | BR-03 | Số dư quỹ chung trước = sau | |
| TC-M10-01 | Bấm "Rút tiền" khi số dư 150.000đ | BR-26, OQ-11 | Yêu cầu 150.000đ (toàn bộ); số dư bị giữ; thủ quỹ nhận thông báo | |
| TC-M10-03 | Gửi yêu cầu rút thứ hai khi đang chờ | BR-26 | 422 `withdrawal.pending_exists` | |
| TC-M10-04 | Thủ quỹ xác nhận đã hoàn tiền | BR-26 | Số dư về 0, BalanceTransaction `Withdrawal`, thông báo người dùng | |
| TC-M10-02 | Dùng số dư đóng khoản thu chung | FR-25, OQ-10 | Thành công; LedgerEntry thu vào quỹ chung; BalanceTransaction `ChargePayment` | |
| TC-M10-05 | Dùng số dư khi đang chờ rút | BR-15 | 422 `balance.locked_by_withdrawal` | |
| TC-M12-01 | Học sinh xem audit | FR-47 | 403 | |
| TC-M13-01 | Học sinh xem báo cáo học sinh khác | FR-51 | 403/404 | |
| TC-M14-01 | Quyết toán cuối năm khi còn sự kiện chưa quyết toán | BR-35 | 422 `year_end.open_events` | |
| TC-M14-02 | Quỹ chung dư 300.000đ, 30 thành viên, chia đều, cộng số dư | BR-36 | Mỗi người +10.000đ, quỹ `Archived`, thông báo cả lớp (Quỹ chung) | |
| TC-M14-03 | Quyết toán cuối năm lần thứ hai | BR-34 | 422 `year_end.already_closed` | |
| TC-M14-04 | Tạo khoản thu sau khi quỹ Archived | BR-37 | 422 `fund.archived` | |
| TC-M5-01 | Upload ảnh đại diện PDF hoặc > 2 MB | BR-07 | 422 `file.invalid` | |
| TC-M5-02 | Học sinh tải bill chuyển khoản của học sinh khác | AZ-05 | 404 | |
