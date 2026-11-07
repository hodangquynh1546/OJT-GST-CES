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
      <a class="brand" routerLink="/">ClassFund</a>
      @if (membership(); as fund) {
        <span class="divider" aria-hidden="true">|</span>
        <span class="fund">{{ fund.fundName }}</span>
        @if (canSwitchFund()) {
          <a class="switch" routerLink="/funds">· Đổi quỹ ▾</a>
        }
      }
      @if (role(); as display) {
        <span class="badge" [style.background]="display.tint" [style.color]="display.color">
          {{ display.badge }}
        </span>
      }
      <a class="user" routerLink="/account" aria-label="Tài khoản">
        <span class="avatar" aria-hidden="true">{{ initial() }}</span>
        <span class="user-name">{{ user()?.fullName }}</span>
      </a>
    </header>
  `,
  styles: `
    .header {
      display: flex;
      align-items: center;
      gap: 10px;
      box-sizing: border-box;
      height: var(--cf-header-height);
      padding: 0 16px;
      border-bottom: 1px solid var(--cf-border);
      background: var(--cf-surface);
    }
    .brand {
      color: var(--cf-primary-dark);
      font-size: 16px;
      font-weight: 700;
      text-decoration: none;
    }
    .divider {
      color: var(--cf-border-strong);
    }
    .fund {
      overflow: hidden;
      min-width: 0;
      font-weight: 600;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .switch {
      white-space: nowrap;
    }
    .badge {
      display: inline-flex;
      align-items: center;
      height: 22px;
      padding: 0 8px;
      border-radius: 11px;
      font-size: 12px;
      font-weight: 600;
      white-space: nowrap;
    }
    .user {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-left: auto;
      color: inherit;
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
  `,
})
export class AppHeader {
  private readonly auth = inject(AuthService);
  private readonly currentFund = inject(CurrentFund);

  protected readonly user = this.auth.user;
  protected readonly membership = this.currentFund.membership;
  protected readonly role = computed(() => {
    const role = this.membership()?.role;
    return role ? ROLE_DISPLAY[role] : null;
  });
  protected readonly canSwitchFund = computed(() => this.user()?.kind !== 'Student');
  protected readonly initial = computed(
    () => this.user()?.fullName.trim().split(/\s+/).pop()?.charAt(0) ?? '',
  );
}
