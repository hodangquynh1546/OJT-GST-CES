import { FinancialScope } from '../../shared/models/labels';

/** Khớp `AuditLogDto`. `before` / `after` là JSON đã lưu; `ipAddress` đã che hai phần cuối. */
export interface AuditLog {
  id: string;
  at: string;
  actorUserId: string | null;
  actorName: string;
  action: string;
  scope: FinancialScope;
  eventId: string | null;
  eventName: string | null;
  entityType: string | null;
  entityId: string | null;
  summary: string;
  isSelfAction: boolean;
  note: string | null;
  before: string | null;
  after: string | null;
  ipAddress: string | null;
}

export interface AuditActor {
  userId: string;
  fullName: string;
}

/** Ô "Phạm vi" của U-01: Tất cả / Chung / Sự kiện. */
export type AuditScopeFilter = 'all' | 'General' | 'Event';

export interface AuditLogQuery {
  /** yyyy-MM-dd theo giờ Việt Nam. */
  from?: string | null;
  to?: string | null;
  actorId?: string | null;
  scope?: AuditScopeFilter;
  eventId?: string | null;
  action?: string | null;
  selfOnly?: boolean;
  page: number;
  pageSize: number;
}

export const AUDIT_PAGE_SIZE = 20;
