/** Loại giao dịch / danh mục (backend-api.md mục Categories; BR-08). */
export type CategoryType = 'Income' | 'Expense';

/**
 * Một mục của `GET /funds/{fundId}/categories` – khớp `CategoryResponse` (Contracts/snapshots/Categories.json).
 * Bản GVCN / thủ quỹ có `isHidden` và `transactionCount` (tính cả khoản đã hủy). Bản công khai (học sinh, phụ huynh – X-01)
 * không có hai trường này: chỉ danh mục đang hiện có ít nhất một khoản công khai.
 */
export interface Category {
  id: string;
  type: CategoryType;
  name: string;
  isHidden?: boolean;
  transactionCount?: number;
}

/** Tham số của `GET /funds/{fundId}/categories`. `includeHidden` chỉ có tác dụng với GVCN / thủ quỹ. */
export interface CategoryListQuery {
  type?: CategoryType;
  includeHidden?: boolean;
}

/** Body của `POST /funds/{fundId}/categories` (`Category.Manage`). */
export interface CreateCategoryRequest {
  type: CategoryType;
  name: string;
}

/** Body của `PUT /funds/{fundId}/categories/{id}`: cần đủ cả hai trường. */
export interface UpdateCategoryRequest {
  name: string;
  isHidden: boolean;
}

/** Response 201 của `POST /categories`. */
export interface CategoryCreated {
  id: string;
}

/** Tên danh mục 1–100 ký tự sau khi cắt khoảng trắng (`Category.Name` varchar(100)). */
export const CATEGORY_NAME_MAX_LENGTH = 100;
