import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const BALANCE_ROUTES: Routes = [
  { path: '', ...comingSoon('B-01', 'Số dư cá nhân') },
];

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const WITHDRAWAL_ROUTES: Routes = [
  { path: '', ...comingSoon('B-02', 'Yêu cầu rút tiền') },
];
