import { Routes } from '@angular/router';

import './auth.errors';

/** Module Auth (M1) – trang khách (guestGuard ở app.routes.ts). */
export const GUEST_ROUTES: Routes = [
  {
    path: 'login',
    title: 'Đăng nhập – ClassFund',
    loadComponent: () => import('./login-page/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'register-teacher',
    title: 'Đăng ký giáo viên – ClassFund',
    loadComponent: () => import('./register-teacher-page/register-teacher-page').then((m) => m.RegisterTeacherPage),
  },
];

/** Module Auth (M1) – A-03 tại `/change-password` (authGuard ở app.routes.ts, không có header / nav). */
export const CHANGE_PASSWORD_ROUTES: Routes = [
  {
    path: '',
    title: 'Đặt mật khẩu mới – ClassFund',
    loadComponent: () => import('./change-password-page/change-password-page').then((m) => m.ChangePasswordPage),
  },
];
