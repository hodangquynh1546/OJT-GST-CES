import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { AuthService } from '../auth/auth.service';
import { CurrentFund } from '../services/current-fund.service';
import { buildNavItems } from './nav-items';
import { ROLE_DISPLAY } from './role-display';

@Component({
  selector: 'app-nav',
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="nav" aria-label="Điều hướng chính">
      @for (item of items(); track item.id) {
        <a
          class="item"
          [class.desktop-only]="!item.mobile"
          [class.mobile-last]="item.mobileLast"
          [routerLink]="item.link"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          [routerLinkActiveOptions]="{ exact: item.exact }"
          [attr.title]="item.label"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true"><path [attr.d]="item.icon" /></svg>
          <span class="label" [class.desktop-only]="item.mobileLabel">{{ item.label }}</span>
          @if (item.mobileLabel; as short) {
            <span class="label mobile-only">{{ short }}</span>
          }
        </a>
      }
      <a class="item mobile-only account" routerLink="/account" routerLinkActive="active" ariaCurrentWhenActive="page">
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM5 21a7 7 0 0 1 14 0" />
        </svg>
        <span class="label">Tài khoản</span>
      </a>
      @if (footnote(); as text) {
        <p class="footnote">{{ text }}</p>
      }
    </nav>
  `,
  styles: `
    .nav {
      display: flex;
      flex-direction: column;
      gap: 2px;
      box-sizing: border-box;
      width: var(--sf-nav-width);
      height: 100%;
      padding: 12px 8px;
      border-right: 1px solid var(--sf-border);
      background: var(--sf-surface);
    }
    .item {
      display: flex;
      align-items: center;
      gap: 10px;
      min-height: 40px;
      padding: 0 10px;
      border-radius: 6px;
      color: var(--sf-text);
      text-decoration: none;
    }
    .item:hover {
      background: var(--sf-background);
    }
    .item.active {
      background: var(--sf-primary-tint);
      color: var(--sf-primary-dark);
      font-weight: 600;
    }
    svg {
      flex: none;
      width: 20px;
      height: 20px;
      fill: none;
      stroke: currentColor;
      stroke-linecap: round;
      stroke-linejoin: round;
      stroke-width: 1.8;
    }
    .footnote {
      margin: auto 10px 4px;
      color: var(--sf-text-muted);
      font-size: 12px;
      line-height: 16px;
    }
    .mobile-only {
      display: none;
    }
    @media (min-width: 768px) and (max-width: 1023px) {
      .nav {
        width: var(--sf-nav-width-compact);
      }
      .label,
      .footnote {
        display: none;
      }
      .item {
        justify-content: center;
        padding: 0;
      }
    }
    @media (max-width: 767px) {
      .nav {
        flex-direction: row;
        width: 100%;
        height: var(--sf-tabbar-height);
        padding: 4px;
        border-top: 1px solid var(--sf-border);
        border-right: 0;
      }
      .item {
        flex: 1 1 0;
        flex-direction: column;
        justify-content: center;
        gap: 2px;
        min-width: 0;
        min-height: 44px;
        padding: 0 2px;
      }
      .label {
        overflow: hidden;
        max-width: 100%;
        font-size: 11px;
        line-height: 14px;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .desktop-only,
      .footnote {
        display: none;
      }
      .mobile-only {
        display: flex;
      }
      .mobile-last {
        order: 1;
      }
      .account {
        order: 2;
      }
    }
  `,
})
export class AppNav {
  private readonly auth = inject(AuthService);
  private readonly currentFund = inject(CurrentFund);

  protected readonly items = computed(() => {
    const user = this.auth.user();
    return user
      ? buildNavItems({
          kind: user.kind,
          fundId: this.currentFund.membership()?.fundId ?? null,
          role: this.currentFund.membership()?.role ?? null,
          permissions: this.currentFund.permissions(),
        })
      : [];
  });

  protected readonly footnote = computed(() => {
    const role = this.currentFund.membership()?.role;
    return role ? ROLE_DISPLAY[role].footnote : null;
  });
}
