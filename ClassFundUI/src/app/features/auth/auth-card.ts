import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Khung A-02 / A-03 (canvas "ClassFund UI Design"): thẻ trắng bo 12 px giữa trang, không có logo. */
@Component({
  selector: 'app-auth-card',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="page">
      <section class="card" aria-labelledby="auth-title">
        @if (backLink(); as link) {
          <a class="back" [routerLink]="link.url">← {{ link.label }}</a>
        }
        <h1 id="auth-title">{{ title() }}</h1>
        @if (subtitle(); as text) {
          <p class="subtitle">{{ text }}</p>
        }
        <ng-content />
      </section>
    </main>
  `,
  styles: `
    :host {
      --kendo-color-primary: var(--cf-ink);
      --kendo-color-primary-hover: var(--cf-ink-hover);
      --kendo-color-primary-active: var(--cf-ink-hover);
      --kendo-color-primary-emphasis: var(--cf-ink);
      --kendo-border-radius-md: var(--cf-radius-lg);
    }
    .page {
      display: flex;
      align-items: flex-start;
      justify-content: center;
      box-sizing: border-box;
      min-height: 100dvh;
      padding: 120px 16px 48px;
    }
    .card {
      box-sizing: border-box;
      width: 100%;
      max-width: 440px;
      padding: 32px;
      border: 1px solid var(--cf-border);
      border-radius: 12px;
      background: var(--cf-surface);
    }
    .back {
      display: inline-block;
      margin-bottom: 12px;
      color: var(--cf-text-muted);
      font-size: 13px;
      text-decoration: none;
    }
    .back:hover {
      color: var(--cf-text);
    }
    h1 {
      font-size: 24px;
      line-height: 32px;
    }
    .subtitle {
      margin: 6px 0 24px;
      color: var(--cf-text-muted);
    }
  `,
})
export class AuthCard {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
  readonly backLink = input<{ url: string; label: string }>();
}
