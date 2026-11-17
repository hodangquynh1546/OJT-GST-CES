import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const CATEGORY_ROUTES: Routes = [
  { path: '', ...comingSoon('C-01', 'Danh mục thu chi') },
];
