import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { toHttpParams } from '../../shared/utils/http-params';
import { NotificationList } from './notifications.models';

/** M11 – Thông báo của tôi (`/funds/{fundId}/me/notifications`). */
@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly http = inject(HttpClient);

  list(fundId: string, query: { unreadOnly?: boolean; page?: number; pageSize?: number } = {}): Observable<NotificationList> {
    return this.http.get<NotificationList>(fundApiUrl(fundId, '/me/notifications'), { params: toHttpParams({ ...query }) });
  }

  unreadCount(fundId: string): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(fundApiUrl(fundId, '/me/notifications/unread-count'));
  }

  markRead(fundId: string, id: string): Observable<void> {
    return this.http.post<void>(fundApiUrl(fundId, `/me/notifications/${encodeURIComponent(id)}/read`), null);
  }

  markAllRead(fundId: string): Observable<{ marked: number }> {
    return this.http.post<{ marked: number }>(fundApiUrl(fundId, '/me/notifications/read-all'), null);
  }
}
