import { Routes } from '@angular/router';

/** Module Tổng quan & Báo cáo (M7) – S-01 tại `/funds/:fundId`. */
export const OVERVIEW_ROUTES: Routes = [
  { path: '', title: 'Tổng quan', loadComponent: () => import('./overview-page/overview-page').then((m) => m.OverviewPage) },
];
