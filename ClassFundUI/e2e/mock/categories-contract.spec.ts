import { expect, test } from '@playwright/test';

import { FUND } from '../support/mock-session';
import { CATEGORIES_FULL, CATEGORIES_PUBLIC, CATEGORIES_TREASURER, mockCategories } from '../support/mock-categories';

/** Kiểm tra bộ mock danh mục (dùng chung cho M5 / M6) khớp hợp đồng GET /categories. */
const API_URL = `/api/v1/funds/${FUND}/categories`;

const fetchJson = (page: import('@playwright/test').Page, query = '') =>
  page.evaluate(async (path) => (await fetch(path)).json(), `${API_URL}${query}`);

test.beforeEach(async ({ page }) => {
  await page.goto('/login');
});

test('mock danh mục thủ quỹ: mặc định bỏ danh mục ẩn; includeHidden=true thêm danh mục ẩn; lọc theo loại', async ({ page }) => {
  await mockCategories(page, CATEGORIES_FULL);

  const visible = await fetchJson(page);
  expect(visible).toEqual(CATEGORIES_TREASURER);
  expect(visible.every((item: { isHidden: boolean; transactionCount: number }) => item.isHidden === false && item.transactionCount >= 0)).toBe(true);

  const all = await fetchJson(page, '?includeHidden=true');
  expect(all).toHaveLength(7);
  expect(all.filter((item: { isHidden: boolean }) => item.isHidden).map((item: { name: string }) => item.name)).toEqual(['Photo tài liệu']);

  const expenses = await fetchJson(page, '?type=Expense&includeHidden=true');
  expect(expenses.map((item: { type: string }) => item.type)).toEqual(Array(5).fill('Expense'));
});

test('mock danh mục công khai: không isHidden, không transactionCount', async ({ page }) => {
  await mockCategories(page, CATEGORIES_PUBLIC);

  const list = await fetchJson(page);
  expect(list).toHaveLength(3);
  for (const item of list) {
    expect(Object.keys(item).sort()).toEqual(['id', 'name', 'type']);
  }
});
