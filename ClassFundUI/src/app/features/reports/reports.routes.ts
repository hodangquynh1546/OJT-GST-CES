import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const REPORT_ROUTES: Routes = [
  { path: '', ...comingSoon('S-02', 'Báo cáo thu chi chung') },
  { path: 'events/:eventId', ...comingSoon('S-03', 'Tổng quan sự kiện') },
  { path: 'students', ...comingSoon('S-04', 'Báo cáo học sinh') },
  { path: 'year-end', ...comingSoon('S-05', 'Tổng kết cuối năm') },
];
