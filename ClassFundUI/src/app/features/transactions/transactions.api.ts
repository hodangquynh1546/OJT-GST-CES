import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import {
  Transaction,
  TransactionHistoryItem,
  TransactionPage,
  TransactionQuery,
  TransactionType,
  TransactionValues,
} from './transactions.models';

/** API module Thu chi (M6). Danh mục dùng `CategoriesApi` của M5 (module-plan.md – dùng chung với M6). */
@Injectable({ providedIn: 'root' })
export class TransactionsApi {
  private readonly http = inject(HttpClient);

  list(fundId: string, query: TransactionQuery): Observable<TransactionPage> {
    let params = new HttpParams().set('status', query.status).set('page', query.page).set('pageSize', query.pageSize);
    for (const key of ['type', 'categoryId', 'from', 'to', 'search'] as const) {
      const value = query[key]?.trim();
      if (value) {
        params = params.set(key, value);
      }
    }
    return this.http.get<TransactionPage>(fundApiUrl(fundId, '/transactions'), { params });
  }

  get(fundId: string, id: string): Observable<Transaction> {
    return this.http.get<Transaction>(fundApiUrl(fundId, `/transactions/${encodeURIComponent(id)}`));
  }

  history(fundId: string, id: string): Observable<TransactionHistoryItem[]> {
    return this.http.get<TransactionHistoryItem[]>(fundApiUrl(fundId, `/transactions/${encodeURIComponent(id)}/history`));
  }

  /** `idempotencyKey` sinh một lần mỗi lần mở form; gửi lại khi bấm Lưu nhiều lần (BR-12). */
  create(fundId: string, type: TransactionType, values: TransactionValues, idempotencyKey: string): Observable<Transaction> {
    return this.http.post<Transaction>(fundApiUrl(fundId, '/transactions'), { type, ...values, idempotencyKey });
  }

  update(fundId: string, id: string, values: TransactionValues): Observable<Transaction> {
    return this.http.put<Transaction>(fundApiUrl(fundId, `/transactions/${encodeURIComponent(id)}`), values);
  }

  cancel(fundId: string, id: string, reason: string): Observable<Transaction> {
    return this.http.post<Transaction>(fundApiUrl(fundId, `/transactions/${encodeURIComponent(id)}/cancel`), { reason });
  }
}
