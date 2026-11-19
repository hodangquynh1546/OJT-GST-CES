import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { AssignmentStatus, PaymentStatus } from '../../shared/models/labels';
import { PagedResult } from '../../shared/models/paged-result';
import { toHttpParams } from '../../shared/utils/http-params';
import {
  AssignmentRow,
  Charge,
  ChargeStatus,
  CreateChargeRequest,
  MyAssignmentItem,
  PaymentQueueItem,
  SubmitPaymentRequest,
  SubmittedPayment,
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

  // ---------- Thanh toán ----------

  /** Thủ quỹ ghi nhận học sinh đã đưa tiền mặt (gửi + xác nhận một bước). */
  recordCash(fundId: string, assignmentId: string): Observable<SubmittedPayment> {
    return this.http.post<SubmittedPayment>(fundApiUrl(fundId, `/assignments/${assignmentId}/record-cash`), {});
  }

  mine(fundId: string): Observable<MyAssignmentItem[]> {
    return this.http.get<MyAssignmentItem[]>(fundApiUrl(fundId, '/me/assignments'));
  }

  submit(fundId: string, assignmentId: string, request: SubmitPaymentRequest): Observable<SubmittedPayment> {
    return this.http.post<SubmittedPayment>(fundApiUrl(fundId, `/me/assignments/${assignmentId}/payments`), request);
  }

  payments(
    fundId: string,
    query: { status?: PaymentStatus; page?: number; pageSize?: number } = {},
  ): Observable<PagedResult<PaymentQueueItem>> {
    return this.http.get<PagedResult<PaymentQueueItem>>(fundApiUrl(fundId, '/payments'), { params: toHttpParams({ ...query }) });
  }

  confirm(fundId: string, paymentId: string): Observable<PaymentQueueItem> {
    return this.http.post<PaymentQueueItem>(fundApiUrl(fundId, `/payments/${paymentId}/confirm`), {});
  }

  reject(fundId: string, paymentId: string, reason: string): Observable<PaymentQueueItem> {
    return this.http.post<PaymentQueueItem>(fundApiUrl(fundId, `/payments/${paymentId}/reject`), { reason });
  }
}
