# Backend Checklist

> **Historical** – checklist của mã nguồn R1 / R3 đã xóa ngày 2026-11-05. Trạng thái hiện tại: **Not Implemented** ([implementation-status.md](implementation-status.md)). Khi triển khai lại, dùng [test-cases.md](../testing/test-cases.md) mục A làm checklist; ✅ dưới đây là kết quả cũ.

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
