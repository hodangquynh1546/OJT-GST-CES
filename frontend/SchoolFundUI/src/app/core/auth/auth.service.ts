import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../config/api.config';
import { CurrentUser, FundMembership } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly currentUser = signal<CurrentUser | null>(null);

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUser() !== null);

  /** Đọc lại phiên từ cookie khi mở ứng dụng; lỗi nghĩa là chưa đăng nhập. */
  async restoreSession(): Promise<void> {
    try {
      this.currentUser.set(await firstValueFrom(this.http.get<CurrentUser>(apiUrl('/auth/me'))));
    } catch {
      this.currentUser.set(null);
    }
  }

  clearSession(): void {
    this.currentUser.set(null);
  }

  membership(fundId: string | null | undefined): FundMembership | undefined {
    return this.currentUser()?.memberships.find((item) => item.fundId === fundId);
  }

  /** Đường dẫn trang chính sau đăng nhập (sitemap mục 1). */
  homeUrl(): string {
    const user = this.currentUser();
    if (!user) {
      return '/login';
    }
    if (user.mustChangePassword) {
      return '/change-password';
    }
    return user.kind === 'Student' && user.memberships.length > 0
      ? `/funds/${user.memberships[0].fundId}`
      : '/funds';
  }
}
