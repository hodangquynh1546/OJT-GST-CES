import { Routes } from '@angular/router';

/** S-01 Tổng quan quỹ chung (mọi thành viên – Fund.View). */
export const OVERVIEW_ROUTES: Routes = [
  {
    path: '',
    title: 'Tổng quan · ClassFund',
    loadComponent: () => import('./overview-page/overview-page').then((m) => m.OverviewPage),
  },
];
