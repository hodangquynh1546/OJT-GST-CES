import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import {
  CreateStudentRequest,
  Credential,
  ParentLinkResult,
  Student,
  StudentCreated,
} from './students.models';

/**
 * API module Học sinh & Phụ huynh (M3), chỉ GVCN (`Student.Manage`).
 * Response có mật khẩu tạm đều `Cache-Control: no-store`; không ghi chúng ra log hay lưu lại.
 */
@Injectable({ providedIn: 'root' })
export class StudentsApi {
  private readonly http = inject(HttpClient);

  list(fundId: string): Observable<Student[]> {
    return this.http.get<Student[]>(fundApiUrl(fundId, '/students'));
  }

  create(fundId: string, body: CreateStudentRequest): Observable<StudentCreated> {
    return this.http.post<StudentCreated>(fundApiUrl(fundId, '/students'), body);
  }

  rename(fundId: string, studentId: string, fullName: string): Observable<void> {
    return this.http.put<void>(fundApiUrl(fundId, `/students/${studentId}`), { fullName });
  }

  /** `username` bỏ trống → backend gợi ý dạng `ten.ho.NNN`. */
  issueAccount(fundId: string, studentId: string, username?: string): Observable<Credential> {
    return this.http.post<Credential>(fundApiUrl(fundId, `/students/${studentId}/account`), { username });
  }

  resetPassword(fundId: string, studentId: string): Observable<Credential> {
    return this.http.post<Credential>(fundApiUrl(fundId, `/students/${studentId}/reset-password`), {});
  }

  linkParent(fundId: string, studentId: string, email: string, fullName?: string): Observable<ParentLinkResult> {
    return this.http.post<ParentLinkResult>(fundApiUrl(fundId, `/students/${studentId}/parent`), { email, fullName });
  }

  unlinkParent(fundId: string, studentId: string): Observable<void> {
    return this.http.post<void>(fundApiUrl(fundId, `/students/${studentId}/parent/unlink`), {});
  }
}
