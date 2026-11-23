import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { PagedResult } from '../../shared/models/paged-result';
import { toHttpParams } from '../../shared/utils/http-params';
import { MemberBalance, MemberBalancesResult, MyBalance, Withdrawal } from './balance.models';

/** M10 – Số dư cá nhân (học sinh chỉ xem) và "Rút số dư" của thủ quỹ. */
@Injectable({ providedIn: 'root' })
export class BalanceApi {
  private readonly http = inject(HttpClient);

  mine(fundId: string): Observable<MyBalance> {
    return this.http.get<MyBalance>(fundApiUrl(fundId, '/me/balance'));
  }

  /** Danh sách thành viên kèm số dư (thủ quỹ). */
  balances(fundId: string, query: { search?: string; page?: number; pageSize?: number } = {}): Observable<MemberBalancesResult> {
    return this.http.get<MemberBalancesResult>(fundApiUrl(fundId, '/balances'), { params: toHttpParams({ ...query }) });
  }

  /** Rút đúng số tiền nhập (≥ 10.000đ, bội của 1.000đ, không vượt số dư). */
  withdraw(fundId: string, userId: string, amount: number): Observable<MemberBalance> {
    return this.http.post<MemberBalance>(fundApiUrl(fundId, `/balances/${encodeURIComponent(userId)}/withdraw`), { amount });
  }

  history(fundId: string, query: { page?: number; pageSize?: number } = {}): Observable<PagedResult<Withdrawal>> {
    return this.http.get<PagedResult<Withdrawal>>(fundApiUrl(fundId, '/withdrawals'), { params: toHttpParams({ ...query }) });
  }
}
