import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { PagedResult } from '../../shared/models/paged-result';
import { toHttpParams } from '../../shared/utils/http-params';
import {
  AddMemberResult,
  BulkAddResult,
  Member,
  MemberQuery,
  RemovalPreview,
  RemoveMemberResult,
  TemporaryPasswordResult,
} from './members.models';

/** Gọi API thành viên quỹ (`/funds/{fundId}/members`). */
@Injectable({ providedIn: 'root' })
export class MembersApi {
  private readonly http = inject(HttpClient);

  list(fundId: string, query: MemberQuery): Observable<PagedResult<Member>> {
    return this.http.get<PagedResult<Member>>(fundApiUrl(fundId, '/members'), { params: toHttpParams({ ...query }) });
  }

  add(fundId: string, email: string, fullName: string): Observable<AddMemberResult> {
    return this.http.post<AddMemberResult>(fundApiUrl(fundId, '/members'), { email, fullName });
  }

  addMany(fundId: string, emails: string[]): Observable<BulkAddResult> {
    return this.http.post<BulkAddResult>(fundApiUrl(fundId, '/members/bulk'), { emails });
  }

  update(fundId: string, memberId: string, fullName: string): Observable<Member> {
    return this.http.put<Member>(fundApiUrl(fundId, `/members/${memberId}`), { fullName });
  }

  resetPassword(fundId: string, memberId: string): Observable<TemporaryPasswordResult> {
    return this.http.post<TemporaryPasswordResult>(fundApiUrl(fundId, `/members/${memberId}/reset-password`), {});
  }

  removalPreview(fundId: string, memberId: string): Observable<RemovalPreview> {
    return this.http.get<RemovalPreview>(fundApiUrl(fundId, `/members/${memberId}/removal-preview`));
  }

  remove(fundId: string, memberId: string): Observable<RemoveMemberResult> {
    return this.http.post<RemoveMemberResult>(fundApiUrl(fundId, `/members/${memberId}/remove`), {});
  }
}
