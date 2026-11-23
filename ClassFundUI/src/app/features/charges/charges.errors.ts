import { registerErrorMessages } from '../../shared/utils/error-messages';

/** Thông báo cho mã lỗi của module Khoản thu & Thu tiền (khớp `ChargesErrors`, `PaymentsErrors` ở backend). */
registerErrorMessages({
  'charge.due_date_in_past': 'Hạn đóng không được trước ngày hôm nay.',
  'charge.invalid_status': 'Khoản thu không còn ở trạng thái phù hợp để thực hiện thao tác này.',
  'charge.has_payments': 'Khoản thu đã có người nộp tiền nên không thể hủy. Hãy đóng khoản thu thay vì hủy.',
  'charge.nothing_to_remind': 'Không còn ai chưa nộp để nhắc.',
  'charge.remind_too_soon': 'Vừa nhắc xong. Vui lòng chờ ít nhất 1 giờ rồi nhắc lại.',
  'category.unusable': 'Danh mục không tồn tại, đã ngừng dùng hoặc không phải danh mục thu. Vui lòng chọn lại.',
  'money.invalid': 'Số tiền phải lớn hơn 0.',
  'payment.method_invalid': 'Thủ quỹ chỉ ghi nhận Tiền mặt hoặc Chuyển khoản; số dư cá nhân không phải phương thức thanh toán.',
  'payment.not_payable': 'Khoản này đã được ghi nhận là đã nộp.',
  'payment.not_collected': 'Khoản này chưa được ghi nhận đã thu nên không có gì để bỏ.',
  'payment.charge_not_open': 'Khoản thu đã đóng, không ghi nhận thêm.',
  'payment.reason_required': 'Vui lòng nhập lý do.',
  'attachment.unusable': 'Ảnh minh chứng không dùng được. Vui lòng tải lại ảnh.',
  'fund.archived': 'Quỹ đã tổng kết cuối năm, chỉ được xem.',
});

/** Mã lỗi nghiệp vụ → ô nhập nhận lỗi (dùng với `applyServerErrors`). */
export const CHARGE_FIELD_FOR: Readonly<Record<string, string>> = {
  'charge.due_date_in_past': 'dueDate',
  'money.invalid': 'amountPerPerson',
  'category.unusable': 'categoryId',
};
