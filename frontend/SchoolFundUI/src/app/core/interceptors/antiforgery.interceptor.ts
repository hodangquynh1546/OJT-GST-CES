import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { switchMap } from 'rxjs';

import { isApiRequest } from '../config/api.config';
import { AntiforgeryService } from '../services/antiforgery.service';

const SAFE_METHODS = ['GET', 'HEAD', 'OPTIONS'];

/** Gắn header X-XSRF-TOKEN cho mọi request ghi tới API. */
export const antiforgeryInterceptor: HttpInterceptorFn = (request, next) => {
  if (SAFE_METHODS.includes(request.method) || !isApiRequest(request.url)) {
    return next(request);
  }

  return inject(AntiforgeryService)
    .token()
    .pipe(
      switchMap((token) => next(request.clone({ setHeaders: { 'X-XSRF-TOKEN': token } }))),
    );
};
