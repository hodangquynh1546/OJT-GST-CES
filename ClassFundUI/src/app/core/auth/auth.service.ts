import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';

import { apiUrl } from '../config/api.config';
import { AntiforgeryService } from '../services/antiforgery.service';
import { CurrentUser, FundMembership } from './auth.models';

/** Response đổi phiên (đăng ký, đăng nhập, đổi mật khẩu): token antiforgery mới + người dùng (`GET /auth/me`). */
export interface SessionResponse {
  antiforgeryToken: string;
  user: CurrentUser;
}

export interface RegisterTeacherBody {
  fullName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly antiforgery = inject(AntiforgeryService);
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

  // ---------- M1 – Auth & Tài khoản ----------

  /** A-02: một ô email / tên đăng nhập, không chọn vai trò (D-34). */
  async login(login: string, password: string): Promise<CurrentUser> {
    return this.startSession(this.http.post<SessionResponse>(apiUrl('/auth/login'), { login, password }));
  }

  /** A-01: giáo viên tự đăng ký và được đăng nhập luôn. */
  async registerTeacher(body: RegisterTeacherBody): Promise<CurrentUser> {
    return this.startSession(this.http.post<SessionResponse>(apiUrl('/auth/register-teacher'), body));
  }

  /** A-03 / A-04: đổi mật khẩu – phiên này nhận cookie + token mới, các phiên khác bị đăng xuất. */
  async changePassword(currentPassword: string, newPassword: string): Promise<CurrentUser> {
    return this.startSession(
      this.http.post<SessionResponse>(apiUrl('/auth/change-password'), { currentPassword, newPassword }),
    );
  }

  /** A-04: sửa họ tên. */
  async updateProfile(fullName: string): Promise<CurrentUser> {
    const user = await firstValueFrom(this.http.put<CurrentUser>(apiUrl('/auth/me'), { fullName }));
    this.currentUser.set(user);
    return user;
  }

  /** Đăng xuất; luôn xóa phiên ở client kể cả khi gọi API lỗi. Token antiforgery cũ gắn với người dùng nên lấy token ẩn danh mới. */
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
