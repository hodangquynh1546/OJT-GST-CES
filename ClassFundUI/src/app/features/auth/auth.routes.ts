import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

export const GUEST_ROUTES: Routes = [
  { path: 'login', ...comingSoon('A-02', 'Đăng nhập') },
  { path: 'register-teacher', ...comingSoon('A-01', 'Đăng ký giáo viên') },
];
