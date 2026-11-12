import { CATEGORY_NAME_MAX_LENGTH, Category, CategoryType } from './categories.models';

export const CATEGORY_TYPE_LABEL: Readonly<Record<CategoryType, string>> = { Income: 'thu', Expense: 'chi' };

export function categoryNameTaken(type: CategoryType, name: string): string {
  return `Đã có danh mục ${CATEGORY_TYPE_LABEL[type]} tên “${name.trim()}”.`;
}

/**
 * Lỗi tên danh mục (BR-08), đúng thông báo trên Canvas; null nếu hợp lệ. `categories` phải gồm cả danh mục đã ẩn:
 * tên trùng với danh mục ẩn vẫn bị từ chối. Không phân biệt hoa thường, so sánh trong cùng loại; `exceptId` là chính nó khi đổi tên.
 */
export function categoryNameError(
  name: string,
  type: CategoryType,
  categories: readonly Category[],
  exceptId?: string,
): string | null {
  const trimmed = name.trim();
  if (!trimmed) {
    return 'Nhập tên danh mục.';
  }
  if (trimmed.length > CATEGORY_NAME_MAX_LENGTH) {
    return `Tên danh mục tối đa ${CATEGORY_NAME_MAX_LENGTH} ký tự.`;
  }
  const lowered = trimmed.toLocaleLowerCase('vi');
  const taken = categories.some(
    (category) =>
      category.type === type && category.id !== exceptId && category.name.toLocaleLowerCase('vi') === lowered,
  );
  return taken ? categoryNameTaken(type, trimmed) : null;
}
