import { Routes } from '@angular/router';

/** Module Báo cáo (M7b) – S-03 tại `/funds/:fundId/reports`. */
export const REPORT_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./reports-page/reports-page').then((m) => m.ReportsPage) },
];
