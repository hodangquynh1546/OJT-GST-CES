# Test case nghiệp vụ

> Mỗi module bổ sung khi làm. Mã: `TC-<module>-<số>`. Cột "Tự động" ghi tên test tương ứng.
> Dưới đây là các test case **bắt buộc** đã xác định từ yêu cầu – module phải phủ.

| Mã | Mô tả | FR/BR | Kỳ vọng | Tự động |
|---|---|---|---|---|
| TC-M1-01 | Đăng ký tài khoản mới | FR-01 | 201, đăng nhập luôn, `accountPermissions` có `Fund.Create` | `AuthApiTests.Register_signs_in_and_me_returns_the_new_user`, e2e full `đăng ký → vào hệ thống…` |
| TC-M1-02 | Học sinh đăng nhập bằng mật khẩu tạm rồi gọi API khác | FR-03 | 403 `password.change_required` | `AuthApiTests.Temporary_password_blocks_other_endpoints_until_changed`, e2e full `mật khẩu tạm (tài khoản seed)` |
| TC-M1-03 | Đổi mật khẩu xong, cookie cũ ở trình duyệt khác | BR-29 | 401 | `AuthApiTests.Changing_password_invalidates_other_sessions_but_keeps_the_current_one`, e2e full `BR-29` |
| TC-M1-04 | Đăng nhập sai mật khẩu / email không tồn tại | FR-02, BR-27 | 401 `auth.invalid_credentials`, cùng thông báo cho cả hai (không lộ email có tồn tại) | `AuthServiceTests`, `AuthApiTests.Login_with_*`, e2e mock/full |
| TC-M1-05 | Đăng nhập vào tài khoản bị khóa | FR-02 | 401 `auth.account_disabled` chỉ khi mật khẩu đúng; phiên đang mở bị từ chối ngay | `AuthApiTests.Disabled_account_cannot_login_and_existing_session_is_rejected` |
| TC-M1-06 | Đăng ký trùng email (khác hoa thường) / dữ liệu sai | BR-27, BR-28 | 409 `auth.email_exists` / 400 `validation_failed` kèm `errors` từng ô | `AuthApiTests.Register_with_*`, e2e full |
| TC-M1-07 | Đổi mật khẩu sai mật khẩu hiện tại | FR-04 | **422** `auth.current_password_incorrect`, vẫn đăng nhập (không bị đá ra đăng nhập) | `AuthApiTests.Change_password_with_wrong_current_password_is_422_and_keeps_session`, e2e mock/full |
| TC-M1-08 | Đăng nhập / đăng ký quá giới hạn trong 1 phút | NFR-01 | 429 `auth.rate_limited` | `AuthApiTests.Login_is_rate_limited_per_ip_with_429` |
| TC-M1-09 | Audit đăng ký / đổi mật khẩu / sửa hồ sơ | NFR-02 | Đúng `Action`, Scope `System`; không chứa mật khẩu, hash, SecurityStamp | `AuthServiceTests`, `AuthApiTests.*audit*` |
| TC-M1-10 | Request ghi thiếu header antiforgery | NFR-01 | 400 `antiforgery_failed` | `AuthApiTests.Write_request_without_antiforgery_header_is_rejected` |
| TC-M1-11 | Phiên hết hạn khi sửa hồ sơ (PUT /auth/me → 401) | FR-04 | FE xóa phiên và về `/login` | `interceptors.spec.ts`, e2e mock `phiên hết hạn giữa chừng` |
| TC-M3-01 | Thêm học sinh bằng email chưa có tài khoản | FR-09 | Trả mật khẩu tạm một lần; AuditLog không chứa mật khẩu | |
| TC-M3-02 | Thêm nhiều email một lần: 1 mới, 1 đã có tài khoản, 1 đã là thành viên, 1 sai định dạng, 1 trùng trong danh sách | FR-09, OQ-20 | Kết quả từng dòng `created`/`existing_added`/`already_exists`/`invalid`, email trùng chỉ xử lý một lần, email lỗi không ảnh hưởng email khác; mật khẩu tạm chỉ cho tài khoản mới, AuditLog không chứa mật khẩu | |
| TC-M3-03 | Thêm nhiều email với danh sách > 50 hoặc khi quỹ Archived | OQ-20, BR-37 | 400 / 422 `fund.archived` | |
| TC-M4-01 | Thủ quỹ (không phải Owner) thu hồi thủ quỹ khác | FR-13 | 403 | |
| TC-M6-01 | Học sinh nộp chuyển khoản không có minh chứng | BR-11 | 422 | |
| TC-M6-02 | Thủ quỹ xác nhận thanh toán của học sinh | BR-10 | Paid, LedgerEntry thu, audit `IsSelfAction=false`, thông báo học sinh | |
| TC-M6-03 | Thủ quỹ tự nộp và tự xác nhận | BR-12 | `SelfDeclared`, `IsSelfDeclared=true`, audit `IsSelfAction=true` | |
| TC-M6-04 | Từ chối không có lý do | FR-21 | 400 | |
| TC-M6-05 | Khoản thu sự kiện tạo ra | BR-18, BR-32 | Chỉ người tham gia có nghĩa vụ và nhận thông báo | |
| TC-M7-01 | Ghi chi 600.000đ không chứng từ | BR-06 | 422 `evidence.required` | |
| TC-M7-02 | Ghi chi 500.000đ không chứng từ | BR-06 | 201 | |
| TC-M7-03 | Tổng quan quỹ chung khi có giao dịch sự kiện | BR-02 | Không cộng giao dịch sự kiện | |
| TC-M7-04 | Ghi chi lớn hơn số dư phạm vi (sự kiện còn 3.500.000đ, chi 3.600.000đ) | BR-38 | 422 `ledger.exceeds_balance`, không ghi sổ | |
| TC-M8-01 | Kết thúc sự kiện: A chưa đóng khoản nào, B đóng 1/2 khoản, C có thanh toán đang chờ | BR-19 | A bị loại, nghĩa vụ `Excluded`, nhận thông báo; B, C vẫn `IsFinal`; B nhận thông báo nhắc | |
| TC-M8-02 | Sau khi kết thúc: tạo khoản thu / ghi chi sự kiện | BR-20 | 422; B nộp phần còn lại và được xác nhận → 200 | |
| TC-M9-01 | Thừa 150.000đ, 3 người đã đóng 100.000 / 100.000 / 50.000, cộng số dư | BR-22 | 60.000 + 60.000 + 30.000; số dư tăng, BalanceTransaction, thông báo | |
| TC-M9-02 | Thừa 100.000đ, 3 người đã đóng bằng nhau | BR-22, OQ-03 | 33.334 + 33.333 + 33.333 = 100.000 (largest remainder) | |
| TC-M9-03 | Tổng chia khi tỷ lệ lẻ (27 người 150.000 + 1 người 100.000, thừa 150.000) | BR-22 | 19 × 5.422 + 8 × 5.421 + 3.614 = 150.000 | |
| TC-M9-04 | Quyết toán không chạm quỹ chung | BR-03 | Số dư quỹ chung trước = sau | |
| TC-M10-01 | Bấm "Rút tiền" khi số dư 150.000đ | BR-26, OQ-11 | Yêu cầu 150.000đ (toàn bộ); số dư bị giữ; thủ quỹ nhận thông báo | |
| TC-M10-03 | Gửi yêu cầu rút thứ hai khi đang chờ | BR-26 | 422 `withdrawal.pending_exists` | |
| TC-M10-04 | Thủ quỹ xác nhận đã hoàn tiền | BR-26 | Số dư về 0, BalanceTransaction `Withdrawal`, thông báo người dùng | |
| TC-M10-02 | Dùng số dư đóng khoản thu chung | FR-25, OQ-10 | Thành công; LedgerEntry thu vào quỹ chung; BalanceTransaction `ChargePayment` | |
| TC-M10-05 | Dùng số dư khi đang chờ rút | BR-15 | 422 `balance.locked_by_withdrawal` | |
| TC-M12-01 | Học sinh xem audit | FR-47 | 403 | |
| TC-M13-01 | Học sinh xem báo cáo học sinh khác | FR-51 | 403/404 | |
| TC-M14-01 | Quyết toán cuối năm khi còn sự kiện chưa quyết toán | BR-35 | 422 `year_end.open_events` | |
| TC-M14-02 | Quỹ chung dư 300.000đ, 30 thành viên đã đóng bằng nhau, cộng số dư (theo tỷ lệ – OQ-19) | BR-36 | Mỗi người +10.000đ, quỹ `Archived`, thông báo cả lớp (Quỹ chung) | |
| TC-M14-03 | Quyết toán cuối năm lần thứ hai | BR-34 | 422 `year_end.already_closed` | |
| TC-M14-04 | Tạo khoản thu sau khi quỹ Archived | BR-37 | 422 `fund.archived` | |
| TC-M14-05 | Quỹ Archived: học sinh có số dư gửi yêu cầu rút, thủ quỹ xác nhận đã trả | BR-37, OQ-11 | Thành công; số dư về 0; không bị chặn `fund.archived` | |
| TC-M5-01 | Upload ảnh đại diện PDF hoặc > 2 MB | BR-07 | 422 `file.invalid` | |
| TC-M5-02 | Học sinh tải bill chuyển khoản của học sinh khác | AZ-05 | 404 | |
