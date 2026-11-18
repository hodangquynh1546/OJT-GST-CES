import { Routes } from '@angular/router';

import './auth.errors';
import { ChangePasswordPage } from './change-password-page';
import { LoginPage } from './login-page';
import { RegisterPage } from './register-page';

/** A-01 / A-02: dành cho khách (guestGuard ở `app.routes.ts`). */
export const GUEST_ROUTES: Routes = [
  { path: 'login', component: LoginPage, title: 'Đăng nhập · ClassFund' },
  { path: 'register', component: RegisterPage, title: 'Đăng ký thủ quỹ · ClassFund' },
];

/** A-03: cần đăng nhập, nằm ngoài shell để mật khẩu tạm không thấy menu. */
export const CHANGE_PASSWORD_ROUTES: Routes = [
  { path: '', component: ChangePasswordPage, title: 'Đổi mật khẩu · ClassFund' },
];
