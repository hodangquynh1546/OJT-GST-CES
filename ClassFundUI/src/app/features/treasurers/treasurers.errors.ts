import { registerErrorMessages } from '../../shared/utils/error-messages';

/** Thông báo cho mã lỗi của module Thủ quỹ (khớp `TreasurersErrors` ở backend). */
registerErrorMessages({
  'treasurer.already_treasurer': 'Người này đã là thủ quỹ của quỹ.',
  'treasurer.not_treasurer': 'Người này không phải thủ quỹ được phân quyền.',
  'treasurer.cannot_revoke_owner': 'Không thể thu hồi quyền của thủ quỹ chính.',
  'treasurer.member_not_active': 'Thành viên này đã bị ngừng. Hãy thêm lại vào quỹ trước khi phân quyền.',
  'fund.archived': 'Quỹ đã tổng kết cuối năm, chỉ được xem.',
});
