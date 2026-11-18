import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { AuthService } from '../auth/auth.service';
import { CurrentFund } from '../services/current-fund.service';
import { fundShortName } from './fund-short-name';
import { NAV_GROUPS, NavItem, buildNavItems } from './nav-items';
import { ROLE_DISPLAY } from './role-display';

/**
 * Sidebar của khung quỹ (canvas S-01): thẻ quỹ ở đầu (bấm để chuyển quỹ → "Quỹ của tôi"), rồi ba nhóm menu
 * Quỹ chung · Cá nhân · Quản lý; nhóm không có mục nào thì không hiện.
 */
@Component({
  selector: 'app-nav',
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="side">
      @if (fund(); as current) {
        <a class="switcher" routerLink="/funds" [attr.aria-label]="'Chuyển quỹ, đang mở ' + current.fundName">
          <span class="logo" aria-hidden="true">{{ short() }}</span>
          <span class="text">
            <b class="name">{{ current.fundName }}</b>
            <span class="role">{{ roleLabel() }}</span>
          </span>
          <svg class="chevrons" viewBox="0 0 24 24" aria-hidden="true"><path d="m8 9 4-4 4 4M8 15l4 4 4-4" /></svg>
        </a>
      }
      <nav class="nav" aria-label="Điều hướng chính">
        @for (group of groups(); track group.name) {
          <div class="group" role="group" [attr.aria-label]="group.name">
            <p class="group-title">{{ group.name }}</p>
            @for (item of group.items; track item.id) {
              <a
                class="item"
                [routerLink]="item.link"
                routerLinkActive="active"
                ariaCurrentWhenActive="page"
                [routerLinkActiveOptions]="{ exact: item.exact }"
              >
                <svg viewBox="0 0 24 24" aria-hidden="true"><path [attr.d]="item.icon" /></svg>
                <span class="label">{{ item.label }}</span>
              </a>
            }
          </div>
        }
      </nav>
    </aside>
  `,
  styles: `
    .side {
      display: flex;
      flex-direction: column;
      gap: 8px;
      box-sizing: border-box;
      width: var(--cf-nav-width);
      height: 100%;
      overflow-y: auto;
      padding: 12px;
      border-right: 1px solid var(--cf-border);
      background: var(--cf-surface);
    }
    .switcher {
      display: flex;
      align-items: center;
      gap: 10px;
      box-sizing: border-box;
      padding: 12px;
      border: 1px solid var(--cf-border);
      border-radius: 10px;
      color: var(--cf-text);
      text-decoration: none;
    }
    .switcher:hover {
      border-color: var(--cf-border-strong);
      background: var(--cf-background);
    }
    .logo {
      display: flex;
      flex: none;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      border-radius: 8px;
      background: var(--cf-ink);
      color: var(--cf-surface);
      font-size: 10px;
      font-weight: 700;
    }
    .text {
      display: flex;
      flex: 1;
      flex-direction: column;
      min-width: 0;
      line-height: 18px;
    }
    .name {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .role {
      color: var(--cf-text-muted);
      font-size: 12px;
    }
    .chevrons {
      flex: none;
      width: 16px;
      height: 16px;
      fill: none;
      stroke: var(--cf-text-muted);
      stroke-linecap: round;
      stroke-linejoin: round;
      stroke-width: 2;
    }
    .nav {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .group {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .group-title {
      margin: 8px 0 4px;
      padding: 0 12px;
      color: var(--cf-text-muted);
      font-size: 12px;
    }
    .item {
      display: flex;
      align-items: center;
      gap: 10px;
      min-height: 36px;
      padding: 0 12px;
      border-radius: var(--cf-radius-md);
      color: var(--cf-text);
      text-decoration: none;
      white-space: nowrap;
    }
    .item:hover {
      background: var(--cf-background);
    }
    .item:focus-visible,
    .switcher:focus-visible {
      outline: 2px solid var(--cf-ink);
      outline-offset: -2px;
    }
    .item.active {
      background: var(--cf-subtle);
      font-weight: 600;
    }
    .item svg {
      flex: none;
      width: 18px;
      height: 18px;
      fill: none;
      stroke: currentColor;
      stroke-linecap: round;
      stroke-linejoin: round;
      stroke-width: 2;
    }
  `,
})
export class AppNav {
  private readonly auth = inject(AuthService);
  private readonly currentFund = inject(CurrentFund);

  protected readonly fund = this.currentFund.context;
  protected readonly short = computed(() => fundShortName(this.fund()?.fundName ?? ''));
  protected readonly roleLabel = computed(() => {
    const role = this.fund()?.role;
    return role ? ROLE_DISPLAY[role].subtitle : '';
  });

  protected readonly groups = computed(() => {
    const context = this.fund();
    if (!this.auth.user() || !context) {
      return [];
    }
    const items = buildNavItems({ fundId: context.fundId, role: context.role, permissions: context.permissions });
    return NAV_GROUPS.map((name) => ({ name, items: items.filter((item: NavItem) => item.group === name) })).filter(
      (group) => group.items.length > 0,
    );
  });
}
