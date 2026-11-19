import { registerErrorMessages } from '../../shared/utils/error-messages';

registerErrorMessages({
  'ledger.exceeds_balance': 'Số tiền chi vượt số dư hiện có. Tổng chi không được lớn hơn tổng thu – tạo khoản thu trước.',
  'ledger.future_date': 'Ngày giao dịch không được ở tương lai.',
  'ledger.event_closed': 'Sự kiện đã kết thúc hoặc đã quyết toán / hủy, không ghi chi mới.',
  'ledger.scope_locked': 'Sự kiện đã quyết toán hoặc đã hủy, sổ thu chi của sự kiện không thay đổi được nữa.',
  'ledger.already_reversed': 'Giao dịch này đã được đảo.',
  'ledger.not_reversible': 'Chỉ đảo được khoản chi ghi tay chưa bị đảo.',
  'ledger.reason_required': 'Vui lòng nhập lý do đảo.',
  'evidence.required': 'Khoản chi trên 500.000 đ bắt buộc có chứng từ.',
  'money.invalid': 'Số tiền phải lớn hơn 0.',
  'category.unusable': 'Danh mục không tồn tại, đã ngừng dùng hoặc không đúng loại thu/chi.',
  'attachment.unusable': 'Chứng từ không dùng được (đã gắn vào giao dịch khác hoặc không phải bạn tải lên). Hãy tải lại.',
  'fund.archived': 'Quỹ đã tổng kết cuối năm, chỉ được xem.',
});
