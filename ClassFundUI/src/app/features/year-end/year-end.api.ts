import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { SettlementLine, SurplusMethod } from '../events/events.models';
import { YearEndPreview, YearEndResult } from './year-end.models';

/** M14 – Tổng kết cuối năm quỹ chung (`/funds/{fundId}/year-end-settlement`). */
@Injectable({ providedIn: 'root' })
export class YearEndApi {
  private readonly http = inject(HttpClient);

  preview(fundId: string): Observable<YearEndPreview> {
    return this.http.get<YearEndPreview>(fundApiUrl(fundId, '/year-end-settlement/preview'));
  }

  result(fundId: string): Observable<YearEndResult> {
    return this.http.get<YearEndResult>(fundApiUrl(fundId, '/year-end-settlement'));
  }

  settle(fundId: string, surplusMethod: SurplusMethod | null): Observable<YearEndResult> {
    return this.http.post<YearEndResult>(fundApiUrl(fundId, '/year-end-settlement'), { surplusMethod });
  }

  confirmRefund(fundId: string, lineId: string): Observable<SettlementLine> {
    return this.http.post<SettlementLine>(fundApiUrl(fundId, `/year-end-settlement/lines/${encodeURIComponent(lineId)}/refunded`), {});
  }
}
