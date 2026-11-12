import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { Member } from './members.models';

/** API module Thành viên (M5): `GET /funds/{fundId}/members`, chỉ xem. */
@Injectable({ providedIn: 'root' })
export class MembersApi {
  private readonly http = inject(HttpClient);

  /** Backend đã sắp: GVCN, thủ quỹ, học sinh; trong mỗi nhóm theo họ tên. */
  list(fundId: string): Observable<Member[]> {
    return this.http.get<Member[]>(fundApiUrl(fundId, '/members'));
  }
}
