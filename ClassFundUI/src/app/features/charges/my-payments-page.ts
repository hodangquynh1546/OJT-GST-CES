import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap } from 'rxjs';

import { AppState } from '../../shared/components/app-state/app-state';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { ScopeBadge } from '../../shared/components/scope-badge/scope-badge';
import { PAYMENT_METHOD_LABEL } from '../../shared/models/labels';
import { VnDatePipe } from '../../shared/pipes/vn-date.pipe';
import { VndMoneyPipe } from '../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../shared/state/load-state';
import { ChargesApi } from './charges.api';
import './charges.errors';
import { MyAssignmentItem } from './charges.models';
import { daysUntil, dueText, todayInVietnam } from './charges.utils';

/** H-03 Khoản cần đóng của tôi (FR-20, FR-23), chỉ xem: Đã nộp / Chưa nộp. Thủ quỹ là người tích "đã thu". */
@Component({
  selector: 'app-my-payments-page',
  imports: [PageHeader, ScopeBadge, AppState, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <app-page-header title="Khoản cần đóng của tôi" subtitle="Các khoản thu của lớp và tình trạng nộp của bạn. Khi nộp tiền, hãy đưa cho thủ quỹ – thủ quỹ sẽ ghi nhận cho bạn." />

      @if (kind(); as value) {
        <app-state [kind]="value" [title]="value === 'empty' ? 'Bạn chưa có khoản nào cần đóng' : undefined" />
      } @else {
        <section class="stats" aria-label="Tóm tắt">
          <div class="cf-card stat"><span class="cf-muted">Chưa nộp</span><b class="big">{{ owed() | vndMoney }}</b><span class="cf-muted">{{ owedCount() }} khoản</span></div>
          <div class="cf-card stat"><span class="cf-muted">Đã nộp</span><b class="big cf-income">{{ paid() | vndMoney }}</b><span class="cf-muted">{{ paidCount() }} khoản</span></div>
        </section>

        <h2>Chưa nộp</h2>
        <section class="cf-card list" aria-label="Chưa nộp">
          @for (item of open(); track item.assignmentId) {
            <article class="item">
              <div class="main">
                <div class="head">
                  <b>{{ item.title }}</b>
                  <app-scope-badge [scope]="item.eventId ? 'Event' : 'General'" [eventName]="item.eventName" />
                </div>
                <span class="cf-muted note">{{ note(item) }}</span>
              </div>
              <b class="cf-money amount">{{ item.amountDue | vndMoney }}</b>
              <span class="cf-tag cf-tag--warning">Chưa nộp</span>
            </article>
          } @empty {
            <p class="cf-muted none">Bạn đã nộp đủ mọi khoản.</p>
          }
        </section>

        <h2>Đã nộp</h2>
        <section class="cf-card list" aria-label="Đã nộp">
          @for (item of done(); track item.assignmentId) {
            <article class="item">
              <div class="main">
                <div class="head">
                  <b>{{ item.title }}</b>
                  <app-scope-badge [scope]="item.eventId ? 'Event' : 'General'" [eventName]="item.eventName" />
                </div>
                <span class="cf-muted note">{{ note(item) }}</span>
              </div>
              <b class="cf-money amount">{{ item.amountDue | vndMoney }}</b>
              <span class="cf-tag cf-tag--success">Đã nộp</span>
              @if (item.isSelfDeclared) { <span class="cf-tag cf-tag--warning">Tự ghi nhận</span> }
            </article>
          } @empty {
            <p class="cf-muted none">Chưa có khoản nào đã nộp.</p>
          }
        </section>
      }
    </div>

  `,
  styles: `
    .page {
      max-width: 1200px;
    }
    .stats {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 16px;
      margin-bottom: 24px;
    }
    .stat {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .big {
      font-size: 24px;
      font-variant-numeric: tabular-nums;
    }
    h2 {
      margin: 0 0 12px;
      font-size: 16px;
      line-height: 24px;
    }
    .list {
      margin-bottom: 24px;
      padding: 0;
    }
    .item {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 14px 16px;
      border-top: 1px solid var(--cf-border);
    }
    .item:first-child {
      border-top: 0;
    }
    .main {
      display: flex;
      flex: 1;
      flex-direction: column;
      gap: 4px;
      min-width: 0;
    }
    .head {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .note {
      font-size: 13px;
    }
    .amount {
      min-width: 110px;
    }
    .none {
      margin: 20px 0;
      text-align: center;
    }
  `,
})
export class MyPaymentsPage {
  private readonly api = inject(ChargesApi);

  readonly fundId = input.required<string>();

  private readonly today = todayInVietnam();
  private readonly refresh = signal(0);

  private readonly state = toSignal(
    toObservable(computed(() => ({ fundId: this.fundId(), refresh: this.refresh() }))).pipe(
      switchMap(({ fundId }) => toLoadState(this.api.mine(fundId))),
    ),
    { initialValue: LOADING as LoadState<MyAssignmentItem[]> },
  );

  private readonly items = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data : [];
  });
  protected readonly open = computed(() => this.items().filter((item) => item.status !== 'Paid'));
  protected readonly done = computed(() => this.items().filter((item) => item.status === 'Paid'));
  protected readonly owedCount = computed(() => this.items().filter((item) => item.status === 'Unpaid').length);
  protected readonly owed = computed(() => this.sum('Unpaid'));
  protected readonly paidCount = computed(() => this.done().length);
  protected readonly paid = computed(() => this.sum('Paid'));

  protected readonly kind = computed(() => {
    const state = this.state();
    return stateKind(state, (data) => data.length === 0);
  });

  private sum(status: MyAssignmentItem['status']): number {
    return this.items()
      .filter((item) => item.status === status)
      .reduce((total, item) => total + item.amountDue, 0);
  }

  protected note(item: MyAssignmentItem): string {
    const due = item.dueDate ? `Hạn ${new VnDatePipe().transform(item.dueDate)}` : 'Không có hạn';
    if (item.status === 'Paid') {
      const method = item.method ? ` · ${PAYMENT_METHOD_LABEL[item.method]}` : '';
      return `${due}${method}`;
    }
    const purpose = item.purpose ? ` · ${item.purpose}` : '';
    if (item.chargeStatus !== 'Open') {
      return `Khoản thu đã đóng · ${due}`;
    }
    return item.dueDate ? `${due} · ${dueText(daysUntil(item.dueDate, this.today))}${purpose}` : `${due}${purpose}`;
  }
}
