import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const LEDGER_ROUTES: Routes = [
  { path: '', ...comingSoon('L-01', 'Thu chi chung') },
  { path: 'new', ...comingSoon('L-02', 'Ghi giao dịch') },
  { path: ':entryId', ...comingSoon('L-03', 'Chi tiết giao dịch') },
];
