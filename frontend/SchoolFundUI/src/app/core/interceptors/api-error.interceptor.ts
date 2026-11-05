import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { ApiError } from '../../shared/models/api-error';
import { AuthService } from '../auth/auth.service';
import { apiUrl, isApiRequest } from '../config/api.config';

/** Đổi lỗi HTTP thành ApiError và xử lý các lỗi chung: hết phiên, còn mật khẩu tạm. */
export const apiErrorInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || !isApiRequest(request.url)) {
        return throwError(() => error);
      }

      const apiError = ApiError.from(error);
      const expectsAnonymous = [apiUrl('/auth/me'), apiUrl('/auth/login')].includes(request.url);
      if (apiError.status === 401 && !expectsAnonymous) {
        auth.clearSession();
        void router.navigateByUrl('/login');
      } else if (apiError.code === 'password.change_required') {
        void router.navigateByUrl('/change-password');
      }
      return throwError(() => apiError);
    }),
  );
};
