import { Routes } from '@angular/router';

import './balance.errors';

/** B-01 Số dư cá nhân (guard Balance.ViewOwn ở app.routes.ts). */
export const BALANCE_ROUTES: Routes = [
  {
    path: '',
    title: 'Số dư cá nhân · ClassFund',
    loadComponent: () => import('./balance-page/balance-page').then((m) => m.BalancePage),
  },
];

/** B-02 Yêu cầu rút tiền (guard Balance.Handle ở app.routes.ts). */
export const WITHDRAWAL_ROUTES: Routes = [
  {
    path: '',
    title: 'Yêu cầu rút tiền · ClassFund',
    loadComponent: () => import('./withdrawals-page/withdrawals-page').then((m) => m.WithdrawalsPage),
  },
];
