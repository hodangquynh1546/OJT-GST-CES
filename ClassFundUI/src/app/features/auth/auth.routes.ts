import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Module Auth (M1) – trang khách (guestGuard ở app.routes.ts). */
export const GUEST_ROUTES: Routes = [
  { path: 'login', ...comingSoon('A-02', 'Đăng nhập') },
  { path: 'register-teacher', ...comingSoon('A-01', 'Đăng ký giáo viên') },
];

/** Module Auth (M1) – A-03 tại `/change-password` (authGuard ở app.routes.ts, không có header / nav). */
export const CHANGE_PASSWORD_ROUTES: Routes = [{ path: '', ...comingSoon('A-03', 'Đặt mật khẩu mới') }];
