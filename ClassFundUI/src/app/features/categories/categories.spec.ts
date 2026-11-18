import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { fundApiUrl } from '../../core/config/api.config';
import { CategoriesApi } from './categories.api';
import { Category, categoryMeta, normalizeCategoryName } from './categories.models';

describe('categories', () => {
  it('chuẩn hóa tên và dòng phụ', () => {
    expect(normalizeCategoryName('  Văn   nghệ ')).toBe('Văn nghệ');
    const category: Category = { id: '1', name: 'Thu quỹ', type: 'Income', isSystem: true, isActive: true, usageCount: 28 };
    expect(categoryMeta(category)).toBe('Mặc định · 28 giao dịch');
    expect(categoryMeta({ ...category, isSystem: false, usageCount: 0 })).toBe('0 giao dịch');
  });

  describe('CategoriesApi', () => {
    let api: CategoriesApi;
    let backend: HttpTestingController;

    beforeEach(() => {
      TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
      api = TestBed.inject(CategoriesApi);
      backend = TestBed.inject(HttpTestingController);
    });

    afterEach(() => backend.verify());

    it('lọc theo loại / đang dùng và chỉ gửi tham số có giá trị', () => {
      api.list('f1').subscribe();
      expect(backend.expectOne(fundApiUrl('f1', '/categories')).request.params.keys()).toEqual([]);

      api.list('f1', { type: 'Expense', activeOnly: true }).subscribe();
      const filtered = backend.expectOne((request) => request.url === fundApiUrl('f1', '/categories'));
      expect(filtered.request.params.get('type')).toBe('Expense');
      expect(filtered.request.params.get('activeOnly')).toBe('true');
    });

    it('thêm và sửa', () => {
      api.create('f1', { name: 'Văn nghệ', type: 'Expense' }).subscribe();
      expect(backend.expectOne(fundApiUrl('f1', '/categories')).request.method).toBe('POST');

      api.update('f1', 'c1', { name: 'Văn nghệ', isActive: false }).subscribe();
      const update = backend.expectOne(fundApiUrl('f1', '/categories/c1'));
      expect(update.request.method).toBe('PUT');
      expect(update.request.body).toEqual({ name: 'Văn nghệ', isActive: false });
    });
  });
});
