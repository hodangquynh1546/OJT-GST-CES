import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { initialsOf } from '../../shared/utils/initials';
import { AuthService } from '../auth/auth.service';
import { CurrentFund } from '../services/current-fund.service';
import { buildNavItems } from './nav-items';
import { ROLE_DISPLAY } from './role-display';

/**
 * Thanh trên của khung quỹ (canvas S-01): breadcrumb "Quỹ / Trang hiện tại", chuông thông báo, nhãn vai trò trong quỹ
 * và ảnh đại diện (sang Tài khoản – nơi có nút Đăng xuất).
 */
@Component({
  selector: 'app-header',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="top">
      <nav class="crumbs" aria-label="Vị trí hiện tại">
        @if (fund(); as current) {
          <a class="fund" [routerLink]="['/funds', current.fundId]">{{ current.fundName }}</a>
          @if (page(); as label) {
            <span class="sep" aria-hidden="true">/</span>
            <b class="page" aria-current="page">{{ label }}</b>
          }
        }
      </nav>
      <div class="right">
        @if (fund(); as current) {
          <a class="bell" [routerLink]="['/funds', current.fundId, 'notifications']" aria-label="Thông báo">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 8a6 6 0 1 1 12 0c0 7 3 8 3 8H3s3-1 3-8zM10 20a2 2 0 0 0 4 0" /></svg>
          </a>
          <span class="pill" [class]="'pill pill--' + current.role">{{ roleBadge() }}</span>
        }
        <a class="avatar" routerLink="/account" [attr.aria-label]="'Tài khoản của ' + (user()?.fullName ?? '')">{{ initials() }}</a>
      </div>
    </header>
  `,
  styles: `
    .top {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      box-sizing: border-box;
      height: var(--cf-header-height);
      padding: 0 24px;
      border-bottom: 1px solid var(--cf-border);
      background: var(--cf-surface);
    }
    .crumbs {
      display: flex;
      align-items: center;
      gap: 8px;
      min-width: 0;
    }
    .fund {
      color: var(--cf-text-muted);
      text-decoration: none;
      white-space: nowrap;
    }
    .fund:hover {
      color: var(--cf-text);
    }
    .sep {
      color: var(--cf-border-strong);
    }
    .page {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .right {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .bell {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 36px;
      height: 36px;
      border: 1px solid var(--cf-border);
      border-radius: var(--cf-radius-lg);
      color: var(--cf-text);
    }
    .bell:hover {
      background: var(--cf-background);
    }
    .bell svg {
      width: 18px;
      height: 18px;
      fill: none;
      stroke: currentColor;
      stroke-linecap: round;
      stroke-linejoin: round;
      stroke-width: 2;
    }
    .pill {
      padding: 0 10px;
      border-radius: var(--cf-radius-pill);
      font-size: 12px;
      font-weight: 600;
      line-height: 24px;
      white-space: nowrap;
    }
    .pill--Owner {
      background: var(--cf-ink);
      color: var(--cf-surface);
    }
    .pill--Treasurer {
      background: var(--cf-role-treasurer-tint);
      color: var(--cf-role-treasurer);
    }
    .pill--Student {
      background: var(--cf-subtle);
      color: var(--cf-text);
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
export class AppHeader {
  private readonly auth = inject(AuthService);
  private readonly currentFund = inject(CurrentFund);

  protected readonly user = this.auth.user;
  protected readonly fund = this.currentFund.context;
  protected readonly initials = computed(() => initialsOf(this.user()?.fullName));
  protected readonly roleBadge = computed(() => {
    const role = this.fund()?.role;
    return role ? ROLE_DISPLAY[role].badge : '';
  });

  /** Tên trang hiện tại: mục menu có đường dẫn khớp dài nhất với URL ("/funds/x/events/y" → "Sự kiện"). */
  protected readonly page = computed(() => {
    const context = this.fund();
    if (!context) {
      return null;
    }
    const url = this.currentFund.url();
    const items = buildNavItems({ fundId: context.fundId, role: context.role, permissions: context.permissions });
    const match = items
      .filter((item) => url === item.link || url.startsWith(item.link + '/'))
      .sort((a, b) => b.link.length - a.link.length)[0];
    return match?.label ?? null;
  });
}
