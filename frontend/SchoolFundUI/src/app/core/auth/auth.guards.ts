import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { Permission } from './permissions';

/** Chuyển về trang chính theo loại tài khoản. */
export const homeGuard: CanActivateFn = () =>
  inject(Router).parseUrl(inject(AuthService).homeUrl());

/** Trang chỉ dành cho người chưa đăng nhập. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? inject(Router).parseUrl(auth.homeUrl()) : true;
};

/** Cần đăng nhập; còn mật khẩu tạm thì chỉ được vào trang đổi mật khẩu. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const user = auth.user();
  if (!user) {
    return router.parseUrl('/login');
  }
  if (user.mustChangePassword && state.url !== '/change-password') {
    return router.parseUrl('/change-password');
  }
  return true;
};

/** Không thuộc quỹ → trang 404; thuộc quỹ nhưng thiếu quyền → trang 403. Backend vẫn tự kiểm tra lại. */
export function fundPermissionGuard(permission: Permission): CanActivateFn {
  return (route) => {
    const router = inject(Router);
    const membership = inject(AuthService).membership(route.paramMap.get('fundId'));
    if (!membership) {
      return router.parseUrl('/not-found');
    }
    return membership.permissions.includes(permission) ? true : router.parseUrl('/forbidden');
  };
}
