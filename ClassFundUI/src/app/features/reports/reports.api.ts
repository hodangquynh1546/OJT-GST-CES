import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { MonthlyReport, YearlyReport } from './reports.models';

/** API module Báo cáo (M7b). M7a (S-01) dùng lại `yearly` cho biểu đồ 3 tháng. */
@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);

  monthly(fundId: string, year: number, month: number): Observable<MonthlyReport> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get<MonthlyReport>(fundApiUrl(fundId, '/reports/monthly'), { params });
  }

  yearly(fundId: string, year: number): Observable<YearlyReport> {
    const params = new HttpParams().set('year', year);
    return this.http.get<YearlyReport>(fundApiUrl(fundId, '/reports/yearly'), { params });
  }
}
