import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { MembersApi } from '../members/members.api';
import { Candidate, Treasurer } from './treasurers.models';

/** Gọi API đội thủ quỹ (`/funds/{fundId}/treasurers`); danh sách học sinh để chọn lấy từ API thành viên. */
@Injectable({ providedIn: 'root' })
export class TreasurersApi {
  private readonly http = inject(HttpClient);
  private readonly members = inject(MembersApi);

  list(fundId: string): Observable<Treasurer[]> {
    return this.http.get<Treasurer[]>(fundApiUrl(fundId, '/treasurers'));
  }

  grant(fundId: string, memberId: string): Observable<Treasurer> {
    return this.http.post<Treasurer>(fundApiUrl(fundId, '/treasurers'), { memberId });
  }

  revoke(fundId: string, memberId: string): Observable<Treasurer> {
    return this.http.post<Treasurer>(fundApiUrl(fundId, `/treasurers/${memberId}/revoke`), {});
  }

  /** Học sinh đang hoạt động, lọc theo tên / email (tối đa 50 kết quả mỗi lần gõ). */
  candidates(fundId: string, search: string): Observable<Candidate[]> {
    return this.members
      .list(fundId, { role: 'Student', status: 'Active', search, page: 1, pageSize: 50 })
      .pipe(
        map((page) =>
          page.items.map((member) => ({
            memberId: member.id,
            fullName: member.fullName,
            label: `${member.fullName} – ${member.email}`,
          })),
        ),
      );
  }
}
