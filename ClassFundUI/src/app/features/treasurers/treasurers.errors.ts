import { registerErrorMessages } from '../../shared/utils/error-messages';

/** Thông báo cho mã lỗi của M4 (screen-specifications.md mục 5). Import trong treasurers.routes.ts. */
export const TREASURER_ERROR_MESSAGES = {
  'treasurer.no_account': 'Học sinh chưa có tài khoản. Cấp tài khoản ở trang Học sinh trước.',
  'treasurer.account_not_activated': 'Học sinh chưa kích hoạt tài khoản (chưa đổi mật khẩu tạm).',
  'treasurer.account_disabled': 'Tài khoản của học sinh đang bị khóa.',
  'treasurer.already_assigned': 'Học sinh này đang là thủ quỹ.',
  'treasurer.last': 'Không thể thu hồi thủ quỹ cuối cùng. Hãy phân công thủ quỹ mới trước rồi mới thu hồi người cũ.',
  'treasurer.already_revoked': 'Phân công này đã được thu hồi trước đó.',
} as const;

registerErrorMessages(TREASURER_ERROR_MESSAGES);
