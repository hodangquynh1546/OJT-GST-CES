import { Routes } from '@angular/router';


/** M13 – Báo cáo (guard Report.View ở app.routes.ts). S-03 dùng chung E-03; S-05 (tổng kết cuối năm) thuộc M14. */
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
  {
    path: 'year-end',
    title: 'Tổng kết cuối năm · ClassFund',
    loadComponent: () => import('../year-end/year-end-page/year-end-page').then((m) => m.YearEndPage),
  },
];
