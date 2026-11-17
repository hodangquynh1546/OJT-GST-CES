import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';

import { apiUrl } from '../config/api.config';
import { AntiforgeryService } from '../services/antiforgery.service';
import { CurrentUser, FundMembership } from './auth.models';
import { Permission } from './permissions';

/** Response đổi phiên (đăng ký, đăng nhập, đổi mật khẩu): token antiforgery mới + người dùng (`GET /auth/me`). */
export interface SessionResponse {
  antiforgeryToken: string;
  user: CurrentUser;
}

/**
 * Phiên đăng nhập (cookie HttpOnly do backend cấp). Foundation chỉ giữ trạng thái phiên + khôi phục khi mở app;
 * các hàm gọi API đăng ký / đăng nhập / đổi mật khẩu thuộc module M1 và đã đặt sẵn chỗ ở đây.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly antiforgery = inject(AntiforgeryService);
  private readonly currentUser = signal<CurrentUser | null>(null);

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly canCreateFund = computed(() => this.hasAccountPermission('Fund.Create'));

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

  hasAccountPermission(permission: Permission): boolean {
    return this.currentUser()?.accountPermissions.includes(permission) ?? false;
  }

  /** Trang chính sau đăng nhập: một quỹ → vào thẳng quỹ; nhiều quỹ hoặc chưa có quỹ → danh sách quỹ. */
  homeUrl(): string {
    const user = this.currentUser();
    if (!user) {
      return '/login';
    }
    if (user.mustChangePassword) {
      return '/change-password';
    }
    return user.memberships.length === 1 ? `/funds/${user.memberships[0].fundId}` : '/funds';
  }

  // ---------- M1 – Auth & Tài khoản (nối API ở module M1) ----------

  async login(email: string, password: string): Promise<CurrentUser> {
    return this.startSession(this.http.post<SessionResponse>(apiUrl('/auth/login'), { email, password }));
  }

  /** Tự đăng ký tài khoản thủ quỹ đầu tiên. */
  async register(body: { fullName: string; email: string; password: string }): Promise<CurrentUser> {
    return this.startSession(this.http.post<SessionResponse>(apiUrl('/auth/register'), body));
  }

  async changePassword(currentPassword: string, newPassword: string): Promise<CurrentUser> {
    return this.startSession(
      this.http.post<SessionResponse>(apiUrl('/auth/change-password'), { currentPassword, newPassword }),
    );
  }

  /** Đăng xuất; luôn xóa phiên ở client kể cả khi gọi API lỗi, rồi lấy token antiforgery ẩn danh mới. */
  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post<void>(apiUrl('/auth/logout'), {}));
    } finally {
      this.currentUser.set(null);
      try {
        const fresh = await firstValueFrom(this.http.get<{ antiforgeryToken: string }>(apiUrl('/auth/antiforgery')));
        this.antiforgery.set(fresh.antiforgeryToken);
      } catch {
        // Lần ghi kế tiếp sẽ báo lỗi antiforgery; người dùng tải lại trang là được.
      }
    }
  }

  private async startSession(request: Observable<SessionResponse>): Promise<CurrentUser> {
    const session = await firstValueFrom(request);
    this.antiforgery.set(session.antiforgeryToken);
    this.currentUser.set(session.user);
    return session.user;
  }
}
