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
      gap: 4px;
      box-sizing: border-box;
      width: var(--cf-nav-width);
      height: 100%;
      padding: 16px 12px;
      border-right: 1px solid var(--cf-border);
      background: var(--cf-surface);
    }
    .item {
      display: flex;
      align-items: center;
      gap: 10px;
      min-height: 44px;
      padding: 0 12px;
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
      width: 18px;
      height: 18px;
      fill: none;
      stroke: currentColor;
      stroke-linecap: round;
      stroke-linejoin: round;
      stroke-width: 2;
    }
    .footnote {
      margin: auto 0 0;
      padding: 12px;
      border-top: 1px solid var(--cf-subtle);
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
          fundId: this.currentFund.context()?.fundId ?? null,
          role: this.currentFund.context()?.role ?? null,
          permissions: this.currentFund.context()?.permissions ?? [],
        })
      : [];
  });

  protected readonly footnote = computed(() => {
    const role = this.currentFund.context()?.role;
    return role ? ROLE_DISPLAY[role].footnote : null;
  });
}
