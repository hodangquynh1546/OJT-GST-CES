import { registerErrorMessages } from '../../shared/utils/error-messages';

registerErrorMessages({
  'withdrawal.nothing_to_withdraw': 'Số dư bằng 0, không có tiền để rút.',
  'withdrawal.pending_exists': 'Bạn đã có yêu cầu rút tiền đang chờ thủ quỹ xử lý.',
  'withdrawal.not_pending': 'Yêu cầu rút này đã được xử lý.',
  'withdrawal.reason_required': 'Vui lòng nhập lý do từ chối.',
  'balance.insufficient': 'Số dư cá nhân không đủ.',
  'balance.locked_by_withdrawal': 'Số dư đang chờ rút, không thể dùng để đóng khoản thu.',
});
