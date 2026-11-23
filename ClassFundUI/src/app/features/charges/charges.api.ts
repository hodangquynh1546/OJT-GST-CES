import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { AssignmentStatus } from '../../shared/models/labels';
import { PagedResult } from '../../shared/models/paged-result';
import { toHttpParams } from '../../shared/utils/http-params';
import {
  AssignmentRow,
  Charge,
  ChargeStatus,
  CreateChargeRequest,
  CollectedPayment,
  CollectPaymentRequest,
  MyAssignmentItem,
} from './charges.models';


/** Gọi API khoản thu chung và thanh toán (`/funds/{fundId}/charges`, `/me/assignments`, `/payments`). */
@Injectable({ providedIn: 'root' })
export class ChargesApi {
  private readonly http = inject(HttpClient);

  list(fundId: string, query: { status?: ChargeStatus; page?: number; pageSize?: number } = {}): Observable<PagedResult<Charge>> {
    return this.http.get<PagedResult<Charge>>(fundApiUrl(fundId, '/charges'), { params: toHttpParams({ ...query }) });
  }

  get(fundId: string, chargeId: string): Observable<Charge> {
    return this.http.get<Charge>(fundApiUrl(fundId, `/charges/${chargeId}`));
  }

  create(fundId: string, request: CreateChargeRequest): Observable<Charge> {
    return this.http.post<Charge>(fundApiUrl(fundId, '/charges'), request);
  }

  assignments(
    fundId: string,
    chargeId: string,
    query: { status?: AssignmentStatus; search?: string; page?: number; pageSize?: number },
  ): Observable<PagedResult<AssignmentRow>> {
    return this.http.get<PagedResult<AssignmentRow>>(fundApiUrl(fundId, `/charges/${chargeId}/assignments`), {
      params: toHttpParams({ ...query }),
    });
  }

  close(fundId: string, chargeId: string): Observable<Charge> {
    return this.http.post<Charge>(fundApiUrl(fundId, `/charges/${chargeId}/close`), {});
  }

  cancel(fundId: string, chargeId: string): Observable<Charge> {
    return this.http.post<Charge>(fundApiUrl(fundId, `/charges/${chargeId}/cancel`), {});
  }

  /** Nhắc người chưa đóng (thông báo trong ứng dụng); trả số người được nhắc. */
  remind(fundId: string, chargeId: string): Observable<{ reminded: number }> {
    return this.http.post<{ reminded: number }>(fundApiUrl(fundId, `/charges/${chargeId}/remind`), {});
  }

  // ---------- Thu tiền (thủ quỹ tích "đã thu") ----------

  mine(fundId: string): Observable<MyAssignmentItem[]> {
    return this.http.get<MyAssignmentItem[]>(fundApiUrl(fundId, '/me/assignments'));
  }

  /** Thủ quỹ tích đã thu (Tiền mặt / Chuyển khoản). */
  collect(fundId: string, assignmentId: string, request: CollectPaymentRequest): Observable<CollectedPayment> {
    return this.http.post<CollectedPayment>(fundApiUrl(fundId, `/assignments/${assignmentId}/collect`), request);
  }

  /** Bỏ tích nhầm: bút toán đảo, nghĩa vụ về Chưa nộp. */
  uncollect(fundId: string, assignmentId: string, reason: string): Observable<CollectedPayment> {
    return this.http.post<CollectedPayment>(fundApiUrl(fundId, `/assignments/${assignmentId}/uncollect`), { reason });
  }

  /** "Nhắc đóng" một người còn khoản chưa nộp (thông báo riêng, có ghi nhật ký). */
  remindOne(fundId: string, assignmentId: string): Observable<{ reminded: number }> {
    return this.http.post<{ reminded: number }>(fundApiUrl(fundId, `/assignments/${assignmentId}/remind`), {});
  }
}
