import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { StudentOption, TreasurerAssigned, TreasurerAssignment } from './treasurers.models';

/** API module Phân công thủ quỹ (M4) + danh sách học sinh (M3) để chọn người phân công. */
@Injectable({ providedIn: 'root' })
export class TreasurersApi {
  private readonly http = inject(HttpClient);

  /** Đang hiệu lực trước, rồi lịch sử (backend đã sắp). */
  list(fundId: string): Observable<TreasurerAssignment[]> {
    return this.http.get<TreasurerAssignment[]>(fundApiUrl(fundId, '/treasurers'));
  }

  assign(fundId: string, studentId: string): Observable<string> {
    return this.http
      .post<TreasurerAssigned>(fundApiUrl(fundId, '/treasurers'), { studentId })
      .pipe(map((created) => created.id));
  }

  revoke(fundId: string, assignmentId: string): Observable<void> {
    return this.http.post<void>(fundApiUrl(fundId, `/treasurers/${encodeURIComponent(assignmentId)}/revoke`), null);
  }

  students(fundId: string): Observable<StudentOption[]> {
    return this.http.get<StudentOption[]>(fundApiUrl(fundId, '/students'));
  }
}
