import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { apiUrl, fundApiUrl } from '../../core/config/api.config';
import { FundCreated, FundSummary } from './funds.models';

/** API module Quỹ (M2): `GET /funds`, `POST /funds`, `PUT /funds/{fundId}`. */
@Injectable({ providedIn: 'root' })
export class FundsApi {
  private readonly http = inject(HttpClient);

  /** Quỹ tôi có vai trò, đã sắp theo tên ở backend. */
  list(): Observable<FundSummary[]> {
    return this.http.get<FundSummary[]>(apiUrl('/funds'));
  }

  create(name: string): Observable<string> {
    return this.http.post<FundCreated>(apiUrl('/funds'), { name }).pipe(map((created) => created.id));
  }

  rename(fundId: string, name: string): Observable<void> {
    return this.http.put<void>(fundApiUrl(fundId), { name });
  }
}
