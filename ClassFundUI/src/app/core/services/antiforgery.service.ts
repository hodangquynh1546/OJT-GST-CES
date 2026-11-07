import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, of, tap } from 'rxjs';

import { apiUrl } from '../config/api.config';

/** Giữ token antiforgery trong bộ nhớ. Đăng nhập, đăng ký, đổi mật khẩu trả token mới qua set(). */
@Injectable({ providedIn: 'root' })
export class AntiforgeryService {
  private readonly http = inject(HttpClient);
  private cached: string | null = null;

  set(token: string): void {
    this.cached = token;
  }

  token(): Observable<string> {
    if (this.cached) {
      return of(this.cached);
    }
    return this.http.get<{ antiforgeryToken: string }>(apiUrl('/auth/antiforgery')).pipe(
      map((response) => response.antiforgeryToken),
      tap((token) => this.set(token)),
    );
  }
}
