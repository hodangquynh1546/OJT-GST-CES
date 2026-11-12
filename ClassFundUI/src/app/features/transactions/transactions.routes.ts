import { Routes } from '@angular/router';

import './transactions.errors';

const ledger = () => import('./ledger-page/ledger-page').then((m) => m.LedgerPage);

/** Module Thu chi (M6) – S-02 tại `/funds/:fundId/transactions`, chi tiết `/funds/:fundId/transactions/:txId`. */
export const TRANSACTION_ROUTES: Routes = [
  { path: '', loadComponent: ledger },
  { path: ':txId', loadComponent: ledger },
];
