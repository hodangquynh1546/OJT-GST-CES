import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Khung trang khách A-01, A-02, A-03 (design-system.md mục 5): không header / nav, thẻ giữa 400–440 px, cách trên 48 px. */
@Component({
  selector: 'app-guest-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="brand">
      <span class="logo" aria-hidden="true">
        <svg width="22" height="22" viewBox="0 0 24 24">
          <path d="M4 7h16v12H4zM4 7l2-3h12l2 3M9 12h6" fill="none" stroke="currentColor" stroke-width="2"
            stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </span>
      <b>ClassFund</b>
    </div>
    <div class="card" [style.width.px]="width()"><ng-content /></div>
    <ng-content select="[guest-after]" />
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 20px;
      box-sizing: border-box;
      min-height: 100vh;
      padding: 48px 16px;
      background: var(--cf-background);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 10px;
      font-size: 22px;
    }
    .logo {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 40px;
      height: 40px;
      border-radius: 9px;
      background: var(--cf-primary);
      color: var(--cf-surface);
    }
    .card {
      display: flex;
      flex-direction: column;
      gap: 16px;
      box-sizing: border-box;
      padding: var(--guest-card-padding, 28px);
      border: 1px solid var(--cf-border);
      border-radius: var(--guest-card-radius, 10px);
      background: var(--cf-surface);
    }
  `,
})
export class GuestCard {
  /** Board Login: 400 px; TeacherRegister, ChangePasswordRequired: 440 px. */
  readonly width = input(440);
}
