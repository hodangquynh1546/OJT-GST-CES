import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { toHttpParams } from '../../shared/utils/http-params';
import { EventReportRow, FundOverview, GeneralReport, StudentReportRow } from './reports.models';

/** M13 – Tổng quan & báo cáo (chỉ đọc). */
@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);

  overview(fundId: string): Observable<FundOverview> {
    return this.http.get<FundOverview>(fundApiUrl(fundId, '/reports/overview'));
  }

  general(fundId: string, from: string | null, to: string | null): Observable<GeneralReport> {
    return this.http.get<GeneralReport>(fundApiUrl(fundId, '/reports/general'), { params: toHttpParams({ from, to }) });
  }

  events(fundId: string): Observable<EventReportRow[]> {
    return this.http.get<EventReportRow[]>(fundApiUrl(fundId, '/reports/events'));
  }

  students(fundId: string): Observable<StudentReportRow[]> {
    return this.http.get<StudentReportRow[]>(fundApiUrl(fundId, '/reports/students'));
  }

  me(fundId: string): Observable<StudentReportRow> {
    return this.http.get<StudentReportRow>(fundApiUrl(fundId, '/reports/me'));
  }
}
