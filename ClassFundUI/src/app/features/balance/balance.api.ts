import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { toHttpParams } from '../../shared/utils/http-params';
import { MyBalance, WithdrawalList, WithdrawalRequest, WithdrawalStatus } from './balance.models';

/** M10 – Số dư cá nhân và yêu cầu rút. */
@Injectable({ providedIn: 'root' })
export class BalanceApi {
  private readonly http = inject(HttpClient);

  mine(fundId: string): Observable<MyBalance> {
    return this.http.get<MyBalance>(fundApiUrl(fundId, '/me/balance'));
  }

  /** Rút toàn bộ số dư (không có body, OQ-11). */
  requestWithdrawal(fundId: string): Observable<MyBalance> {
    return this.http.post<MyBalance>(fundApiUrl(fundId, '/me/withdrawal-requests'), null);
  }

  list(fundId: string, status: WithdrawalStatus | null = null): Observable<WithdrawalList> {
    return this.http.get<WithdrawalList>(fundApiUrl(fundId, '/withdrawal-requests'), { params: toHttpParams({ status }) });
  }

  markPaid(fundId: string, requestId: string): Observable<WithdrawalRequest> {
    return this.http.post<WithdrawalRequest>(fundApiUrl(fundId, `/withdrawal-requests/${encodeURIComponent(requestId)}/paid`), null);
  }

  reject(fundId: string, requestId: string, reason: string): Observable<WithdrawalRequest> {
    return this.http.post<WithdrawalRequest>(fundApiUrl(fundId, `/withdrawal-requests/${encodeURIComponent(requestId)}/reject`), { reason });
  }
}
