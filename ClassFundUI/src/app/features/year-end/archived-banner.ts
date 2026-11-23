import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';

import { CurrentFund } from '../../core/services/current-fund.service';
import { FundStatusStore } from './fund-status.store';

/** Banner trên mọi trang của quỹ đã tổng kết cuối năm (BR-37): nói rõ những gì vẫn làm được. */
@Component({
  selector: 'app-archived-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (store.archived()) {
      <div class="cf-alert cf-alert--warning banner" role="status">
        <b>Quỹ đã tổng kết cuối năm – chỉ xem.</b>
        Không tạo khoản thu, giao dịch hay sự kiện mới. Bạn vẫn xem được báo cáo, rút số dư cá nhân và thủ quỹ vẫn xác nhận hoàn tiền.
      </div>
    }
  `,
  styles: `
    .banner {
      margin-bottom: 16px;
    }
  `,
})
export class ArchivedBanner {
  private readonly currentFund = inject(CurrentFund);
  protected readonly store = inject(FundStatusStore);

  constructor() {
    effect(() => {
      const fundId = this.currentFund.context()?.fundId;
      if (fundId) {
        void this.store.refresh(fundId);
      } else {
        this.store.reset();
      }
    });
  }
}
