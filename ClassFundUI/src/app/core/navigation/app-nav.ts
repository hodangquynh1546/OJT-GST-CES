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
          [routerLink]="item.link"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          [routerLinkActiveOptions]="{ exact: item.exact }"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true"><path [attr.d]="item.icon" /></svg>
          <span class="label">{{ item.label }}</span>
        </a>
      }
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
      width: var(--cf-nav-width);
      height: 100%;
      padding: 12px 8px;
      border-right: 1px solid var(--cf-border);
      background: var(--cf-surface);
    }
    .item {
      display: flex;
      align-items: center;
      gap: 10px;
      min-height: 40px;
      padding: 0 10px;
      border-radius: var(--cf-radius-md);
      color: var(--cf-text);
      text-decoration: none;
      white-space: nowrap;
    }
    .item:hover {
      background: var(--cf-background);
    }
    .item:focus-visible {
      outline: 2px solid var(--cf-primary);
      outline-offset: -2px;
    }
    .item.active {
      background: var(--cf-primary-tint);
      color: var(--cf-primary-dark);
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
      color: var(--cf-text-muted);
      font-size: 12px;
      line-height: 16px;
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
