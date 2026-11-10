import { Routes } from '@angular/router';

import '../auth/auth.errors';

/** Module Auth (M1) – A-04 Tài khoản (trong AppShell). */
export const ACCOUNT_ROUTES: Routes = [
  {
    path: '',
    title: 'Tài khoản – ClassFund',
    loadComponent: () => import('./account-page/account-page').then((m) => m.AccountPage),
  },
];
