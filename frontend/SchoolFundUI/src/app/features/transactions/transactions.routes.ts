import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

export const TRANSACTION_ROUTES: Routes = [
  { path: '', ...comingSoon('S-02', 'Thu chi') },
  { path: ':txId', ...comingSoon('S-02', 'Chi tiết giao dịch') },
];
