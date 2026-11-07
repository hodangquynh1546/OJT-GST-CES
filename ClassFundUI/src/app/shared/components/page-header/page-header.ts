import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Tiêu đề trang; nút hành động đặt trong nội dung của component. */
@Component({
  selector: 'app-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="text">
      <h1>{{ title() }}</h1>
      @if (subtitle(); as text) {
        <span class="cf-muted">{{ text }}</span>
      }
    </div>
    <ng-content />
  `,
  styles: `
    :host {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
    }
    .text {
      display: flex;
      flex: 1 1 260px;
      flex-direction: column;
      min-width: 0;
    }
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
}
