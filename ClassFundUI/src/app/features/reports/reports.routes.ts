import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** M13 – Báo cáo (guard Report.View ở app.routes.ts). S-03 dùng chung E-03; S-05 thuộc M14 (quyết toán cuối năm). */
export const REPORT_ROUTES: Routes = [
  {
    path: '',
    title: 'Báo cáo · ClassFund',
    loadComponent: () => import('./reports-page/reports-page').then((m) => m.ReportsPage),
  },
  // S-03 Tổng quan sự kiện: số liệu (FR-50) có ở tab Sự kiện và trang chi tiết sự kiện E-03.
  { path: 'events/:eventId', redirectTo: '/funds/:fundId/events/:eventId' },
  {
    path: 'students',
    title: 'Báo cáo học sinh · ClassFund',
    data: { tab: 'students' },
    loadComponent: () => import('./reports-page/reports-page').then((m) => m.ReportsPage),
  },
  { path: 'year-end', ...comingSoon('S-05', 'Tổng kết cuối năm') },
];
