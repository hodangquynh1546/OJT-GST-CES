import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { fundApiUrl } from '../../core/config/api.config';
import { PagedResult } from '../../shared/models/paged-result';
import { toHttpParams } from '../../shared/utils/http-params';
import { AuditActor, AuditLog, AuditLogQuery } from './audit.models';

/** M12 – Nhật ký hoạt động (chỉ đọc). */
@Injectable({ providedIn: 'root' })
export class AuditApi {
  private readonly http = inject(HttpClient);

  list(fundId: string, query: AuditLogQuery): Observable<PagedResult<AuditLog>> {
    return this.http.get<PagedResult<AuditLog>>(fundApiUrl(fundId, '/audit-logs'), {
      params: toHttpParams({
        from: query.from,
        to: query.to,
        actorId: query.actorId,
        scope: query.scope && query.scope !== 'all' ? query.scope : null,
        eventId: query.eventId,
        action: query.action,
        selfOnly: query.selfOnly ? true : null,
        page: query.page,
        pageSize: query.pageSize,
      }),
    });
  }

  actors(fundId: string): Observable<AuditActor[]> {
    return this.http.get<AuditActor[]>(fundApiUrl(fundId, '/audit-logs/actors'));
  }
}
