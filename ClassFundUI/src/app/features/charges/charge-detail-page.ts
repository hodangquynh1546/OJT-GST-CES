import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { CellTemplateDirective, ColumnComponent, GridComponent, NoRecordsTemplateDirective, PageChangeEvent } from '@progress/kendo-angular-grid';
import { Observable, debounceTime, distinctUntilChanged, firstValueFrom, map, switchMap } from 'rxjs';

import { ROLE_DISPLAY } from '../../core/navigation/role-display';
import { ConfirmDialog } from '../../core/services/confirm-dialog.service';
import { AppNotifications } from '../../core/services/notification.service';
import { AppState } from '../../shared/components/app-state/app-state';
import { ScopeBadge } from '../../shared/components/scope-badge/scope-badge';
import { ApiError } from '../../shared/models/api-error';
import { ASSIGNMENT_STATUS_LABEL, AssignmentStatus, PAYMENT_METHOD_LABEL } from '../../shared/models/labels';
import { PagedResult } from '../../shared/models/paged-result';
import { VnDatePipe } from '../../shared/pipes/vn-date.pipe';
import { VndMoneyPipe } from '../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../shared/state/load-state';
import { ChargesApi } from './charges.api';
import './charges.errors';
import { AssignmentRow, CHARGE_STATUS_LABEL, Charge } from './charges.models';
import { percentOf } from './charges.utils';

const PAGE_SIZE = 20;

type Filter = 'All' | AssignmentStatus;

const FILTERS: readonly { value: Filter; label: string }[] = [
  { value: 'All', label: 'Tất cả' },
  { value: 'Unpaid', label: 'Chưa đóng' },
  { value: 'AwaitingConfirmation', label: 'Chờ xác nhận' },
  { value: 'Paid', label: 'Đã đóng' },
];

/** H-01b Theo dõi một khoản thu (FR-24): ai đã đóng / chưa đóng, xác nhận nhanh, đóng hoặc hủy khoản thu. */
@Component({
  selector: 'app-charge-detail-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonComponent,
    GridComponent,
    ColumnComponent,
    CellTemplateDirective,
    NoRecordsTemplateDirective,
    AppState,
    ScopeBadge,
    VndMoneyPipe,
    VnDatePipe,
  ],
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
              {{ c.amountPerPerson | vndMoney }}/người · Hạn {{ c.dueDate | vnDate }}@if (c.purpose) { · {{ c.purpose }} }
            </p>
          </div>
          <div class="actions">
            @if (c.status === 'Open') {
              <button kendoButton fillMode="outline" type="button" [disabled]="busy()" (click)="remind(c)">Gửi nhắc người chưa đóng</button>
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
              <div><dt>Đã thu (đã xác nhận)</dt><dd class="cf-income">{{ p.collectedAmount | vndMoney }}</dd></div>
              <div><dt>Chờ xác nhận</dt><dd class="await">{{ p.awaitingCount * c.amountPerPerson | vndMoney }}</dd></div>
              <div><dt>Còn thiếu</dt><dd class="cf-expense">{{ missing(c) | vndMoney }}</dd></div>
            </dl>
            <div class="bar" role="progressbar" [attr.aria-valuenow]="percent(p.paidCount, p.assignedCount)" aria-valuemin="0" aria-valuemax="100">
              <span class="paid" [style.width.%]="percent(p.paidCount, p.assignedCount)"></span>
              <span class="pending" [style.width.%]="percent(p.awaitingCount, p.assignedCount)"></span>
            </div>
            <p class="cf-muted legend">
              Đã đóng {{ p.paidCount }} · Chờ xác nhận {{ p.awaitingCount }} · Chưa đóng {{ p.assignedCount - p.paidCount - p.awaitingCount }}
            </p>
          </section>
        }

        <section class="cf-card table" aria-label="Danh sách học sinh">
          <div class="toolbar">
            <div class="tabs" role="tablist" aria-label="Lọc theo tình trạng">
              @for (item of filters; track item.value) {
                <button type="button" role="tab" [class.active]="filter() === item.value" [attr.aria-selected]="filter() === item.value" (click)="setFilter(item.value)">
                  {{ item.label }}
                </button>
              }
            </div>
            <input class="search" type="search" aria-label="Tìm học sinh" placeholder="Tên hoặc email" [formControl]="searchControl" />
          </div>

          @if (rowsKind(); as value) {
            <app-state [kind]="value" />
          }
          @if (rows(); as data) {
            <kendo-grid
              [data]="data.items"
              [pageSize]="pageSize"
              [skip]="(pageNumber() - 1) * pageSize"
              [pageable]="data.total > pageSize ? { info: true, previousNext: true } : false"
              [loading]="reloading()"
              (pageChange)="onPageChange($event)"
            >
              <kendo-grid-column title="Học sinh" [width]="300">
                <ng-template kendoGridCellTemplate let-row>
                  <div class="who">
                    <span class="name"><b>{{ row.fullName }}</b>@if (row.role !== 'Student') { <span class="role">{{ roleLabel(row) }}</span> }</span>
                    <span class="cf-muted">{{ row.email }}</span>
                  </div>
                </ng-template>
              </kendo-grid-column>
              <kendo-grid-column title="Phải đóng" [width]="120">
                <ng-template kendoGridCellTemplate let-row><span class="cf-money">{{ row.amountDue | vndMoney }}</span></ng-template>
              </kendo-grid-column>
              <kendo-grid-column title="Trạng thái" [width]="210">
                <ng-template kendoGridCellTemplate let-row>
                  <span class="cf-tag" [class.cf-tag--success]="row.status === 'Paid'" [class.cf-tag--warning]="row.status === 'AwaitingConfirmation'">{{ assignmentLabel(row.status) }}</span>
                  @if (row.isSelfDeclared) { <span class="cf-tag cf-tag--warning self">Tự ghi nhận</span> }
                </ng-template>
              </kendo-grid-column>
              <kendo-grid-column title="Hình thức" [width]="130">
                <ng-template kendoGridCellTemplate let-row>{{ row.method ? methodLabel(row) : '–' }}</ng-template>
              </kendo-grid-column>
              <kendo-grid-column title="Ghi nhận">
                <ng-template kendoGridCellTemplate let-row><span class="cf-muted">{{ recorded(row) }}</span></ng-template>
              </kendo-grid-column>
              <kendo-grid-column title="Thao tác" [width]="200">
                <ng-template kendoGridCellTemplate let-row>
                  @if (row.status === 'Unpaid' && charge()?.status === 'Open') {
                    <button kendoButton size="small" fillMode="outline" type="button" [disabled]="busy()" (click)="recordCash(row)">Ghi nhận tiền mặt</button>
                  } @else if (row.status === 'AwaitingConfirmation' && row.paymentId) {
                    <div class="row-actions">
                      <button kendoButton size="small" themeColor="primary" type="button" [disabled]="busy()" (click)="confirmPayment(row)">Xác nhận</button>
                      <a class="cf-link-button small" [routerLink]="['/funds', fundId(), 'payments']">Xem</a>
                    </div>
                  }
                </ng-template>
              </kendo-grid-column>
              <ng-template kendoGridNoRecordsTemplate>
                <p class="no-records">Không có học sinh phù hợp.</p>
              </ng-template>
            </kendo-grid>
          }
        </section>
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
    .stats,
    .table {
      margin-bottom: 16px;
    }
    .figures {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
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
    .await {
      color: var(--cf-warning-fg);
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
    .pending {
      background: #f59e0b;
    }
    .legend {
      margin: 8px 0 0;
      font-size: 13px;
    }
    .toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      margin-bottom: 12px;
    }
    .tabs {
      display: inline-flex;
      gap: 4px;
      padding: 4px;
      border-radius: var(--cf-radius-lg);
      background: var(--cf-subtle);
    }
    .tabs button {
      min-height: 32px;
      padding: 0 12px;
      border: 0;
      border-radius: var(--cf-radius-md);
      background: transparent;
      color: var(--cf-text-muted);
      font: inherit;
      font-weight: 600;
      cursor: pointer;
    }
    .tabs button.active {
      background: var(--cf-surface);
      color: var(--cf-text);
      box-shadow: 0 1px 2px rgba(15, 23, 42, 0.1);
    }
    .search {
      box-sizing: border-box;
      width: 260px;
      min-height: 40px;
      padding: 0 12px;
      border: 1px solid var(--cf-border-strong);
      border-radius: var(--cf-radius-lg);
      font: inherit;
    }
    .who {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .name {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .role {
      padding: 0 8px;
      border-radius: var(--cf-radius-pill);
      background: var(--cf-role-treasurer-tint);
      color: var(--cf-role-treasurer);
      font-size: 12px;
      font-weight: 600;
      line-height: 20px;
    }
    .self {
      margin-left: 6px;
    }
    .row-actions {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .small {
      min-height: 28px;
      padding: 0 10px;
      font-size: 13px;
    }
    .no-records {
      margin: 24px 0;
      color: var(--cf-text-muted);
      text-align: center;
    }
  `,
})
export class ChargeDetailPage {
  private readonly api = inject(ChargesApi);
  private readonly confirm = inject(ConfirmDialog);
  private readonly notifications = inject(AppNotifications);

  readonly fundId = input.required<string>();
  readonly chargeId = input.required<string>();

  protected readonly pageSize = PAGE_SIZE;
  protected readonly filters = FILTERS;
  protected readonly filter = signal<Filter>('All');
  protected readonly pageNumber = signal(1);
  protected readonly refresh = signal(0);
  protected readonly busy = signal(false);
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  private readonly search = toSignal(
    this.searchControl.valueChanges.pipe(debounceTime(300), map((value) => value.trim()), distinctUntilChanged()),
    { initialValue: '' },
  );

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

  // ---------- Danh sách học sinh ----------

  private readonly rowsState = toSignal(
    toObservable(
      computed(() => ({
        fundId: this.fundId(),
        chargeId: this.chargeId(),
        filter: this.filter(),
        search: this.search(),
        page: this.pageNumber(),
        refresh: this.refresh(),
      })),
    ).pipe(
      switchMap((query) =>
        toLoadState(
          this.api.assignments(query.fundId, query.chargeId, {
            status: query.filter === 'All' ? undefined : query.filter,
            search: query.search,
            page: query.page,
            pageSize: PAGE_SIZE,
          }),
        ),
      ),
    ),
    { initialValue: LOADING as LoadState<PagedResult<AssignmentRow>> },
  );
  private lastRows: PagedResult<AssignmentRow> | null = null;
  protected readonly rows = computed<PagedResult<AssignmentRow> | null>(() => {
    const state = this.rowsState();
    if (state.status === 'ready') {
      this.lastRows = state.data;
    }
    return state.status === 'error' ? null : this.lastRows;
  });
  protected readonly reloading = computed(() => this.rowsState().status === 'loading' && this.lastRows !== null);
  protected readonly rowsKind = computed(() => {
    const state = this.rowsState();
    return state.status === 'error' || (state.status === 'loading' && this.lastRows === null) ? stateKind(state) : null;
  });

  // ---------- Hiển thị ----------

  protected percent(part: number, total: number): number {
    return percentOf(part, total);
  }

  protected chargeStatus(charge: Charge): string {
    return CHARGE_STATUS_LABEL[charge.status];
  }

  protected assignmentLabel(status: AssignmentStatus): string {
    return ASSIGNMENT_STATUS_LABEL[status];
  }

  protected methodLabel(row: AssignmentRow): string {
    return row.method ? PAYMENT_METHOD_LABEL[row.method] : '';
  }

  protected roleLabel(row: AssignmentRow): string {
    return ROLE_DISPLAY[row.role].badge;
  }

  protected missing(charge: Charge): number {
    const p = charge.progress;
    return p ? Math.max(0, (p.assignedCount - p.paidCount - p.awaitingCount) * charge.amountPerPerson) : 0;
  }

  /** Cột "Ghi nhận": ngày nộp / xác nhận kèm ghi chú, hoặc lý do bị từ chối. */
  protected recorded(row: AssignmentRow): string {
    if (row.status === 'Unpaid' && row.rejectionReason) {
      return `Bị từ chối: ${row.rejectionReason}`;
    }
    if (!row.paymentAt) {
      return '–';
    }
    const date = new VnDatePipe().transform(row.paymentAt).slice(0, 5);
    if (row.isSelfDeclared) {
      return `${date} · tự ghi nhận`;
    }
    return row.hasProof ? `${date} · có ảnh minh chứng` : date;
  }

  protected setFilter(value: Filter): void {
    this.filter.set(value);
    this.pageNumber.set(1);
  }

  protected onPageChange(event: PageChangeEvent): void {
    this.pageNumber.set(Math.floor(event.skip / PAGE_SIZE) + 1);
  }

  // ---------- Hành động ----------

  protected close(charge: Charge): void {
    this.confirm
      .confirm({
        title: 'Đóng khoản thu',
        message: `Đóng khoản “${charge.title}”? Học sinh chưa đóng sẽ không nộp thêm được; các khoản đang chờ xác nhận vẫn xử lý bình thường.`,
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

  protected recordCash(row: AssignmentRow): void {
    this.confirm
      .confirm({
        title: 'Ghi nhận tiền mặt',
        message: `Ghi nhận ${row.fullName} đã đưa ${new VndMoneyPipe().transform(row.amountDue)} tiền mặt? Khoản này được tính là đã đóng và ghi vào sổ thu ngay.`,
        confirmLabel: 'Ghi nhận',
      })
      .subscribe((confirmed) => {
        if (confirmed) {
          void this.run(() => this.api.recordCash(this.fundId(), row.assignmentId), `Đã ghi nhận tiền mặt của ${row.fullName}.`);
        }
      });
  }

  protected confirmPayment(row: AssignmentRow): void {
    if (row.paymentId) {
      const paymentId = row.paymentId;
      void this.run(() => this.api.confirm(this.fundId(), paymentId), `Đã xác nhận khoản đóng của ${row.fullName}.`);
    }
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
