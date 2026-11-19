import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { toHttpParams } from '../../shared/utils/http-params';
import { CreateExpenseBody, LedgerEntryDetail, LedgerList, LedgerQuery } from './ledger.models';

/** M7 – Sổ thu chi theo phạm vi. Không có eventId = quỹ chung. */
@Injectable({ providedIn: 'root' })
export class LedgerApi {
  private readonly http = inject(HttpClient);

  list(fundId: string, query: LedgerQuery): Observable<LedgerList> {
    return this.http.get<LedgerList>(fundApiUrl(fundId, '/ledger-entries'), {
      params: toHttpParams({
        eventId: query.eventId,
        type: query.type,
        categoryId: query.categoryId,
        from: query.from,
        to: query.to,
        page: query.page,
        pageSize: query.pageSize,
      }),
    });
  }

  get(fundId: string, entryId: string): Observable<LedgerEntryDetail> {
    return this.http.get<LedgerEntryDetail>(fundApiUrl(fundId, `/ledger-entries/${encodeURIComponent(entryId)}`));
  }

  /** Chỉ ghi chi (BR-39): thu đi qua khoản thu. */
  createExpense(fundId: string, body: CreateExpenseBody): Observable<LedgerEntryDetail> {
    return this.http.post<LedgerEntryDetail>(fundApiUrl(fundId, '/ledger-entries'), body);
  }

  /** Bút toán đảo; trả về dòng đảo vừa tạo. */
  reverse(fundId: string, entryId: string, reason: string): Observable<LedgerEntryDetail> {
    return this.http.post<LedgerEntryDetail>(fundApiUrl(fundId, `/ledger-entries/${encodeURIComponent(entryId)}/reverse`), { reason });
  }
}
