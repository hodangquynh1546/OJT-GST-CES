import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiUrl, fundApiUrl } from '../../core/config/api.config';
import { FundBody, FundDetail, FundSummary } from './funds.models';

/** M2 – Quỹ (docs/api/api-catalogue.md). */
@Injectable({ providedIn: 'root' })
export class FundsApi {
  private readonly http = inject(HttpClient);

  listMine(): Observable<FundSummary[]> {
    return this.http.get<FundSummary[]>(apiUrl('/funds'));
  }

  create(body: FundBody): Observable<FundDetail> {
    return this.http.post<FundDetail>(apiUrl('/funds'), body);
  }

  get(fundId: string): Observable<FundDetail> {
    return this.http.get<FundDetail>(fundApiUrl(fundId));
  }

  update(fundId: string, body: FundBody): Observable<FundDetail> {
    return this.http.put<FundDetail>(fundApiUrl(fundId), body);
  }
}
