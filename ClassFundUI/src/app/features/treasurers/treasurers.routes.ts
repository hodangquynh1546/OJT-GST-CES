import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const TREASURER_ROUTES: Routes = [
  { path: '', ...comingSoon('T-01', 'Thủ quỹ') },
];
