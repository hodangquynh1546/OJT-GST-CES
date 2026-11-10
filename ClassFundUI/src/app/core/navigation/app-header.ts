import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../auth/auth.service';
import { CurrentFund } from '../services/current-fund.service';
import { ROLE_DISPLAY } from './role-display';

@Component({
  selector: 'app-header',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <a class="brand" [routerLink]="homeUrl()">
        <span class="logo" aria-hidden="true">
          <svg width="18" height="18" viewBox="0 0 24 24">
            <path d="M4 7h16v12H4zM4 7l2-3h12l2 3M9 12h6" fill="none" stroke="currentColor" stroke-width="2"
              stroke-linecap="round" stroke-linejoin="round" />
          </svg>
        </span>
        <b>ClassFund</b>
      </a>
      @if (membership(); as fund) {
        <span class="divider" aria-hidden="true">|</span>
        @if (canSwitchFund()) {
          <a class="fund" routerLink="/funds" title="Đổi quỹ">{{ fund.fundName }} <span class="switch">· Đổi quỹ ▾</span></a>
        } @else {
          <span class="fund">{{ fund.fundName }}</span>
        }
      }
      @if (role(); as display) {
        <span class="badge" [style.background]="display.tint" [style.color]="display.color">
          {{ display.badge }}
        </span>
      }
      <a class="user" routerLink="/account" [attr.aria-label]="'Tài khoản của ' + (user()?.fullName ?? '')">
        <span class="avatar" aria-hidden="true" [style.background]="role()?.tint" [style.color]="role()?.color">{{ initial() }}</span>
        <span class="user-text">
          <b class="user-name">{{ user()?.fullName }}</b>
          <span class="user-role">{{ subtitle() }}</span>
        </span>
        <svg class="chevron" width="16" height="16" viewBox="0 0 24 24" aria-hidden="true">
          <path d="m6 9 6 6 6-6" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
        </svg>
      </a>
    </header>
  `,
  styles: `
    .header {
      display: flex;
      align-items: center;
      gap: 12px;
      box-sizing: border-box;
      height: var(--cf-header-height);
      padding: 0 16px;
      border-bottom: 1px solid var(--cf-border);
      background: var(--cf-surface);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 8px;
      color: var(--cf-text);
      font-size: 16px;
      text-decoration: none;
    }
    .logo {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 30px;
      height: 30px;
      border-radius: 7px;
      background: var(--cf-primary);
      color: var(--cf-surface);
    }
    .divider {
      color: var(--cf-border-strong);
    }
    .fund {
      overflow: hidden;
      min-width: 0;
      color: var(--cf-text);
      text-decoration: none;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .switch {
      color: var(--cf-primary);
      font-size: 13px;
    }
    .badge {
      display: inline-flex;
      align-items: center;
      height: 24px;
      padding: 0 10px;
      border-radius: var(--cf-radius-pill);
      font-size: 12px;
      font-weight: 600;
      white-space: nowrap;
    }
    .user {
      display: flex;
      align-items: center;
      gap: 8px;
      min-height: 44px;
      margin-left: auto;
      color: var(--cf-text);
      text-decoration: none;
    }
    .avatar {
      display: flex;
      align-items: center;
      justify-content: center;
      flex: none;
      width: 32px;
      height: 32px;
      border-radius: 16px;
      background: var(--cf-primary-tint);
      color: var(--cf-primary-dark);
      font-weight: 700;
    }
    .user-text {
      display: flex;
      flex-direction: column;
      line-height: 16px;
    }
    .user-name {
      font-size: 13px;
    }
    .user-role,
    .chevron {
      color: var(--cf-text-muted);
      font-size: 12px;
    }
  `,
})
export class AppHeader {
  private readonly auth = inject(AuthService);
  private readonly currentFund = inject(CurrentFund);

  protected readonly user = this.auth.user;
  protected readonly membership = this.currentFund.context;
  protected readonly role = computed(() => {
    const role = this.membership()?.role;
    return role ? ROLE_DISPLAY[role] : null;
  });
  protected readonly canSwitchFund = computed(() => this.user()?.kind !== 'Student');
  protected readonly homeUrl = computed(() => (this.user() ? this.auth.homeUrl() : '/'));

  /** Dòng phụ dưới tên (board AppHeader): theo vai trò trong quỹ đang hiển thị; phụ huynh kèm tên con. */
  protected readonly subtitle = computed(() => {
    const user = this.user();
    const fund = this.membership();
    if (!user) {
      return '';
    }
    if (fund?.role === 'Parent' && fund.linkedStudents?.length) {
      return `Phụ huynh em ${fund.linkedStudents.join(', ')}`;
    }
    if (fund) {
      return ROLE_DISPLAY[fund.role].subtitle;
    }
    return { Teacher: 'Giáo viên chủ nhiệm', Student: 'Học sinh', Parent: 'Phụ huynh' }[user.kind];
  });
  protected readonly initial = computed(
    () => this.user()?.fullName.trim().split(/\s+/).pop()?.charAt(0) ?? '',
  );
}
