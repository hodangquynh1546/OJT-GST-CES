import { registerErrorMessages } from '../../shared/utils/error-messages';

/** Thông báo cho mã lỗi của M6 (screen-specifications.md mục 5, dòng T-01). Import trong transactions.routes.ts. */
export const TRANSACTION_ERROR_MESSAGES = {
  'transaction.future_date': 'Ngày giao dịch không được ở tương lai.',
  'transaction.category_invalid': 'Danh mục không hợp lệ (khác loại, đã ẩn hoặc không thuộc quỹ). Chọn danh mục khác.',
  'transaction.cancelled': 'Khoản này đã bị hủy, không sửa hoặc hủy lại được.',
  'transaction.cancel_reason_required': 'Nhập lý do hủy.',
} as const;

registerErrorMessages(TRANSACTION_ERROR_MESSAGES);

/** Lỗi 422 gắn vào ô tương ứng của T-01. */
export const FIELD_OF_CODE: Readonly<Record<string, string>> = {
  'transaction.future_date': 'occurredOn',
  'transaction.category_invalid': 'categoryId',
  'transaction.cancel_reason_required': 'reason',
};
