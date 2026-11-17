import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { FinancialScope, SCOPE_LABEL } from '../../models/labels';

/**
 * Nhãn phạm vi "Quỹ chung" / "Sự kiện: tên" – bắt buộc đặt cạnh mọi giao dịch, khoản thu, thông báo
 * để người dùng không nhầm hai phạm vi tài chính (yêu cầu mục 6.3, 7).
 */
@Component({
  selector: 'app-scope-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="cf-tag" [class.cf-tag--event]="scope() === 'Event'">{{ text() }}</span>`,
  styles: `
    .cf-tag--event {
      background: var(--cf-primary-tint);
      color: var(--cf-primary-dark);
    }
  `,
})
export class ScopeBadge {
  readonly scope = input.required<FinancialScope>();
  readonly eventName = input<string | null>(null);

  protected readonly text = computed(() =>
    this.scope() === 'Event' && this.eventName()
      ? `${SCOPE_LABEL.Event}: ${this.eventName()}`
      : SCOPE_LABEL[this.scope()],
  );
}
