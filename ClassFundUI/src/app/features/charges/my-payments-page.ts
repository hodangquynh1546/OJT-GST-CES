import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { switchMap } from 'rxjs';

import { AppNotifications } from '../../core/services/notification.service';
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
import { PayDialog, PayTarget } from './pay-dialog';

/** H-03 Khoản cần đóng của tôi (FR-20, FR-23): nộp tiền, theo dõi trạng thái xác nhận, nộp lại khi bị từ chối. */
@Component({
  selector: 'app-my-payments-page',
  imports: [ButtonComponent, PageHeader, ScopeBadge, AppState, PayDialog, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <app-page-header title="Khoản cần đóng của tôi" subtitle="Các khoản thu chung của lớp và tình trạng đóng của bạn." />

      @if (kind(); as value) {
        <app-state [kind]="value" [title]="value === 'empty' ? 'Bạn chưa có khoản nào cần đóng' : undefined" />
      } @else {
        <section class="stats" aria-label="Tóm tắt">
          <div class="cf-card stat"><span class="cf-muted">Còn phải đóng</span><b class="big">{{ owed() | vndMoney }}</b><span class="cf-muted">{{ owedCount() }} khoản</span></div>
          <div class="cf-card stat"><span class="cf-muted">Chờ thủ quỹ xác nhận</span><b class="big await">{{ awaiting() | vndMoney }}</b><span class="cf-muted">{{ awaitingCount() }} khoản</span></div>
          <div class="cf-card stat"><span class="cf-muted">Đã đóng</span><b class="big cf-income">{{ paid() | vndMoney }}</b><span class="cf-muted">{{ paidCount() }} khoản</span></div>
        </section>

        <h2>Cần đóng</h2>
        <section class="cf-card list" aria-label="Cần đóng">
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
              <span class="cf-tag" [class.cf-tag--warning]="item.status === 'AwaitingConfirmation'" [class.cf-tag--danger]="rejected(item)">{{ statusText(item) }}</span>
              @if (item.status === 'Unpaid' && item.chargeStatus === 'Open') {
                <button kendoButton themeColor="primary" type="button" (click)="pay(item)">{{ rejected(item) ? 'Gửi lại' : 'Nộp tiền' }}</button>
              }
            </article>
          } @empty {
            <p class="cf-muted none">Bạn không còn khoản nào cần đóng.</p>
          }
        </section>

        <h2>Lịch sử đã đóng</h2>
        <section class="cf-card list" aria-label="Lịch sử đã đóng">
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
              <span class="cf-tag cf-tag--success">Đã xác nhận</span>
              @if (item.isSelfDeclared) { <span class="cf-tag cf-tag--warning">Tự ghi nhận</span> }
            </article>
          } @empty {
            <p class="cf-muted none">Chưa có khoản nào đã đóng.</p>
          }
        </section>
      }
    </div>

    @if (paying(); as target) {
      <app-pay-dialog [fundId]="fundId()" [target]="target" (closed)="paying.set(null)" (paid)="onPaid($event)" />
    }
  `,
  styles: `
    .page {
      max-width: 1200px;
    }
    .stats {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
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
    .await {
      color: var(--cf-warning-fg);
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
  private readonly notifications = inject(AppNotifications);

  readonly fundId = input.required<string>();

  private readonly today = todayInVietnam();
  private readonly refresh = signal(0);
  protected readonly paying = signal<PayTarget | null>(null);

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
  protected readonly awaitingCount = computed(() => this.items().filter((item) => item.status === 'AwaitingConfirmation').length);
  protected readonly awaiting = computed(() => this.sum('AwaitingConfirmation'));
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

  protected rejected(item: MyAssignmentItem): boolean {
    return item.status === 'Unpaid' && item.paymentStatus === 'Rejected';
  }

  protected statusText(item: MyAssignmentItem): string {
    if (this.rejected(item)) {
      return 'Bị từ chối';
    }
    return item.status === 'AwaitingConfirmation' ? 'Chờ xác nhận' : 'Chưa đóng';
  }

  protected note(item: MyAssignmentItem): string {
    if (this.rejected(item)) {
      return `Bị từ chối: “${item.rejectionReason ?? ''}” – vui lòng gửi lại`;
    }
    if (item.status === 'Paid') {
      const method = item.method ? ` · ${PAYMENT_METHOD_LABEL[item.method]}` : '';
      return `Hạn ${new VnDatePipe().transform(item.dueDate)}${method}`;
    }
    if (item.status === 'AwaitingConfirmation') {
      const method = item.method ? ` · ${PAYMENT_METHOD_LABEL[item.method]}` : '';
      return `Đã gửi${method} · chờ thủ quỹ xác nhận`;
    }
    const due = new VnDatePipe().transform(item.dueDate);
    const purpose = item.purpose ? ` · ${item.purpose}` : '';
    return item.chargeStatus === 'Open'
      ? `Hạn ${due} · ${dueText(daysUntil(item.dueDate, this.today))}${purpose}`
      : `Khoản thu đã đóng · hạn ${due}`;
  }

  protected pay(item: MyAssignmentItem): void {
    this.paying.set({ assignmentId: item.assignmentId, title: item.title, amountDue: item.amountDue });
  }

  protected onPaid(result: { selfDeclared: boolean }): void {
    this.paying.set(null);
    this.refresh.update((value) => value + 1);
    this.notifications.success(result.selfDeclared ? 'Đã tự ghi nhận khoản đóng của bạn.' : 'Đã gửi. Thủ quỹ sẽ xác nhận khoản đóng của bạn.');
  }
}
