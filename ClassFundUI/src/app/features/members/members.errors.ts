import { registerErrorMessages } from '../../shared/utils/error-messages';

/** Thông báo cho mã lỗi của module Học sinh (khớp `MembersErrors` ở backend). */
registerErrorMessages({
  'member.already_exists': 'Học sinh này đã ở trong quỹ.',
  'member.invalid_email': 'Email không hợp lệ.',
  'member.invalid_name': 'Họ tên không được để trống và tối đa 100 ký tự.',
  'member.too_many_emails': 'Mỗi lần chỉ thêm tối đa 50 email.',
  'member.no_emails': 'Vui lòng nhập ít nhất một email.',
  'member.account_not_managed':
    'Tài khoản này do chính người dùng quản lý (tự đăng ký hoặc đang ở quỹ khác). Họ tự đổi họ tên và mật khẩu ở trang Tài khoản.',
  'member.cannot_act_on_self': 'Bạn không thể thao tác này trên chính mình ở đây. Hãy dùng trang Tài khoản.',
  'member.cannot_remove_owner': 'Không thể ngừng thủ quỹ chính của quỹ.',
  'member.already_removed': 'Thành viên này đã bị ngừng.',
  'member.has_balance': 'Thành viên còn số dư cá nhân. Số dư phải về 0 (rút tiền) trước khi ngừng.',
  'member.pending_payments': 'Thành viên còn thanh toán đang chờ xác nhận. Hãy xác nhận hoặc từ chối trước khi ngừng.',
  'fund.archived': 'Quỹ đã tổng kết cuối năm, chỉ được xem.',
});

/** Mã lỗi nghiệp vụ → ô nhập nhận lỗi (dùng với `applyServerErrors`). */
export const MEMBER_FIELD_FOR: Readonly<Record<string, string>> = {
  'member.already_exists': 'email',
  'member.invalid_email': 'email',
  'member.invalid_name': 'fullName',
};
