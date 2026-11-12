import { registerErrorMessages } from '../../shared/utils/error-messages';

/** Thông báo cho mã lỗi của M5 Danh mục (screen-specifications.md mục 5). Import trong categories.routes.ts. */
export const CATEGORY_ERROR_MESSAGES = {
  'category.name_taken': 'Tên danh mục đã tồn tại trong loại này.',
} as const;

registerErrorMessages(CATEGORY_ERROR_MESSAGES);
