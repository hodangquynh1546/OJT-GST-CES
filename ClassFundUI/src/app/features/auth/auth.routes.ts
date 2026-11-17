import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const GUEST_ROUTES: Routes = [
  { path: 'login', ...comingSoon('A-01', 'Đăng nhập') },
  { path: 'register', ...comingSoon('A-02', 'Đăng ký thủ quỹ') },
];

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const CHANGE_PASSWORD_ROUTES: Routes = [
  { path: '', ...comingSoon('A-03', 'Đổi mật khẩu') },
];
