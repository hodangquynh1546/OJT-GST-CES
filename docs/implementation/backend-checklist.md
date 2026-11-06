# Backend Checklist

## Hiện hành (mã nguồn làm lại – cập nhật 2026-11-06)

Trạng thái từng feature: [implementation-status.md](implementation-status.md) mục 1a. Test integration nằm trong `tests/SchoolFund.IntegrationTests`, unit trong `tests/SchoolFund.UnitTests`.

| Yêu cầu kiểm thử | Test | Kết quả |
|---|---|---|
| Build toàn solution | `dotnet build` | ✅ 0 warning, 0 error (2026-11-06) |
| Unit / Integration (PostgreSQL thật) | `dotnet test` | ✅ 289 / 289 · 82 / 82 (2026-11-06, Tổng quan, Báo cáo, Thành viên) |
| **Auth** – Đăng ký giáo viên (luôn `Teacher`, bỏ qua vai trò gửi lên) | `AuthTests.Teacher_registers_and_is_signed_in_as_teacher` | ✅ |
| Đăng ký: xác nhận sai 400 / mật khẩu yếu 422 / email trùng (khác hoa thường) 422 (T-01) | `AuthTests.Register_rejects_mismatched_confirmation_weak_password_and_taken_email` | ✅ |
| Đăng nhập bằng email, không tham số vai trò (G-05) | `AuthTests.Teacher_signs_in_with_email_case_insensitively_without_role_parameter` | ✅ |
| Đăng nhập bằng username, `mustChangePassword` (SES-01) | `AuthTests.Student_signs_in_with_username_and_must_change_temporary_password` | ✅ |
| Sai thông tin 401 chung; khóa → `auth.account_disabled` chỉ khi đúng mật khẩu (BR-26) | `AuthTests.Login_failures_return_401_with_specific_codes` | ✅ |
| Còn mật khẩu tạm → 403 `password.change_required`; được gọi me / change-password / logout; đổi xong dùng được (SES-02 → SES-04) | `AuthTests.Temporary_password_blocks_other_endpoints_until_changed` | ✅ |
| Đổi mật khẩu / khóa tài khoản → phiên khác 401 (SES-05, G-03) | `AuthTests.Changing_password_ends_every_other_session`, `…Disabling_account_ends_current_session` | ✅ |
| Log và AuditLog không chứa mật khẩu, hash, cookie, token, pepper (SES-08, NFR-10) | `AuthTests.Passwords_hashes_and_cookies_never_reach_logs_or_audit_log` | ✅ |
| `/auth/me` trả vai trò theo quỹ (GVCN, thủ quỹ, phụ huynh + tên con) | `AuthTests.Me_lists_memberships_with_roles_derived_from_data` | ✅ |
| Antiforgery, rate limit, logout, sửa họ tên | `AuthTests.Write_requests_require_antiforgery_token`, `…Login_is_rate_limited_per_client`, `…Logout_clears_session`, `…Update_me_trims_full_name_and_validates` | ✅ |
| Pepper `SECRET_KEY`: hash cũ nâng cấp; thiếu khóa không khởi động | `AuthTests.Password_created_before_pepper_still_signs_in_and_is_rehashed_with_pepper`, `…Api_refuses_to_start_without_a_valid_secret_key` | ✅ |
| **Funds** – Giáo viên tạo quỹ kèm 6 danh mục mặc định, chưa có thủ quỹ (FR-23) | `FundTests.Teacher_creates_fund_with_six_default_categories_and_no_treasurer` | ✅ |
| Tên quỹ 1–100 ký tự | `FundTests.Create_fund_validates_name_length_after_trimming` | ✅ |
| Chỉ giáo viên tạo quỹ (403) | `FundTests.Only_teachers_can_create_funds` | ✅ |
| GVCN đổi tên quỹ + AuditLog `Fund.Rename` (FR-11) | `FundTests.Homeroom_teacher_renames_fund_and_change_is_audited` | ✅ |
| Không phải GVCN đổi tên → 403; không thuộc quỹ → 404 | `FundTests.Non_homeroom_accounts_cannot_rename_fund` | ✅ |
| Quỹ của tôi: số dư, thủ quỹ, trường theo vai trò (FR-22, BR-02, BR-23) | `FundTests.List_returns_balance_treasurers_and_role_specific_fields`, `…List_contains_only_funds_of_current_account` | ✅ |
| `GET /funds` cần đăng nhập, không còn mật khẩu tạm | `FundTests.List_requires_sign_in_and_changed_password` | ✅ |
| **Danh mục** (Dev B) | `CategoryTests`, `CategoryServiceTests` | ✅ |
| **Thu chi** (Dev B) | `TransactionTests`, `TransactionServiceTests`, `TransactionsApiTests`, `FundTransactionTests` | ✅ |
| **Học sinh** – Thêm, danh sách, chi tiết, đổi tên; validate (FR-24, S-01) | `StudentTests.Teacher_adds_lists_views_and_renames_students` | ✅ |
| Cấp tài khoản: gợi ý `ten.ho.NNN`, mật khẩu tạm `Xxxx-xxxx-0000-xxxx`, `PendingActivation` → `Active`, cấp lần 2 → 422 (FR-25, S-02) | `StudentTests.Issuing_account_suggests_username_and_returns_one_time_temporary_password`, `CredentialsTests` | ✅ |
| Tên đăng nhập tự chọn: định dạng 422 `user.username`, trùng 422 `user.username_taken` | `StudentTests.Issuing_account_with_chosen_username_validates_format_and_uniqueness` | ✅ |
| Cấp lại mật khẩu tạm → phiên cũ 401, phải đổi lại (BR-35, S-06) | `StudentTests.Resetting_password_ends_every_session_and_requires_a_new_change` | ✅ |
| Liên kết phụ huynh mới: tạo tài khoản + mật khẩu tạm, `emailSent = false`; liên kết thứ hai 422 (FR-26, P-01, P-03) | `StudentTests.Linking_new_parent_email_creates_parent_account_with_one_time_password` | ✅ |
| Liên kết phụ huynh đã có (khác hoa thường, quỹ khác): chỉ liên kết, 1 tài khoản, 2 quỹ (P-02, P-04) | `StudentTests.Linking_existing_parent_email_only_links_without_new_account` | ✅ |
| Email giáo viên / học sinh → 422 `parent.email_in_use`; thêm học sinh kèm email sai không lưu nửa vời | `StudentTests.Teacher_or_student_email_cannot_be_linked_as_parent` | ✅ |
| Thêm học sinh kèm email phụ huynh trong một bước (P-13) | `StudentTests.Adding_student_with_parent_email_links_parent_in_one_step` | ✅ |
| Gỡ liên kết: đánh dấu, giữ lịch sử, phụ huynh 404 ngay, liên kết lại không cấp mật khẩu (BR-33, P-05) | `StudentTests.Unlinking_parent_marks_link_removed_keeps_history_and_account` | ✅ |
| Chỉ GVCN quản lý học sinh (403 / 404); danh sách có thủ quỹ + phụ huynh (S-09) | `StudentTests.Only_homeroom_teacher_manages_students`, `…List_shows_treasurer_flag_and_parent_of_each_student` | ✅ |
| Mật khẩu tạm không vào AuditLog / log (S-05, NFR-10) | `StudentTests.Temporary_passwords_never_reach_audit_log_or_logs` | ✅ |
| Không xóa cứng Student / ParentLink (TR_NoDelete, M-02) | `StudentTests.Students_and_parent_links_cannot_be_hard_deleted` | ✅ |
| **Báo cáo** – Đầu tháng sau = cuối tháng trước, kể cả sau khi sửa / hủy khoản cũ (BR-10) | `ReportTests.Next_opening_balance_equals_previous_closing_even_after_editing_and_cancelling`, `ReportServiceTests.Opening_balance_equals_closing_balance_of_the_previous_month_across_a_year_end` | ✅ |
| Số dư không lệch khi hủy khoản cũ (BR-02) | `ReportServiceTests.Cancelling_an_old_transaction_moves_every_later_month_and_keeps_the_chain` | ✅ |
| Dòng "Khoản khác" cho học sinh, phụ huynh; tổng các dòng khớp số tổng; không lộ chi tiết (P1, BR-15, NFR-08) | `ReportTests.Parent_and_student_get_other_lines_that_add_up_without_leaking_details` | ✅ |
| Thống kê 12 tháng nối tiếp, chỉ số tổng (FR-09) | `ReportTests.Yearly_report_chains_twelve_months_and_contains_only_totals` | ✅ |
| **Tổng quan** – số liệu, tháng hiện tại, người quản lý, 5 khoản gần nhất theo phạm vi (FR-01) | `ReportTests.Overview_returns_totals_current_month_managers_and_scoped_recent_transactions` | ✅ |
| **Thành viên** – chỉ họ tên + vai trò; học sinh, phụ huynh 403 (FR-12) | `ReportTests.Members_returns_only_names_and_roles_to_teacher_and_treasurer` | ✅ |
| Ngoài quỹ 404, ẩn danh 401, kỳ không hợp lệ 400 | `ReportTests.Outsider_gets_404_and_anonymous_gets_401`, `…Invalid_period_returns_400` | ✅ |

## Historical

> **Historical** – checklist của mã nguồn R1 / R3 đã xóa ngày 2026-11-05; ✅ dưới đây là kết quả cũ, không phản ánh mã nguồn hiện tại.

> Cập nhật 2026-11-05 (R3). Mục cũ (R1) giữ nguyên tên test; test đã chuyển sang đường dẫn `/funds/{fundId}` và luồng thủ quỹ là học sinh được phân công.

| Yêu cầu kiểm thử (task mục 9) | Test |
|---|---|
| Build toàn solution | ✅ 0 warning, 0 error |
| Unit test | ✅ 57 / 57 (`FundDomainTests` + permission / tài khoản / phân công, `CredentialsTests`, kiến trúc, hạ tầng) |
| Integration test PostgreSQL thật | ✅ 67 / 67 |
| API ghi nhận khoản thu / chi | `TransactionTests.Adding_income_and_expense_updates_the_balance_immediately` |
| Cập nhật và hủy giao dịch | `TransactionTests.Editing_…`, `TransactionTests.Cancelling_…` |
| Số dư sau từng thao tác | các test trên + `Expense_larger_than_the_balance_…` |
| Báo cáo tháng | `ReportAndCategoryTests.Closing_balance_of_a_month_carries_over_…` |
| Số dư chuyển tiếp giữa hai tháng | cùng test trên + `Yearly_statistics_chain_month_balances` |
| Dữ liệu không hợp lệ | `TransactionTests.Invalid_input_is_rejected_with_clear_errors` |
| Thao tác lưu lặp | `TransactionTests.Pressing_save_several_times_never_creates_duplicates` (tuần tự + 5 request đồng thời) |
| Cô lập dữ liệu giữa hai quỹ | `TransactionTests.A_treasurer_cannot_see_or_change_another_class_fund` |
| Danh mục | `ReportAndCategoryTests.Categories_can_be_added_renamed_and_hidden` |
| Đăng nhập / phiên / khóa tài khoản | `ReportAndCategoryTests.Treasurer_signs_in_and_sessions_are_protected` |
| Bảo vệ DB | `ProtectionTriggerTests`, `DatabaseAccountTests` |
| **R3** – Giáo viên đăng ký, nhiều quỹ, không tự chọn vai trò; GVCN không ghi thu chi / danh mục | `Roles.TeacherStudentTreasurerTests.Teacher_registers_creates_several_funds_and_cannot_choose_a_role` |
| Giáo viên không truy cập quỹ của giáo viên khác (404, kể cả Id sai định dạng) | `…Teacher_cannot_see_or_manage_another_teachers_fund` |
| Tài khoản học sinh: mật khẩu tạm một lần, bắt buộc đổi, chưa kích hoạt không phân công được, không lộ trong AuditLog / log, cấp lại → phiên cũ hết hiệu lực | `…Student_account_uses_a_one_time_temporary_password_and_must_change_it` |
| Học sinh chưa phân công chỉ dữ liệu công khai, không thao tác quản lý | `…Unassigned_student_sees_public_data_only_and_cannot_manage_anything` |
| Thủ quỹ: thu chi + danh mục, chỉ xem thành viên; GVCN xem đầy đủ + lịch sử | `…Treasurer_manages_transactions_and_categories_but_only_views_members` |
| Đổi thủ quỹ (phân công trước, thu hồi sau), không thu hồi thủ quỹ cuối, không phân công học sinh quỹ khác, lịch sử + AuditLog | `…Changing_treasurer_requires_assigning_the_new_one_before_revoking_the_old_one` |
| Thu hồi đồng thời không để quỹ mất thủ quỹ | `…Concurrent_revokes_never_leave_a_fund_without_a_treasurer` |
| Phụ huynh: một tài khoản / email, liên kết nhiều quỹ, email giáo viên bị từ chối, gỡ liên kết | `Roles.ParentAndPrivacyTests.Parent_account_is_created_once_per_email_and_linked_to_children_in_several_funds` |
| Phụ huynh chỉ công khai; không ghi chú / lý do / người thực hiện; "Khoản khác"; con là thủ quỹ nhưng phụ huynh không có quyền | `…Parent_sees_public_data_only_and_never_notes_reasons_or_actors` |
| Hủy bắt buộc lý do, lưu người hủy | `…Cancelling_requires_a_reason_and_records_who_cancelled` |
| Migration bổ sung trên DB có dữ liệu cũ | `Database.UpgradeMigrationTests` |
| Trigger NoDelete cho Student / ParentLink / TreasurerAssignment | `ProtectionTriggerTests` (thêm 3 trường hợp) |
| Regression R1 (thu chi, báo cáo, chống trùng, lọc, cô lập quỹ, phiên) | `TransactionTests`, `ReportAndCategoryTests` (giữ nguyên kịch bản) |
