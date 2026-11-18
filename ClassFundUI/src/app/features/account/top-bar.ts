import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { initialsOf } from '../../shared/utils/initials';

/**
 * Thanh trên của các màn ngoài khung menu quỹ (canvas "ClassFund UI Design"): F-01 Quỹ của tôi, F-02 Tạo quỹ,
 * A-04 Tài khoản. Chỉ khi người dùng chọn một quỹ mới vào khung có menu (AppShell).
 * `funds`: Đăng xuất + ảnh đại diện (sang Tài khoản). `account`: Quỹ của tôi + Đăng xuất.
 */
@Component({
  selector: 'app-top-bar',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="bar">
      <a class="brand" routerLink="/funds">
        <span class="logo" aria-hidden="true">
          <svg width="16" height="16" viewBox="0 0 24 24">
            <path d="M4 7h16v12H4zM4 7l2-3h12l2 3M9 12h6" fill="none" stroke="currentColor" stroke-width="2"
              stroke-linecap="round" stroke-linejoin="round" />
          </svg>
        </span>
        <b>ClassFund</b>
      </a>
      <nav class="actions" aria-label="Tài khoản">
        @if (variant() === 'account') {
          <a routerLink="/funds" class="cf-link-button">Quỹ của tôi</a>
        }
        <button type="button" class="cf-link-button" (click)="logout()">Đăng xuất</button>
        @if (variant() === 'funds') {
          <a routerLink="/account" class="avatar" [attr.aria-label]="'Tài khoản của ' + (name() ?? '')">{{ initials() }}</a>
        }
      </nav>
    </header>
  `,
  styles: `
    .bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      box-sizing: border-box;
      height: var(--cf-header-height);
      padding: 0 24px;
      border-bottom: 1px solid var(--cf-border);
      background: var(--cf-surface);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 8px;
      color: var(--cf-text);
      text-decoration: none;
    }
    .logo {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 28px;
      height: 28px;
      border-radius: 8px;
      background: var(--cf-ink);
      color: var(--cf-surface);
    }
    .actions {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .cf-link-button {
      min-height: 36px;
      font: inherit;
      font-weight: 600;
      cursor: pointer;
    }
    .avatar {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      border-radius: 16px;
      background: var(--cf-subtle);
      color: var(--cf-text);
      font-size: 12px;
      font-weight: 700;
      text-decoration: none;
    }
  `,
})
export class TopBar {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly variant = input<'funds' | 'account'>('funds');

  protected readonly name = computed(() => this.auth.user()?.fullName);
  protected readonly initials = computed(() => initialsOf(this.name()));

  protected async logout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigateByUrl('/login');
  }
}
