import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const FUND_LIST_ROUTES: Routes = [
  { path: '', ...comingSoon('F-01', 'Quỹ của tôi') },
  { path: 'new', ...comingSoon('F-02', 'Tạo quỹ') },
];

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const FUND_SETTINGS_ROUTES: Routes = [
  { path: '', ...comingSoon('F-03', 'Thông tin quỹ') },
];
