import { registerErrorMessages } from '../../shared/utils/error-messages';

/** Thông báo cho mã lỗi của M1 (screen-specifications.md mục mã lỗi). Import trong auth.routes.ts / account.routes.ts. */
export const AUTH_ERROR_MESSAGES = {
  'auth.invalid_credentials': 'Email / tên đăng nhập hoặc mật khẩu không đúng.',
  'auth.account_disabled': 'Tài khoản đã bị khóa – liên hệ giáo viên chủ nhiệm.',
  'auth.password_policy': 'Mật khẩu cần 8–128 ký tự, có cả chữ và số.',
  'auth.password_reused': 'Mật khẩu mới phải khác mật khẩu hiện tại.',
  'auth.wrong_password': 'Mật khẩu hiện tại không đúng.',
  'user.email_taken': 'Email này đã được dùng cho một tài khoản khác.',
} as const;

registerErrorMessages(AUTH_ERROR_MESSAGES);

/** Mật khẩu 8–128 ký tự, có chữ và số (BR-28) – kiểm tra sớm ở client, backend vẫn kiểm lại. */
export const PASSWORD_PATTERN = /^(?=.*\p{L})(?=.*\d).{8,128}$/u;
