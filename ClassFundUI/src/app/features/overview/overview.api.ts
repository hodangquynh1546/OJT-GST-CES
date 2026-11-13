import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { FundOverview } from './overview.models';

/** API Tổng quan (M7a): `GET /funds/{fundId}`. Biểu đồ 3 tháng dùng `ReportsApi.yearly` của M7b. */
@Injectable({ providedIn: 'root' })
export class OverviewApi {
  private readonly http = inject(HttpClient);

  get(fundId: string): Observable<FundOverview> {
    return this.http.get<FundOverview>(fundApiUrl(fundId));
  }
}
