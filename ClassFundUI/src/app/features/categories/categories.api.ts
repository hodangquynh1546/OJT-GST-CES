import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import {
  Category,
  CategoryCreated,
  CategoryListQuery,
  CreateCategoryRequest,
  UpdateCategoryRequest,
} from './categories.models';

/**
 * API module Danh mục (M5): `GET` / `POST /funds/{fundId}/categories`, `PUT …/{id}`.
 * Dùng chung cho T-02 (quản lý), dialog giao dịch và bộ lọc sổ quỹ (M6) – module khác dùng lại service này, không tự viết.
 */
@Injectable({ providedIn: 'root' })
export class CategoriesApi {
  private readonly http = inject(HttpClient);

  /** Phạm vi theo quyền người xem: GVCN / thủ quỹ đầy đủ, học sinh / phụ huynh chỉ danh mục công khai (X-01). */
  list(fundId: string, query: CategoryListQuery = {}): Observable<Category[]> {
    let params = new HttpParams();
    if (query.type) {
      params = params.set('type', query.type);
    }
    if (query.includeHidden) {
      params = params.set('includeHidden', 'true');
    }
    return this.http.get<Category[]>(fundApiUrl(fundId, '/categories'), { params });
  }

  create(fundId: string, body: CreateCategoryRequest): Observable<string> {
    return this.http
      .post<CategoryCreated>(fundApiUrl(fundId, '/categories'), body)
      .pipe(map((created) => created.id));
  }

  update(fundId: string, categoryId: string, body: UpdateCategoryRequest): Observable<void> {
    return this.http.put<void>(fundApiUrl(fundId, `/categories/${categoryId}`), body);
  }
}
