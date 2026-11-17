import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const CHARGE_ROUTES: Routes = [
  { path: '', ...comingSoon('H-01', 'Khoản thu chung') },
  { path: 'new', ...comingSoon('H-02', 'Tạo khoản thu chung') },
];

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const MY_PAYMENT_ROUTES: Routes = [
  { path: '', ...comingSoon('H-03', 'Khoản cần đóng của tôi') },
];

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const PAYMENT_CONFIRM_ROUTES: Routes = [
  { path: '', ...comingSoon('H-04', 'Xác nhận thanh toán') },
];
