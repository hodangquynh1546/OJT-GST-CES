export type LedgerType = 'Income' | 'Expense';

export const LEDGER_TYPE_LABEL: Record<LedgerType, string> = { Income: 'Thu', Expense: 'Chi' };

/** Khớp `CategoryDto`. */
export interface Category {
  id: string;
  name: string;
  type: LedgerType;
  isSystem: boolean;
  isActive: boolean;
  usageCount: number;
}

export interface CreateCategoryBody {
  name: string;
  type: LedgerType;
}

export interface UpdateCategoryBody {
  name: string;
  isActive: boolean;
}

export const CATEGORY_NAME_MAX = 100;

/** Bỏ khoảng trắng thừa (khớp `CategoriesService.Normalize`). */
export function normalizeCategoryName(name: string): string {
  return name.trim().split(/\s+/).filter(Boolean).join(' ');
}

/** Dòng phụ dưới tên: "Mặc định · 3 giao dịch". */
export function categoryMeta(category: Category): string {
  return `${category.isSystem ? 'Mặc định · ' : ''}${category.usageCount} giao dịch`;
}
