import { Category } from './categories.models';
import { categoryNameError } from './categories.validation';

const categories: Category[] = [
  { id: 'c1', type: 'Expense', name: 'Văn phòng phẩm', isHidden: false, transactionCount: 2 },
  { id: 'c2', type: 'Expense', name: 'Photo tài liệu', isHidden: true, transactionCount: 1 },
  { id: 'c3', type: 'Income', name: 'Đóng quỹ', isHidden: false, transactionCount: 0 },
];

describe('categoryNameError', () => {
  it('rejects an empty or blank name', () => {
    expect(categoryNameError('', 'Expense', categories)).toBe('Nhập tên danh mục.');
    expect(categoryNameError('   ', 'Expense', categories)).toBe('Nhập tên danh mục.');
  });

  it('rejects a name longer than 100 characters after trimming', () => {
    expect(categoryNameError(' ' + 'a'.repeat(100) + ' ', 'Expense', categories)).toBeNull();
    expect(categoryNameError('a'.repeat(101), 'Expense', categories)).toBe('Tên danh mục tối đa 100 ký tự.');
  });

  it('rejects a duplicate in the same type ignoring case and accents-preserving Vietnamese letters', () => {
    expect(categoryNameError('VĂN PHÒNG PHẨM', 'Expense', categories)).toBe('Đã có danh mục chi tên “VĂN PHÒNG PHẨM”.');
    expect(categoryNameError(' đóng quỹ ', 'Income', categories)).toBe('Đã có danh mục thu tên “đóng quỹ”.');
  });

  it('also rejects a name used by a hidden category', () => {
    expect(categoryNameError('photo tài liệu', 'Expense', categories)).toContain('Đã có danh mục chi');
  });

  it('allows the same name in the other type and a different accent', () => {
    expect(categoryNameError('Đóng quỹ', 'Expense', categories)).toBeNull();
    expect(categoryNameError('Van phong pham', 'Expense', categories)).toBeNull();
  });

  it('ignores the category being renamed', () => {
    expect(categoryNameError('văn phòng phẩm', 'Expense', categories, 'c1')).toBeNull();
    expect(categoryNameError('Photo tài liệu', 'Expense', categories, 'c1')).not.toBeNull();
  });
});
