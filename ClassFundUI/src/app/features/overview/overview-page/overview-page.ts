import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SVGIconComponent } from '@progress/kendo-angular-icons';
import { plusIcon } from '@progress/kendo-svg-icons';
import { map, switchMap } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { Permissions } from '../../../core/auth/permissions';
import { CurrentFund } from '../../../core/services/current-fund.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { ScopeBadge } from '../../../shared/components/scope-badge/scope-badge';
import { PAYMENT_METHOD_LABEL } from '../../../shared/models/labels';
import { VnDatePipe } from '../../../shared/pipes/vn-date.pipe';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { MY_RESPONSE_BADGE, dateBox } from '../../events/events.models';
import { ReportsApi } from '../../reports/reports.api';
import { FundOverview, OverviewEntry, OverviewEvent, percent } from '../../reports/reports.models';

/**
 * S-01 · Tổng quan quỹ chung (FR-48) theo canvas Overview (thủ quỹ) và StuOverview (học sinh).
 * Số liệu chỉ gồm quỹ chung, không gồm tiền sự kiện (BR-02).
 */
@Component({
  selector: 'app-overview-page',
  imports: [AppState, NgTemplateOutlet, RouterLink, SVGIconComponent, ScopeBadge, VnDatePipe, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './overview-page.html',
  styleUrl: './overview-page.scss',
})
export class OverviewPage {
  private readonly api = inject(ReportsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly currentFund = inject(CurrentFund);
  private readonly auth = inject(AuthService);

  protected readonly plus = plusIcon;
  protected readonly date = dateBox;
  protected readonly percent = percent;
  protected readonly manager = computed(() => this.currentFund.can(Permissions.LedgerManage));
  protected readonly canCreateCharge = computed(() => this.currentFund.can(Permissions.ChargeManage));

  protected readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), { initialValue: '' });

  protected readonly state = toSignal(
    this.route.paramMap.pipe(switchMap((params) => toLoadState(this.api.overview(params.get('fundId') ?? '')))),
    { initialValue: LOADING as LoadState<FundOverview> },
  );
  protected readonly kind = computed(() => stateKind(this.state(), () => false));
  protected readonly data = computed(() => {
    const current = this.state();
    return current.status === 'ready' ? current.data : null;
  });

  /** "Chào Thu Hà": bỏ họ (từ đầu) với tên từ ba chữ trở lên. */
  protected readonly greeting = computed(() => {
    const words = (this.auth.user()?.fullName ?? '').trim().split(/\s+/).filter(Boolean);
    return words.length >= 3 ? words.slice(1).join(' ') : words.join(' ');
  });

  /** Dòng phụ dưới tên giao dịch như canvas: "Chuyển khoản · đã xác nhận", "Trần Quang Huy · 2 chứng từ". */
  protected entryNote(entry: OverviewEntry): string {
    if (entry.source === 'Payment') {
      const method = entry.paymentMethod ? PAYMENT_METHOD_LABEL[entry.paymentMethod] : 'Thanh toán';
      return entry.isSelfDeclared ? `${method} · chưa đối chiếu với thủ quỹ khác` : `${method} · đã xác nhận`;
    }
    return entry.attachmentCount > 0 ? `${entry.performedByName} · ${entry.attachmentCount} chứng từ` : entry.performedByName;
  }

  /** Dòng phụ thẻ sự kiện: thủ quỹ thấy tiến độ đóng; học sinh thấy câu trả lời của mình. */
  protected eventNote(event: OverviewEvent): string {
    if (!this.manager() && event.myResponse) {
      return MY_RESPONSE_BADGE[event.myResponse].text;
    }
    return event.status === 'Open' ? `${event.participantCount} tham gia · ${event.paidInFullCount}/${event.participantCount} đã đóng` : 'Đã kết thúc';
  }

  protected shortDate(value: string): string {
    const [, month, day] = value.split('-');
    return `${day}/${month}`;
  }
}
