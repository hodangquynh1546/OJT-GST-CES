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
| TC-M3-01 | Thêm học sinh bằng email chưa có tài khoản | FR-09 | Trả mật khẩu tạm một lần; AuditLog không chứa mật khẩu | `MembersServiceTests.Add_*`, `MembersApiTests.Owner_adds_a_new_email…`, `…Add_audit_is_written…`, e2e mock/full `Thêm một email` |
| TC-M3-02 | Thêm nhiều email một lần: 1 mới, 1 đã có tài khoản, 1 đã là thành viên, 1 sai định dạng, 1 trùng trong danh sách | FR-09, OQ-20 | Kết quả từng dòng `created`/`existing_added`/`already_exists`/`invalid`, email trùng chỉ xử lý một lần, email lỗi không ảnh hưởng email khác; mật khẩu tạm chỉ cho tài khoản mới, AuditLog không chứa mật khẩu | |
| TC-M3-03 | Thêm nhiều email với danh sách > 50 hoặc khi quỹ Archived | OQ-20, BR-37 | 400 / 422 `fund.archived` | `MembersApiTests.Bulk_add_rejects_empty_and_more_than_50…`, `…Add_to_an_archived_fund…`, e2e mock |
| TC-M3-04 | Thêm nhiều email: kết quả từng dòng, tài khoản mới đăng nhập được bằng mật khẩu tạm, rate limit | OQ-20 | Từng dòng đúng outcome; mật khẩu tạm chỉ cho tài khoản mới; 10 lần/phút → 429 | `MembersApiTests.Bulk_add_*`, e2e full `thêm nhiều email` |
| TC-M3-05 | Thủ quỹ thao tác trên thủ quỹ chính / thủ quỹ khác | AZ-02 | 403 (sửa, cấp mật khẩu, ngừng); chủ quỹ thì được; UI ẩn nút | `MembersServiceTests.Treasurer_cannot_touch…`, `MembersApiTests.Treasurer_cannot_touch…`, e2e mock |
| TC-M3-06 | Sửa tên / cấp mật khẩu cho tài khoản tự đăng ký hoặc đang ở quỹ khác; cho chính mình | AZ-07 | 422 `member.account_not_managed` / `member.cannot_act_on_self`; UI khóa nút | `MembersServiceTests.Cannot_*`, `MembersApiTests.Cannot_rename_or_reset…`, e2e full |
| TC-M3-07 | Cấp lại mật khẩu tạm | FR-10, BR-29 | Mật khẩu mới trả một lần, mật khẩu cũ và phiên cũ hết hiệu lực, buộc đổi; audit không có mật khẩu | `MembersApiTests.Reset_password_gives…`, e2e full `cấp lại mật khẩu tạm` |
| TC-M3-08 | Ngừng thành viên: học sinh còn khoản chưa đóng / còn số dư / còn thanh toán chờ / là chủ quỹ | OQ-15, BR-30 | Khoản `Unpaid` của khoản đang mở → `Cancelled`, dữ liệu cũ giữ nguyên; còn số dư hoặc thanh toán chờ → 422; chủ quỹ → 422; mất quyền truy cập quỹ (404) nhưng tài khoản còn; thêm lại → `Reactivated` | `MembersServiceTests.Remove_*`, `MembersApiTests.Remove_*`, e2e mock/full |
| TC-M3-09 | Sau đăng nhập luôn vào Quỹ của tôi (không menu), chọn quỹ mới có menu | điều hướng | `/funds` kể cả khi một quỹ; mật khẩu tạm → `/change-password` | e2e mock `F-01 Quỹ của tôi`, `auth.service.spec.ts` |
| TC-M4-01 | Thủ quỹ (không phải Owner) xem / phân quyền / thu hồi | FR-13, AZ-02 | 403 cho cả ba endpoint; UI ẩn mục Thủ quỹ và chặn đường dẫn trực tiếp (trang 403) | `TreasurersServiceTests.A_treasurer_cannot…`, `TreasurersApiTests.A_treasurer_gets_403…`, e2e mock/full |
| TC-M4-02 | Phân quyền một học sinh đang hoạt động | FR-12 | 201, vai trò → Treasurer, audit `treasurer.grant` (before/after); **hiệu lực ngay ở phiên đang mở**, không đăng nhập lại; thủ quỹ mới vẫn không có `Treasurer.Manage` | `TreasurersServiceTests.Grant_*`, `TreasurersApiTests.Grant_makes_a_student…`, e2e full |
| TC-M4-03 | Phân quyền người không thuộc quỹ / không tồn tại / đã bị ngừng / đã là thủ quỹ hoặc chủ quỹ / quỹ Archived / memberId rỗng | FR-12, BR-37 | 404 / 404 / 422 `treasurer.member_not_active` / 409 `treasurer.already_treasurer` / 422 `fund.archived` / 400 | `TreasurersApiTests.Grant_*` |
| TC-M4-04 | Thu hồi thủ quỹ | FR-12 | 200, vai trò → Student, audit `treasurer.revoke`, mất quyền ngay ở phiên đang mở, vẫn là thành viên | `TreasurersServiceTests.Revoke_*`, `TreasurersApiTests.Revoke_returns…`, e2e full |
| TC-M4-05 | Thu hồi thủ quỹ chính / học sinh | AZ-03 | 422 `treasurer.cannot_revoke_owner` (kể cả tự thu hồi) / 409 `treasurer.not_treasurer`; UI không có nút Thu hồi trên hàng chủ quỹ | `TreasurersApiTests.The_owner_cannot_be_revoked_AZ03`, e2e mock/full |
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
| TC-M12-01 | Học sinh xem audit | FR-47 | 403 (người ngoài quỹ 404, chưa đăng nhập 401); U-01 không hiện với học sinh | `AuditLogsApiTests.Permissions_owner_200_student_403_outsider_404_anonymous_401_TC_M12_01`, E2E `audit.spec.ts` |
| TC-M12-02 | Lọc nhật ký theo ngày (giờ VN, trọn ngày), người, phạm vi, sự kiện, nhóm hành động (`payment.`), chỉ tự ghi nhận; phân trang | FR-47 | Đúng bản ghi, mới nhất trước, `total` đúng | `AuditLogsServiceTests`, `AuditLogsApiTests.Filters_paging_names_and_masked_ip_on_postgres_FR47` |
| TC-M12-03 | Bộ lọc sai (`to < from`, `pageSize > 100`, `eventId` với phạm vi Chung, ngày sai định dạng) | FR-47 | 400 `validation_failed` | `AuditLogsApiTests.Invalid_filters_are_400` |
| TC-M12-04 | Mọi API ghi của M1–M5 sinh audit đúng quỹ / người; audit không chứa mật khẩu tạm hay hash | FR-45, M12-T3 | Đủ 11 hành động cấp quỹ + 2 cấp tài khoản | `AuditLogsApiTests.Every_write_api_of_merged_modules_records_audit_checklist` |
| TC-M12-05 | Xem trước / sau của một thao tác | FR-45, FR-46 | Hộp thoại hiện người, thời điểm, IP đã che, JSON trước / sau; tag "Tự ghi nhận" khi thủ quỹ tự thao tác cho mình | E2E `audit.spec.ts` |
| TC-M13-01 | Học sinh xem báo cáo học sinh khác | FR-51 | 403/404 | |
| TC-M14-01 | Quyết toán cuối năm khi còn sự kiện chưa quyết toán | BR-35 | 422 `year_end.open_events` | |
| TC-M14-02 | Quỹ chung dư 300.000đ, 30 thành viên đã đóng bằng nhau, cộng số dư (theo tỷ lệ – OQ-19) | BR-36 | Mỗi người +10.000đ, quỹ `Archived`, thông báo cả lớp (Quỹ chung) | |
| TC-M14-03 | Quyết toán cuối năm lần thứ hai | BR-34 | 422 `year_end.already_closed` | |
| TC-M14-04 | Tạo khoản thu sau khi quỹ Archived | BR-37 | 422 `fund.archived` | |
| TC-M14-05 | Quỹ Archived: học sinh có số dư gửi yêu cầu rút, thủ quỹ xác nhận đã trả | BR-37, OQ-11 | Thành công; số dư về 0; không bị chặn `fund.archived` | |
| TC-M2-01 | Tài khoản tự đăng ký tạo quỹ | FR-05 | 201, là Owner, có đủ danh mục mặc định, audit `fund.create` | `FundsApiTests.Self_registered_user_creates_fund_and_becomes_owner_with_default_categories_FR05` |
| TC-M2-02 | Tài khoản Provisioned (học sinh) tạo quỹ | FR-05 | 403 | `FundsApiTests.Provisioned_account_cannot_create_fund_403` |
| TC-M2-03 | Người ngoài quỹ xem quỹ | FR-07 | 404 (thành viên: 200) | `FundsApiTests.Outsider_viewing_fund_is_404_member_is_200_FR07` |
| TC-M2-04 | Học sinh sửa thông tin quỹ / sửa quỹ đã Archived | FR-06, BR-37 | 403 / 422 `fund.archived` | `FundsApiTests.Update_requires_fund_manage_FR06`, `FundsServiceTests.Archived_fund_cannot_be_updated_BR37` |
| TC-M2-05 | Danh sách quỹ: số dư quỹ chung không cộng tiền sự kiện; chỉ thủ quỹ thấy số thanh toán chờ | FR-08, BR-02 | Đúng số dư; `pendingPaymentCount` null với học sinh | `FundsServiceTests.List_mine_shows_role_members_general_balance_only_and_pending_for_confirmers` |
| TC-M5-01 | Upload ảnh đại diện PDF hoặc > 2 MB | BR-07 | 422 `file.invalid` | `CategoriesAndFilesApiTests.Avatar_upload_rules_and_visibility_TC_M5_01`, `AttachmentsServiceTests.Avatar_must_be_small_image_TC_M5_01` |
| TC-M5-02 | Học sinh tải bill chuyển khoản của học sinh khác | AZ-05 | 404 | `CategoriesAndFilesApiTests.Upload_and_download_follow_kind_permissions_and_AZ05_TC_M5_02` |
| TC-M5-03 | Upload file khai `application/pdf` nhưng nội dung không phải PDF; hóa đơn > 5 MB | BR-07 | 422 `file.invalid`, không lưu object | `CategoriesAndFilesApiTests.Wrong_type_or_oversized_file_is_422_file_invalid` |
| TC-M5-04 | Khóa lưu trữ không chứa tên file gốc | BR-07 | Khóa dạng `{kind}/{fundId}/{guid}.ext` | `AttachmentsServiceTests.Student_uploads_payment_proof_unattached_with_random_storage_key_BR07` |
| TC-M5-05 | Học sinh tải hóa đơn (Receipt) | FR-15 | 403 | `CategoriesAndFilesApiTests.Upload_and_download_follow_kind_permissions_and_AZ05_TC_M5_02` |
| TC-M5-06 | Học sinh xem chứng từ chi quỹ chung; chứng từ chi sự kiện khi chưa nhận tham gia | FR-17 | 200 / 404 | `AttachmentsServiceTests.Student_sees_general_expense_receipt_but_not_event_receipt_unless_participant_FR17` |
| TC-M5-07 | Gắn file đã gắn / sai loại / do người khác tải lên | FR-15 | 422 `attachment.unusable` | `AttachmentsServiceTests.Attach_rejects_reuse_wrong_kind_and_foreign_uploads` |
| TC-M5-08 | File tải lên quá 24h chưa gắn | TD-01 | Bị dọn khi có upload mới, không tải / gắn được nữa | `AttachmentsServiceTests.Unattached_files_older_than_24h_are_cleaned_on_next_upload_TD01` |
| TC-M5-09 | Thêm danh mục trùng tên (khác hoa thường) cùng loại; ngừng dùng danh mục đã có giao dịch | FR-14 | 409 `category.duplicate_name`; ngừng dùng thành công, không xóa | `CategoriesAndFilesApiTests.Categories_crud_with_permissions_FR14`, `CategoriesServiceTests` |
