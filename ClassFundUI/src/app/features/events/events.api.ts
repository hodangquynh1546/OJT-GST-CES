import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { AssignmentStatus } from '../../shared/models/labels';
import { PagedResult } from '../../shared/models/paged-result';
import { toHttpParams } from '../../shared/utils/http-params';
import { AssignmentRow, CreateChargeRequest } from '../charges/charges.models';
import {
  CompletionPreview,
  EventCharge,
  EventDetail,
  EventParticipants,
  EventSummary,
  ParticipantPaymentStatus,
  ParticipationResponse,
  SaveEventBody,
  SettlementLine,
  SettlementPreview,
  SettlementResult,
  SurplusMethod,
} from './events.models';

/** M8 – Sự kiện. */
@Injectable({ providedIn: 'root' })
export class EventsApi {
  private readonly http = inject(HttpClient);

  list(fundId: string): Observable<EventSummary[]> {
    return this.http.get<EventSummary[]>(fundApiUrl(fundId, '/events'));
  }

  get(fundId: string, eventId: string): Observable<EventDetail> {
    return this.http.get<EventDetail>(this.url(fundId, eventId));
  }

  create(fundId: string, body: SaveEventBody): Observable<EventDetail> {
    return this.http.post<EventDetail>(fundApiUrl(fundId, '/events'), body);
  }

  update(fundId: string, eventId: string, body: SaveEventBody): Observable<EventDetail> {
    return this.http.put<EventDetail>(this.url(fundId, eventId), body);
  }

  cancel(fundId: string, eventId: string, reason: string): Observable<EventDetail> {
    return this.http.post<EventDetail>(this.url(fundId, eventId, '/cancel'), { reason });
  }

  completionPreview(fundId: string, eventId: string): Observable<CompletionPreview> {
    return this.http.get<CompletionPreview>(this.url(fundId, eventId, '/completion-preview'));
  }

  complete(fundId: string, eventId: string): Observable<CompletionPreview> {
    return this.http.post<CompletionPreview>(this.url(fundId, eventId, '/complete'), null);
  }

  participants(fundId: string, eventId: string, paymentStatus: ParticipantPaymentStatus | null = null): Observable<EventParticipants> {
    return this.http.get<EventParticipants>(this.url(fundId, eventId, '/participants'), { params: toHttpParams({ paymentStatus }) });
  }

  respond(fundId: string, eventId: string, response: Exclude<ParticipationResponse, 'Pending'>): Observable<EventDetail> {
    return this.http.put<EventDetail>(this.url(fundId, eventId, '/me/response'), { response });
  }

  // ---- Quyết toán (M9) ----

  settlementPreview(fundId: string, eventId: string): Observable<SettlementPreview> {
    return this.http.get<SettlementPreview>(this.url(fundId, eventId, '/settlement/preview'));
  }

  settlement(fundId: string, eventId: string): Observable<SettlementResult> {
    return this.http.get<SettlementResult>(this.url(fundId, eventId, '/settlement'));
  }

  settle(fundId: string, eventId: string, surplusMethod: SurplusMethod | null): Observable<SettlementResult> {
    return this.http.post<SettlementResult>(this.url(fundId, eventId, '/settlement'), { surplusMethod });
  }

  confirmRefund(fundId: string, eventId: string, lineId: string): Observable<SettlementLine> {
    return this.http.post<SettlementLine>(this.url(fundId, eventId, `/settlement/lines/${encodeURIComponent(lineId)}/refunded`), {});
  }

  /** "Nhắc đóng" một người tham gia còn khoản chưa đóng (thông báo riêng cho người đó). */
  remindParticipant(fundId: string, eventId: string, userId: string): Observable<{ reminded: number }> {
    return this.http.post<{ reminded: number }>(this.url(fundId, eventId, `/participants/${encodeURIComponent(userId)}/remind`), {});
  }

  // ---- Khoản thu sự kiện (M6b) ----

  createCharge(fundId: string, eventId: string, body: CreateChargeRequest): Observable<EventCharge> {
    return this.http.post<EventCharge>(this.url(fundId, eventId, '/charges'), body);
  }

  closeCharge(fundId: string, eventId: string, chargeId: string): Observable<EventCharge> {
    return this.http.post<EventCharge>(this.chargeUrl(fundId, eventId, chargeId, '/close'), {});
  }

  cancelCharge(fundId: string, eventId: string, chargeId: string): Observable<EventCharge> {
    return this.http.post<EventCharge>(this.chargeUrl(fundId, eventId, chargeId, '/cancel'), {});
  }

  chargeAssignments(
    fundId: string,
    eventId: string,
    chargeId: string,
    query: { status?: AssignmentStatus; search?: string; page?: number; pageSize?: number } = {},
  ): Observable<PagedResult<AssignmentRow>> {
    return this.http.get<PagedResult<AssignmentRow>>(this.chargeUrl(fundId, eventId, chargeId, '/assignments'), {
      params: toHttpParams({ ...query }),
    });
  }

  private url(fundId: string, eventId: string, rest = ''): string {
    return fundApiUrl(fundId, `/events/${encodeURIComponent(eventId)}${rest}`);
  }

  private chargeUrl(fundId: string, eventId: string, chargeId: string, rest = ''): string {
    return this.url(fundId, eventId, `/charges/${encodeURIComponent(chargeId)}${rest}`);
  }
}
