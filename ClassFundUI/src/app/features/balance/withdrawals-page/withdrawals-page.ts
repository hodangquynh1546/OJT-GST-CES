import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { CellTemplateDirective, ColumnComponent, GridComponent, NoRecordsTemplateDirective } from '@progress/kendo-angular-grid';
import { debounceTime, distinctUntilChanged, firstValueFrom, map, switchMap } from 'rxjs';

import { AppNotifications } from '../../../core/services/notification.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { ApiError } from '../../../shared/models/api-error';
import { PagedResult } from '../../../shared/models/paged-result';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { BalanceApi } from '../balance.api';
import '../balance.errors';
import { MemberBalance, MemberBalancesResult, WITHDRAWAL_STATUS_BADGE, Withdrawal, shortTime, withdrawError } from '../balance.models';

/**
 * B-02 · Rút số dư (FR-41), dành cho thủ quỹ: danh sách mọi học sinh kèm số dư, nút "Rút tiền" mở hộp thoại nhập số tiền
 * (≥ 10.000đ, bội của 1.000đ, không vượt số dư – BE kiểm lại dưới khóa dòng), và lịch sử các lần rút.
 * Thủ quỹ rút số dư của chính mình được ghi "Tự ghi nhận" trong nhật ký.
 */
@Component({
  selector: 'app-withdrawals-page',
  imports: [
    AppState,
    ButtonComponent,
    CellTemplateDirective,
    ColumnComponent,
    DialogActionsComponent,
    DialogComponent,
    GridComponent,
    NoRecordsTemplateDirective,
    ReactiveFormsModule,
    VndMoneyPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="head-t">
        <h1>Rút số dư</h1>
        <p class="cf-muted">
          Học sinh chỉ xem số dư của mình. Khi trả tiền mặt hoặc chuyển khoản cho học sinh, bấm “Rút tiền” và nhập đúng số tiền đã trả – hệ thống trừ số dư và ghi lịch sử.
        </p>
      </div>

      <section class="cf-card list" aria-label="Số dư của học sinh">
        <div class="card-h">
          <input class="search" type="search" placeholder="Tìm theo tên hoặc email" aria-label="Tìm học sinh" [formControl]="search" />
          @if (balances(); as data) {
            <span class="cf-muted small">Thủ quỹ đang giữ hộ <b class="held">{{ data.heldTotal | vndMoney }}</b></span>
          }
        </div>
        @if (kind(); as stateKindValue) {
          <app-state [kind]="stateKindValue" />
        } @else {
          <kendo-grid [data]="items()" [loading]="state().status === 'loading'">
            <kendo-grid-column title="Học sinh">
              <ng-template kendoGridCellTemplate let-row>
                <div class="strong">{{ row.fullName }}</div>
                <div class="cf-muted xs">{{ row.email }}</div>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Số dư" [width]="160" headerClass="num-head">
              <ng-template kendoGridCellTemplate let-row><span class="cf-money block strong-600">{{ row.balance | vndMoney }}</span></ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Thao tác" [width]="140" headerClass="num-head">
              <ng-template kendoGridCellTemplate let-row>
                <div class="row-actions">
                  <button kendoButton themeColor="primary" size="small" type="button" [disabled]="row.balance < minAmount" (click)="open(row)">Rút tiền</button>
                </div>
              </ng-template>
            </kendo-grid-column>
            <ng-template kendoGridNoRecordsTemplate>
              <app-state kind="empty" title="Không có học sinh nào." message="" />
            </ng-template>
          </kendo-grid>
        }
      </section>

      <section class="cf-card list" aria-label="Lịch sử rút">
        <div class="card-h"><h2>Lịch sử rút</h2></div>
        <kendo-grid [data]="history()">
          <kendo-grid-column title="Thời điểm" [width]="130">
            <ng-template kendoGridCellTemplate let-row><span class="cf-muted">{{ time(row.at) }}</span></ng-template>
          </kendo-grid-column>
          <kendo-grid-column title="Học sinh">
            <ng-template kendoGridCellTemplate let-row><span class="strong">{{ row.fullName }}</span></ng-template>
          </kendo-grid-column>
          <kendo-grid-column title="Số tiền" [width]="150" headerClass="num-head">
            <ng-template kendoGridCellTemplate let-row><span class="cf-money block strong-600">{{ row.amount | vndMoney }}</span></ng-template>
          </kendo-grid-column>
          <kendo-grid-column title="Người thực hiện" [width]="170">
            <ng-template kendoGridCellTemplate let-row><span class="cf-muted small">{{ row.handledByName ?? '' }}</span></ng-template>
          </kendo-grid-column>
          <kendo-grid-column title="Trạng thái" [width]="110">
            <ng-template kendoGridCellTemplate let-row>
              @let badge = badgeOf(row);
              <span class="cf-tag" [class]="'cf-tag--' + badge.tone">{{ badge.text }}</span>
            </ng-template>
          </kendo-grid-column>
          <ng-template kendoGridNoRecordsTemplate>
            <app-state kind="empty" title="Chưa có lần rút nào." message="" />
          </ng-template>
        </kendo-grid>
      </section>
    </div>

    @if (target(); as row) {
      <kendo-dialog [title]="'Rút tiền · ' + row.fullName" [width]="440" [maxWidth]="'calc(100vw - 32px)'" (close)="target.set(null)">
        <div class="stack">
          <p class="cf-muted">Số dư hiện có <b>{{ row.balance | vndMoney }}</b>. Nhập đúng số tiền bạn đã trả cho học sinh.</p>
          <div class="field">
            <label for="withdraw-amount">Số tiền rút (đ)</label>
            <input
              id="withdraw-amount"
              class="amount"
              type="number"
              inputmode="numeric"
              min="10000"
              step="1000"
              [formControl]="amount"
              [attr.aria-invalid]="amountError() ? 'true' : null"
              aria-describedby="withdraw-hint"
            />
            <span id="withdraw-hint" class="xs" [class.error]="amountError() && amount.dirty" [class.cf-muted]="!amountError() || !amount.dirty">
              {{ amountError() && amount.dirty ? amountError() : 'Tối thiểu 10.000đ, là bội của 1.000đ, không vượt quá số dư.' }}
            </span>
          </div>
          @if (serverError(); as message) {
            <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
          }
        </div>
        <kendo-dialog-actions layout="end">
          <button kendoButton type="button" (click)="target.set(null)">Hủy</button>
          <button kendoButton themeColor="primary" type="button" [disabled]="busy() || !!amountError()" (click)="withdraw(row)">Xác nhận rút</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }
  `,
  // Theo canvas B-02 (cf.css); màu dùng token --cf-*.
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 24px;
      max-width: var(--cf-content-max);
    }
    .head-t,
    .stack,
    .field {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .stack {
      gap: 16px;
    }
    .field {
      gap: 8px;
    }
    h1 {
      font-weight: 600;
      letter-spacing: -0.02em;
    }
    h2 {
      margin: 0;
      font-size: 16px;
      line-height: 24px;
      font-weight: 600;
    }
    p {
      margin: 0;
    }
    .small {
      font-size: 13px;
    }
    .xs {
      font-size: 12px;
    }
    .strong {
      font-weight: 500;
    }
    .strong-600 {
      font-weight: 600;
    }
    .block {
      display: block;
    }
    .error {
      color: var(--cf-danger-fg);
    }
    .held {
      font-variant-numeric: tabular-nums;
    }
    .list {
      padding: 0;
      overflow: hidden;
    }
    .card-h {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 16px;
    }
    .search,
    .amount {
      box-sizing: border-box;
      min-height: 40px;
      padding: 0 12px;
      border: 1px solid var(--cf-border-strong);
      border-radius: var(--cf-radius-lg);
      font: inherit;
    }
    .search {
      width: min(320px, 100%);
    }
    .row-actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
    }
  `,
})
export class WithdrawalsPage {
  private readonly api = inject(BalanceApi);
  private readonly route = inject(ActivatedRoute);
  private readonly notifications = inject(AppNotifications);

  protected readonly time = shortTime;
  protected readonly minAmount = 10_000;
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly amount = new FormControl<number | null>(null);

  private readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), { initialValue: '' });
  private readonly refresh = signal(0);
  private readonly term = toSignal(this.search.valueChanges.pipe(debounceTime(250), distinctUntilChanged()), { initialValue: '' });

  protected readonly target = signal<MemberBalance | null>(null);
  protected readonly busy = signal(false);
  protected readonly serverError = signal<string | null>(null);
  private readonly amountValue = toSignal(this.amount.valueChanges, { initialValue: null });

  protected readonly state = toSignal(
    toObservable(computed(() => ({ fundId: this.fundId(), search: this.term().trim(), refresh: this.refresh() }))).pipe(
      switchMap(({ fundId, search }) => toLoadState(this.api.balances(fundId, { search: search || undefined, pageSize: 100 }))),
    ),
    { initialValue: LOADING as LoadState<MemberBalancesResult> },
  );
  protected readonly balances = computed(() => {
    const current = this.state();
    return current.status === 'ready' ? current.data : null;
  });
  protected readonly items = computed(() => this.balances()?.items ?? []);
  protected readonly kind = computed(() => stateKind(this.state(), () => false));

  private readonly historyState = toSignal(
    toObservable(computed(() => ({ fundId: this.fundId(), refresh: this.refresh() }))).pipe(
      switchMap(({ fundId }) => toLoadState(this.api.history(fundId, { pageSize: 50 }))),
    ),
    { initialValue: LOADING as LoadState<PagedResult<Withdrawal>> },
  );
  protected readonly history = computed(() => {
    const current = this.historyState();
    return current.status === 'ready' ? current.data.items : [];
  });

  protected readonly amountError = computed(() => {
    const row = this.target();
    return row ? withdrawError(this.amountValue(), row.balance) : null;
  });

  protected badgeOf(row: Withdrawal): { text: string; tone: string } {
    return WITHDRAWAL_STATUS_BADGE[row.status];
  }

  protected open(row: MemberBalance): void {
    this.amount.reset(null);
    this.serverError.set(null);
    this.target.set(row);
  }

  protected async withdraw(row: MemberBalance): Promise<void> {
    this.amount.markAsDirty();
    const value = this.amount.value;
    if (this.busy() || value === null || withdrawError(value, row.balance)) {
      return;
    }

    this.busy.set(true);
    this.serverError.set(null);
    try {
      await firstValueFrom(this.api.withdraw(this.fundId(), row.userId, value));
      this.target.set(null);
      this.notifications.success(`Đã ghi nhận rút ${new Intl.NumberFormat('vi-VN').format(value)} đ của ${row.fullName}.`);
      this.refresh.update((count) => count + 1);
    } catch (error) {
      this.serverError.set(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
      this.refresh.update((count) => count + 1);
    } finally {
      this.busy.set(false);
    }
  }
}
