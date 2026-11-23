import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { Observable, firstValueFrom, switchMap } from 'rxjs';

import { ConfirmDialog } from '../../core/services/confirm-dialog.service';
import { AppNotifications } from '../../core/services/notification.service';
import { AppState } from '../../shared/components/app-state/app-state';
import { ScopeBadge } from '../../shared/components/scope-badge/scope-badge';
import { ApiError } from '../../shared/models/api-error';
import { VnDatePipe } from '../../shared/pipes/vn-date.pipe';
import { VndMoneyPipe } from '../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../shared/state/load-state';
import { ChargesApi } from './charges.api';
import './charges.errors';
import { ChargeCollectList } from './charge-collect-list';
import { CHARGE_STATUS_LABEL, Charge } from './charges.models';
import { percentOf } from './charges.utils';

/** H-01b Theo dõi một khoản thu (FR-24): ai đã nộp / chưa nộp, thủ quỹ tích "đã thu" (Tiền mặt / Chuyển khoản), nhắc đóng, đóng hoặc hủy khoản thu. */
@Component({
  selector: 'app-charge-detail-page',
  imports: [RouterLink, ButtonComponent, AppState, ChargeCollectList, ScopeBadge, VndMoneyPipe, VnDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <a class="back" [routerLink]="['/funds', fundId(), 'charges']">← Khoản thu chung</a>

      @if (chargeKind(); as value) {
        <app-state [kind]="value" />
      } @else if (charge(); as c) {
        <header class="head">
          <div class="text">
            <div class="title-row">
              <h1>{{ c.title }}</h1>
              <app-scope-badge scope="General" />
              <span class="cf-tag" [class.cf-tag--success]="c.status === 'Open'">{{ chargeStatus(c) }}</span>
            </div>
            <p class="cf-muted sub">
              {{ c.amountPerPerson | vndMoney }}/người · {{ c.dueDate ? 'Hạn ' + (c.dueDate | vnDate) : 'Không có hạn – nhắc đóng đến khi mọi người đã nộp' }}@if (c.categoryName) { · Danh mục: {{ c.categoryName }} }@if (c.purpose) { · {{ c.purpose }} }
            </p>
          </div>
          <div class="actions">
            @if (c.status === 'Open') {
              <button kendoButton fillMode="outline" type="button" [disabled]="busy()" (click)="remind(c)">Nhắc tất cả người chưa nộp</button>
              <button kendoButton fillMode="outline" type="button" [disabled]="busy()" (click)="close(c)">Đóng khoản thu</button>
            }
            @if (c.status !== 'Cancelled') {
              <button kendoButton fillMode="outline" themeColor="error" type="button" [disabled]="busy()" (click)="cancel(c)">Hủy khoản thu</button>
            }
          </div>
        </header>

        @if (c.progress; as p) {
          <section class="cf-card stats" aria-label="Tiến độ thu">
            <dl class="figures">
              <div><dt>Phải thu</dt><dd>{{ p.assignedCount * c.amountPerPerson | vndMoney }}</dd></div>
              <div><dt>Đã thu</dt><dd class="cf-income">{{ p.collectedAmount | vndMoney }}</dd></div>
              <div><dt>Còn thiếu</dt><dd class="cf-expense">{{ missing(c) | vndMoney }}</dd></div>
            </dl>
            <div class="bar" role="progressbar" [attr.aria-valuenow]="percent(p.paidCount, p.assignedCount)" aria-valuemin="0" aria-valuemax="100">
              <span class="paid" [style.width.%]="percent(p.paidCount, p.assignedCount)"></span>
            </div>
            <p class="cf-muted legend">Đã nộp {{ p.paidCount }} · Chưa nộp {{ p.assignedCount - p.paidCount }}</p>
          </section>
        }

        <app-charge-collect-list [fundId]="fundId()" [chargeId]="c.id" [chargeOpen]="c.status === 'Open'" (changed)="refresh.update(bump)" />
      }
    </div>
  `,
  styles: `
    .page {
      max-width: 1200px;
    }
    .back {
      display: inline-block;
      margin-bottom: 8px;
      color: var(--cf-text-muted);
      text-decoration: none;
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 16px;
    }
    .title-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .title-row h1 {
      margin: 0;
    }
    .sub {
      margin: 4px 0 0;
    }
    .actions {
      display: flex;
      gap: 8px;
    }
    .stats {
      margin-bottom: 16px;
    }
    .figures {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 16px;
      margin: 0 0 12px;
    }
    .figures dt {
      color: var(--cf-text-muted);
      font-size: 13px;
    }
    .figures dd {
      margin: 4px 0 0;
      font-size: 22px;
      font-weight: 700;
      font-variant-numeric: tabular-nums;
    }
    .bar {
      display: flex;
      height: 8px;
      overflow: hidden;
      border-radius: 4px;
      background: var(--cf-subtle);
    }
    .paid {
      background: var(--cf-ink);
    }
    .legend {
      margin: 8px 0 0;
      font-size: 13px;
    }
  `,
})
export class ChargeDetailPage {
  private readonly api = inject(ChargesApi);
  private readonly confirm = inject(ConfirmDialog);
  private readonly notifications = inject(AppNotifications);

  readonly fundId = input.required<string>();
  readonly chargeId = input.required<string>();

  protected readonly refresh = signal(0);
  protected readonly busy = signal(false);
  protected readonly bump = (value: number): number => value + 1;

  // ---------- Khoản thu ----------

  private readonly chargeState = toSignal(
    toObservable(computed(() => ({ fundId: this.fundId(), chargeId: this.chargeId(), refresh: this.refresh() }))).pipe(
      switchMap(({ fundId, chargeId }) => toLoadState(this.api.get(fundId, chargeId))),
    ),
    { initialValue: LOADING as LoadState<Charge> },
  );
  private lastCharge: Charge | null = null;
  protected readonly charge = computed<Charge | null>(() => {
    const state = this.chargeState();
    if (state.status === 'ready') {
      this.lastCharge = state.data;
    }
    return state.status === 'error' ? null : this.lastCharge;
  });
  protected readonly chargeKind = computed(() => {
    const state = this.chargeState();
    return state.status === 'error' || (state.status === 'loading' && this.lastCharge === null) ? stateKind(state) : null;
  });

  // ---------- Hiển thị ----------

  protected percent(part: number, total: number): number {
    return percentOf(part, total);
  }

  protected chargeStatus(charge: Charge): string {
    return CHARGE_STATUS_LABEL[charge.status];
  }

  protected missing(charge: Charge): number {
    const p = charge.progress;
    return p ? Math.max(0, (p.assignedCount - p.paidCount) * charge.amountPerPerson) : 0;
  }

  // ---------- Hành động ----------

  protected close(charge: Charge): void {
    this.confirm
      .confirm({
        title: 'Đóng khoản thu',
        message: this.closeMessage(charge),
        confirmLabel: 'Đóng khoản thu',
      })
      .subscribe((confirmed) => confirmed && void this.run(() => this.api.close(this.fundId(), charge.id), 'Đã đóng khoản thu.'));
  }

  protected cancel(charge: Charge): void {
    this.confirm
      .confirm({
        title: 'Hủy khoản thu',
        message: `Hủy khoản “${charge.title}”? Chỉ hủy được khi chưa có ai nộp tiền. Thao tác này không hoàn tác.`,
        confirmLabel: 'Hủy khoản thu',
        danger: true,
      })
      .subscribe((confirmed) => confirmed && void this.run(() => this.api.cancel(this.fundId(), charge.id), 'Đã hủy khoản thu.'));
  }

  protected remind(charge: Charge): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    firstValueFrom(this.api.remind(this.fundId(), charge.id))
      .then((result) => this.notifications.success(`Đã gửi nhắc cho ${result.reminded} người chưa đóng.`))
      .catch((error: unknown) => this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.'))
      .finally(() => this.busy.set(false));
  }

  /** Cảnh báo khi còn người chưa nộp: đóng khoản thu thì không tích "đã thu" thêm được nữa. */
  private closeMessage(charge: Charge): string {
    const unpaid = charge.progress ? charge.progress.assignedCount - charge.progress.paidCount : 0;
    return unpaid > 0
      ? `Còn ${unpaid} người chưa nộp khoản “${charge.title}”. Sau khi đóng khoản thu bạn không tích “đã thu” thêm được nữa. Vẫn đóng khoản thu?`
      : `Đóng khoản “${charge.title}”? Mọi người đã nộp đủ.`;
  }

  private async run(action: () => Observable<unknown>, success: string): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      await firstValueFrom(action());
      this.notifications.success(success);
    } catch (error) {
      this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      this.refresh.update((value) => value + 1);
      this.busy.set(false);
    }
  }
}
