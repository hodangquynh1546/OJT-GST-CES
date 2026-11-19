import { registerErrorMessages } from '../../shared/utils/error-messages';

registerErrorMessages({
  'event.invalid_transition': 'Không thể chuyển trạng thái sự kiện theo cách này.',
  'event.not_open': 'Sự kiện không còn mở, không đổi được câu trả lời tham gia.',
  'event.has_money': 'Sự kiện đã có thu tiền hoặc ghi chi, không hủy được. Hãy kết thúc rồi quyết toán.',
  'event.response_locked': 'Bạn đã nộp tiền cho sự kiện này nên không chuyển sang không tham gia được. Liên hệ thủ quỹ.',
  'event.not_editable': 'Sự kiện đã quyết toán hoặc đã hủy, không sửa được.',
  'event.invalid_response': 'Chỉ chọn Tham gia hoặc Không tham gia.',
  'fund.archived': 'Quỹ đã tổng kết cuối năm, chỉ được xem.',
});
