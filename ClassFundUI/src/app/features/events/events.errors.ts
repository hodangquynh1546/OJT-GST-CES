import { registerErrorMessages } from '../../shared/utils/error-messages';

registerErrorMessages({
  'event.invalid_transition': 'Không thể chuyển trạng thái sự kiện theo cách này.',
  'event.not_open': 'Sự kiện không còn mở, không đổi được câu trả lời tham gia.',
  'event.has_money': 'Sự kiện đã có thu tiền hoặc ghi chi, không hủy được. Hãy đóng danh sách rồi quyết toán.',
  'event.list_not_closed': 'Chỉ ghi chi cho sự kiện sau khi đóng danh sách.',
  'event.list_deadline_not_reached': 'Chưa tới hạn đóng danh sách. Hãy xác nhận đóng sớm nếu bạn muốn đóng ngay.',
  'event.list_deadline_in_past': 'Hạn đóng danh sách không được trước ngày hôm nay.',
  'event.response_locked': 'Bạn đã nộp tiền cho sự kiện này nên không chuyển sang không tham gia được. Liên hệ thủ quỹ.',
  'event.not_editable': 'Sự kiện đã quyết toán hoặc đã hủy, không sửa được.',
  'event.invalid_response': 'Chỉ chọn Tham gia hoặc Không tham gia.',
  'settlement.event_not_completed': 'Chỉ quyết toán được sự kiện đã đóng danh sách.',
  'settlement.already_settled': 'Sự kiện này đã được quyết toán.',
  'settlement.method_required': 'Vui lòng chọn cách xử lý tiền thừa: hoàn trực tiếp hoặc cộng vào số dư.',
  'settlement.line_processed': 'Khoản hoàn này đã được xử lý.',
  'settlement.no_contributors': 'Không có ai đã đóng tiền nên không chia được tiền thừa.',
  'fund.archived': 'Quỹ đã tổng kết cuối năm, chỉ được xem.',
});
