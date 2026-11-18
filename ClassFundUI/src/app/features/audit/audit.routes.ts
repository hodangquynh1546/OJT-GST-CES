import { Routes } from '@angular/router';

/** U-01 Nhật ký hoạt động (guard Audit.View ở app.routes.ts). */
export const AUDIT_ROUTES: Routes = [
  {
    path: '',
    title: 'Nhật ký hoạt động · ClassFund',
    loadComponent: () => import('./audit-log-page/audit-log-page').then((m) => m.AuditLogPage),
  },
];
