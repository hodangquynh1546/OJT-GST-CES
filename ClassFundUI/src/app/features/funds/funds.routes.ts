import { Routes } from '@angular/router';

/** Module Quỹ (M2) – G-01 tại `/funds`. */
export const FUND_LIST_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./fund-list/fund-list').then((m) => m.FundList) },
];

/** Module Quỹ (M2) – G-04 tại `/funds/:fundId/settings` (guard Fund.Manage ở app.routes.ts). */
export const FUND_SETTINGS_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./fund-settings/fund-settings').then((m) => m.FundSettings) },
];
