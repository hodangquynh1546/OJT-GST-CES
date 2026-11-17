import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const EVENT_ROUTES: Routes = [
  { path: '', ...comingSoon('E-01', 'Sự kiện') },
  { path: 'new', ...comingSoon('E-02', 'Tạo sự kiện') },
  { path: ':eventId', ...comingSoon('E-03', 'Chi tiết sự kiện') },
  { path: ':eventId/settlement', ...comingSoon('E-04', 'Quyết toán sự kiện') },
];
