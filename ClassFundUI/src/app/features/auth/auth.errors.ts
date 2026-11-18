import { registerErrorMessages } from '../../shared/utils/error-messages';

/** Thông báo cho mã lỗi của module Auth (khớp `AuthErrors` ở backend). */
registerErrorMessages({
  'auth.invalid_credentials': 'Email hoặc mật khẩu không đúng.',
  'auth.account_disabled': 'Tài khoản đã bị khóa. Vui lòng liên hệ thủ quỹ.',
  'auth.email_exists': 'Email này đã được đăng ký.',
  'auth.current_password_incorrect': 'Mật khẩu hiện tại không đúng.',
  'auth.password_unchanged': 'Mật khẩu mới phải khác mật khẩu hiện tại.',
  'auth.password_policy': 'Mật khẩu cần 8–128 ký tự, có cả chữ và số.',
  'auth.invalid_email': 'Email không hợp lệ.',
  'auth.invalid_name': 'Họ tên không được để trống và tối đa 100 ký tự.',
});

/** Mã lỗi nghiệp vụ → ô nhập nhận lỗi (dùng với `applyServerErrors`). */
export const AUTH_FIELD_FOR: Readonly<Record<string, string>> = {
  'auth.email_exists': 'email',
  'auth.invalid_email': 'email',
  'auth.invalid_name': 'fullName',
  'auth.password_policy': 'newPassword',
  'auth.current_password_incorrect': 'currentPassword',
  'auth.password_unchanged': 'newPassword',
};
