import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { toHttpParams } from '../../shared/utils/http-params';
import { Category, CreateCategoryBody, LedgerType, UpdateCategoryBody } from './categories.models';

/** M5 – Danh mục thu chi. M7 dùng `list(fundId, { type, activeOnly: true })` cho ô chọn danh mục. */
@Injectable({ providedIn: 'root' })
export class CategoriesApi {
  private readonly http = inject(HttpClient);

  list(fundId: string, query: { type?: LedgerType; activeOnly?: boolean } = {}): Observable<Category[]> {
    return this.http.get<Category[]>(fundApiUrl(fundId, '/categories'), {
      params: toHttpParams({ type: query.type, activeOnly: query.activeOnly }),
    });
  }

  create(fundId: string, body: CreateCategoryBody): Observable<Category> {
    return this.http.post<Category>(fundApiUrl(fundId, '/categories'), body);
  }

  update(fundId: string, categoryId: string, body: UpdateCategoryBody): Observable<Category> {
    return this.http.put<Category>(fundApiUrl(fundId, `/categories/${encodeURIComponent(categoryId)}`), body);
  }
}
