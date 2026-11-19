import { registerErrorMessages } from '../../shared/utils/error-messages';

/** Thông báo cho mã lỗi của module Khoản thu & Thanh toán (khớp `ChargesErrors`, `PaymentsErrors` ở backend). */
registerErrorMessages({
  'charge.due_date_in_past': 'Hạn đóng không được trước ngày hôm nay.',
  'charge.invalid_status': 'Khoản thu không còn ở trạng thái phù hợp để thực hiện thao tác này.',
  'charge.has_payments': 'Khoản thu đã có người nộp tiền nên không thể hủy. Hãy đóng khoản thu thay vì hủy.',
  'charge.nothing_to_remind': 'Không còn ai chưa đóng để nhắc.',
  'charge.remind_too_soon': 'Vừa nhắc xong. Vui lòng chờ ít nhất 1 giờ rồi nhắc lại.',
  'money.invalid': 'Số tiền phải lớn hơn 0.',
  'payment.proof_required': 'Chuyển khoản cần kèm ảnh minh chứng.',
  'payment.method_unavailable': 'Chưa hỗ trợ đóng bằng số dư cá nhân.',
  'payment.not_payable': 'Khoản này đã nộp hoặc đang chờ thủ quỹ xác nhận.',
  'payment.charge_not_open': 'Khoản thu đã đóng, không nhận thêm tiền.',
  'payment.already_processed': 'Thanh toán này đã được xử lý.',
  'payment.reason_required': 'Vui lòng nhập lý do từ chối.',
  'attachment.unusable': 'Ảnh minh chứng không dùng được. Vui lòng tải lại ảnh.',
  'fund.archived': 'Quỹ đã tổng kết cuối năm, chỉ được xem.',
});

/** Mã lỗi nghiệp vụ → ô nhập nhận lỗi (dùng với `applyServerErrors`). */
export const CHARGE_FIELD_FOR: Readonly<Record<string, string>> = {
  'charge.due_date_in_past': 'dueDate',
  'money.invalid': 'amountPerPerson',
};
