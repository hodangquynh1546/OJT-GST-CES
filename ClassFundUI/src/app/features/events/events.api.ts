import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { toHttpParams } from '../../shared/utils/http-params';
import {
  CompletionPreview,
  EventDetail,
  EventParticipants,
  EventSummary,
  ParticipantPaymentStatus,
  ParticipationResponse,
  SaveEventBody,
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

  private url(fundId: string, eventId: string, rest = ''): string {
    return fundApiUrl(fundId, `/events/${encodeURIComponent(eventId)}${rest}`);
  }
}
