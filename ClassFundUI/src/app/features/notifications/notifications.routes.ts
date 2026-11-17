import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const NOTIFICATION_ROUTES: Routes = [
  { path: '', ...comingSoon('N-01', 'Thông báo') },
];
