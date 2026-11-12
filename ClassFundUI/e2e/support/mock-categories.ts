import { Page } from '@playwright/test';
import { FUND } from './mock-session';

/**
 * Dữ liệu giả cho `GET /funds/{id}/categories` (hợp đồng: docs/api/backend-api.md – Categories; Canvas T-02).
 * Hai phạm vi: GVCN / thủ quỹ thấy `isHidden` + `transactionCount`; học sinh / phụ huynh chỉ danh mục đang hiện có
 * khoản công khai (X-01) và không có hai trường đó.
 */
export type MockCategoryType = 'Income' | 'Expense';

export interface MockCategory {
  id: string;
  type: MockCategoryType;
  name: string;
  isHidden?: boolean;
  transactionCount?: number;
}

const category = (n: number, type: MockCategoryType, name: string, transactionCount: number, isHidden = false): MockCategory => ({
  id: `019a0000-0000-7000-8000-0000000002${String(n).padStart(2, '0')}`,
  type,
  name,
  isHidden,
  transactionCount,
});

export const CATEGORY_DONG_QUY = category(1, 'Income', 'Đóng quỹ', 4);
export const CATEGORY_THU_KHAC = category(2, 'Income', 'Thu khác', 1);
export const CATEGORY_VAN_PHONG_PHAM = category(3, 'Expense', 'Văn phòng phẩm', 3);
export const CATEGORY_SINH_HOAT = category(4, 'Expense', 'Sinh hoạt, liên hoan', 3);
export const CATEGORY_KHEN_THUONG = category(5, 'Expense', 'Khen thưởng', 2);
export const CATEGORY_CHI_KHAC = category(6, 'Expense', 'Chi khác', 2);
/** Danh mục đã ẩn nhưng còn khoản cũ: chỉ hiện khi `includeHidden=true` (BR-08). */
export const CATEGORY_HIDDEN = category(7, 'Expense', 'Photo tài liệu', 1, true);

/** Bản của thủ quỹ khi bật "Hiện danh mục ẩn": 7 danh mục, 1 đã ẩn. */
export const CATEGORIES_FULL: MockCategory[] = [
  CATEGORY_DONG_QUY,
  CATEGORY_THU_KHAC,
  CATEGORY_VAN_PHONG_PHAM,
  CATEGORY_SINH_HOAT,
  CATEGORY_KHEN_THUONG,
  CATEGORY_CHI_KHAC,
  CATEGORY_HIDDEN,
];

/** Bản của thủ quỹ / GVCN mặc định: không gồm danh mục ẩn. */
export const CATEGORIES_TREASURER: MockCategory[] = CATEGORIES_FULL.filter((item) => !item.isHidden);

/** Bản công khai (học sinh / phụ huynh): không `isHidden`, không `transactionCount`; ví dụ "Khen thưởng" chưa có khoản công khai nên không có. */
export const CATEGORIES_PUBLIC: MockCategory[] = [CATEGORY_DONG_QUY, CATEGORY_VAN_PHONG_PHAM, CATEGORY_SINH_HOAT].map(
  ({ id, type, name }) => ({ id, type, name }),
);

/** Giả lập GET /funds/{fundId}/categories (lọc `type`, và `includeHidden` với bản đầy đủ); các method khác để route khác xử lý. */
export async function mockCategories(
  page: Page,
  categories: MockCategory[] = CATEGORIES_FULL,
  fundId = FUND,
): Promise<void> {
  await page.route(`**/api/v1/funds/${fundId}/categories?**`, (route) => answer(route, categories));
  await page.route(`**/api/v1/funds/${fundId}/categories`, (route) => answer(route, categories));
}

async function answer(route: import('@playwright/test').Route, categories: MockCategory[]): Promise<void> {
  if (route.request().method() !== 'GET') {
    return route.fallback();
  }
  const url = new URL(route.request().url());
  const type = url.searchParams.get('type');
  const includeHidden = url.searchParams.get('includeHidden') === 'true';
  return route.fulfill({
    json: categories.filter((item) => (!type || item.type === type) && (includeHidden || !item.isHidden)),
  });
}
