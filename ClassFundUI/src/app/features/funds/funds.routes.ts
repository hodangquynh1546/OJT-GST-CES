import { Routes } from '@angular/router';

import { FundListPage } from './fund-list-page';

/**
 * F-01 Quỹ của tôi, F-02 Tạo quỹ – ngoài khung có menu quỹ (app.routes.ts), có thanh trên riêng (`app-top-bar`).
 * Quyền Fund.Create của F-02 kiểm ở màn hình và backend.
 */
export const FUND_LIST_ROUTES: Routes = [
  { path: '', component: FundListPage, title: 'Quỹ của tôi · ClassFund' },
  { path: 'new', title: 'Tạo quỹ · ClassFund', loadComponent: () => import('./fund-new/fund-new').then((m) => m.FundNew) },
];

/** F-03 Thông tin quỹ (trong khung menu; guard Fund.Manage ở app.routes.ts). */
export const FUND_SETTINGS_ROUTES: Routes = [
  { path: '', title: 'Thông tin quỹ', loadComponent: () => import('./fund-settings/fund-settings').then((m) => m.FundSettings) },
];
