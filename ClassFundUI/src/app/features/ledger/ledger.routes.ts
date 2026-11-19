import { Routes } from '@angular/router';

import { fundPermissionGuard } from '../../core/auth/auth.guards';
import { Permissions } from '../../core/auth/permissions';
import './ledger.errors';

/** M7 – Thu chi chung (guard Fund.View ở app.routes.ts). L-02 là hộp thoại của L-01 nhưng vẫn mở thẳng được bằng `…/ledger/new`. */
export const LEDGER_ROUTES: Routes = [
  {
    path: '',
    title: 'Thu chi chung · ClassFund',
    loadComponent: () => import('./ledger-page/ledger-page').then((m) => m.LedgerPage),
  },
  {
    path: 'new',
    title: 'Ghi giao dịch · ClassFund',
    canActivate: [fundPermissionGuard(Permissions.LedgerManage)],
    data: { openNew: true },
    loadComponent: () => import('./ledger-page/ledger-page').then((m) => m.LedgerPage),
  },
  {
    path: ':entryId',
    title: 'Chi tiết giao dịch · ClassFund',
    loadComponent: () => import('./ledger-detail/ledger-detail').then((m) => m.LedgerDetail),
  },
];
