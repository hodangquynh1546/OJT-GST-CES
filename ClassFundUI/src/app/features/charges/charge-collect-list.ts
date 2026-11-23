import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { CellTemplateDirective, ColumnComponent, GridComponent, NoRecordsTemplateDirective, PageChangeEvent } from '@progress/kendo-angular-grid';
import { Observable, debounceTime, distinctUntilChanged, firstValueFrom, map, switchMap } from 'rxjs';

import { ROLE_DISPLAY } from '../../core/navigation/role-display';
import { AppNotifications } from '../../core/services/notification.service';
import { AppState } from '../../shared/components/app-state/app-state';
import { ApiError } from '../../shared/models/api-error';
import { ASSIGNMENT_STATUS_LABEL, AssignmentStatus, PAYMENT_METHOD_LABEL } from '../../shared/models/labels';
import { PagedResult } from '../../shared/models/paged-result';
import { VnDatePipe } from '../../shared/pipes/vn-date.pipe';
import { VndMoneyPipe } from '../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../shared/state/load-state';
import { EventsApi } from '../events/events.api';
import { ChargesApi } from './charges.api';
import './charges.errors';
import { AssignmentRow, CollectMethod } from './charges.models';

const PAGE_SIZE = 20;

type Filter = 'All' | AssignmentStatus;

const FILTERS: readonly { value: Filter; label: string }[] = [
  { value: 'All', label: 'Tất cả' },
  { value: 'Unpaid', label: 'Chưa nộp' },
  { value: 'Paid', label: 'Đã nộp' },
];

/**
 * Danh sách người phải đóng của MỘT khoản thu (khoản chung hoặc khoản của sự kiện) cho thủ quỹ (FR-22, FR-24):
 * tích "đã thu" chọn Tiền mặt / Chuyển khoản (số dư không phải phương thức thanh toán), bỏ tích nhầm kèm lý do, "Nhắc đóng" từng người.
 * Học sinh không có màn hình này – họ chỉ xem Đã nộp / Chưa nộp ở H-03.
 */
@Component({
  selector: 'app-charge-collect-list',
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ButtonComponent,
    DialogActionsComponent,
    DialogComponent,
    GridComponent,
    ColumnComponent,
    CellTemplateDirective,
    NoRecordsTemplateDirective,
    AppState,
    VndMoneyPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
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
          <kendo-grid-column title="Học sinh" [width]="280">
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
          <kendo-grid-column title="Trạng thái" [width]="190">
            <ng-template kendoGridCellTemplate let-row>
              <span class="cf-tag" [class.cf-tag--success]="row.status === 'Paid'">{{ assignmentLabel(row.status) }}</span>
              @if (row.isSelfDeclared) { <span class="cf-tag cf-tag--warning self">Tự ghi nhận</span> }
            </ng-template>
          </kendo-grid-column>
          <kendo-grid-column title="Hình thức" [width]="130">
            <ng-template kendoGridCellTemplate let-row>{{ row.method ? methodLabel(row) : '–' }}</ng-template>
          </kendo-grid-column>
          <kendo-grid-column title="Ghi nhận">
            <ng-template kendoGridCellTemplate let-row><span class="cf-muted">{{ recorded(row) }}</span></ng-template>
          </kendo-grid-column>
          @if (chargeOpen()) {
            <kendo-grid-column title="Thao tác" [width]="250">
              <ng-template kendoGridCellTemplate let-row>
                @if (row.status === 'Unpaid') {
                  <div class="row-actions">
                    <button kendoButton size="small" themeColor="primary" type="button" [disabled]="busy()" (click)="openCollect(row)">Đã thu</button>
                    <button kendoButton size="small" fillMode="outline" type="button" [disabled]="busy()" (click)="remind(row)">Nhắc đóng</button>
                  </div>
                } @else if (row.status === 'Paid') {
                  <button kendoButton size="small" fillMode="outline" type="button" [disabled]="busy()" (click)="openUncollect(row)">Bỏ xác nhận thu</button>
                }
              </ng-template>
            </kendo-grid-column>
          }
          <ng-template kendoGridNoRecordsTemplate>
            <p class="no-records">Không có học sinh phù hợp.</p>
          </ng-template>
        </kendo-grid>
      }
    </section>

    @if (collecting(); as row) {
      <kendo-dialog [title]="'Ghi nhận đã thu · ' + row.fullName" [width]="460" [maxWidth]="'calc(100vw - 32px)'" (close)="collecting.set(null)">
        <div class="stack">
          <p>
            Số tiền <b>{{ row.amountDue | vndMoney }}</b> – chọn hình thức học sinh đã nộp. Khoản này được tính là đã nộp và ghi vào sổ thu ngay.
          </p>
          <fieldset class="methods">
            <legend>Hình thức nộp</legend>
            <label class="method">
              <input type="radio" name="collect-method" value="Cash" [ngModel]="method()" (ngModelChange)="method.set('Cash')" />
              <span><b>Tiền mặt</b></span>
            </label>
            <label class="method">
              <input type="radio" name="collect-method" value="BankTransfer" [ngModel]="method()" (ngModelChange)="method.set('BankTransfer')" />
              <span><b>Chuyển khoản</b></span>
            </label>
          </fieldset>
          @if (method() === 'BankTransfer') {
            <div class="field">
              <label for="transfer-code">Mã giao dịch (nếu có)</label>
              <input id="transfer-code" class="text" maxlength="100" [ngModel]="transferCode()" (ngModelChange)="transferCode.set($event)" />
            </div>
          }
          @if (dialogError(); as message) {
            <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
          }
        </div>
        <kendo-dialog-actions layout="end">
          <button kendoButton type="button" (click)="collecting.set(null)">Hủy</button>
          <button kendoButton themeColor="primary" type="button" [disabled]="busy()" (click)="collect(row)">Xác nhận đã thu</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }

    @if (uncollecting(); as row) {
      <kendo-dialog [title]="'Bỏ xác nhận thu · ' + row.fullName" [width]="460" [maxWidth]="'calc(100vw - 32px)'" (close)="uncollecting.set(null)">
        <div class="stack">
          <p class="cf-muted">Dùng khi tích nhầm. Hệ thống ghi bút toán đảo vào sổ và đưa khoản này về Chưa nộp; lý do được lưu trong nhật ký.</p>
          <div class="field">
            <label for="uncollect-reason">Lý do</label>
            <textarea id="uncollect-reason" class="text" rows="2" maxlength="500" [ngModel]="reason()" (ngModelChange)="reason.set($event)"></textarea>
          </div>
          @if (dialogError(); as message) {
            <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
          }
        </div>
        <kendo-dialog-actions layout="end">
          <button kendoButton type="button" (click)="uncollecting.set(null)">Hủy</button>
          <button kendoButton themeColor="error" type="button" [disabled]="busy() || !reason().trim()" (click)="uncollect(row)">Bỏ xác nhận thu</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }
  `,
  styles: `
    .table {
      margin-bottom: 16px;
    }
    .toolbar {
      display: flex;
      flex-wrap: wrap;
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
      max-width: 100%;
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
    .no-records {
      margin: 24px 0;
      color: var(--cf-text-muted);
      text-align: center;
    }
    .stack {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .stack p {
      margin: 0;
    }
    .methods {
      display: grid;
      gap: 8px;
      margin: 0;
      padding: 0;
      border: 0;
    }
    legend {
      margin-bottom: 6px;
      font-weight: 500;
    }
    .method {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 10px 12px;
      border: 1px solid var(--cf-border);
      border-radius: var(--cf-radius-lg);
      cursor: pointer;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .text {
      box-sizing: border-box;
      min-height: 40px;
      padding: 8px 12px;
      border: 1px solid var(--cf-border-strong);
      border-radius: var(--cf-radius-lg);
      font: inherit;
    }
  `,
})
export class ChargeCollectList {
  private readonly charges = inject(ChargesApi);
  private readonly events = inject(EventsApi);
  private readonly notifications = inject(AppNotifications);

  readonly fundId = input.required<string>();
  readonly chargeId = input.required<string>();
  /** Khoản thu của sự kiện nào; null = khoản thu chung. */
  readonly eventId = input<string | null>(null);
  /** Khoản thu còn mở mới được tích / bỏ tích / nhắc. */
  readonly chargeOpen = input(true);
  /** Báo cho trang cha tải lại tiến độ sau mỗi thay đổi. */
  readonly changed = output<void>();

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

  protected readonly collecting = signal<AssignmentRow | null>(null);
  protected readonly uncollecting = signal<AssignmentRow | null>(null);
  protected readonly method = signal<CollectMethod>('Cash');
  protected readonly transferCode = signal('');
  protected readonly reason = signal('');
  protected readonly dialogError = signal<string | null>(null);

  private readonly rowsState = toSignal(
    toObservable(
      computed(() => ({
        fundId: this.fundId(),
        chargeId: this.chargeId(),
        eventId: this.eventId(),
        filter: this.filter(),
        search: this.search(),
        page: this.pageNumber(),
        refresh: this.refresh(),
      })),
    ).pipe(
      switchMap((query) => {
        const params = { status: query.filter === 'All' ? undefined : query.filter, search: query.search, page: query.page, pageSize: PAGE_SIZE };
        return toLoadState(
          query.eventId
            ? this.events.chargeAssignments(query.fundId, query.eventId, query.chargeId, params)
            : this.charges.assignments(query.fundId, query.chargeId, params),
        );
      }),
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

  protected assignmentLabel(status: AssignmentStatus): string {
    return ASSIGNMENT_STATUS_LABEL[status];
  }

  protected methodLabel(row: AssignmentRow): string {
    return row.method ? PAYMENT_METHOD_LABEL[row.method] : '';
  }

  protected roleLabel(row: AssignmentRow): string {
    return ROLE_DISPLAY[row.role].badge;
  }

  /** Cột "Ghi nhận": ngày thu kèm ghi chú tự ghi nhận. */
  protected recorded(row: AssignmentRow): string {
    if (!row.paymentAt) {
      return '–';
    }
    const date = new VnDatePipe().transform(row.paymentAt).slice(0, 5);
    return row.isSelfDeclared ? `${date} · tự ghi nhận` : date;
  }

  protected setFilter(value: Filter): void {
    this.filter.set(value);
    this.pageNumber.set(1);
  }

  protected onPageChange(event: PageChangeEvent): void {
    this.pageNumber.set(Math.floor(event.skip / PAGE_SIZE) + 1);
  }

  // ---------- Hành động ----------

  protected openCollect(row: AssignmentRow): void {
    this.method.set('Cash');
    this.transferCode.set('');
    this.dialogError.set(null);
    this.collecting.set(row);
  }

  protected openUncollect(row: AssignmentRow): void {
    this.reason.set('');
    this.dialogError.set(null);
    this.uncollecting.set(row);
  }

  protected async collect(row: AssignmentRow): Promise<void> {
    const bank = this.method() === 'BankTransfer';
    await this.run(
      () => this.charges.collect(this.fundId(), row.assignmentId, { method: this.method(), transferCode: bank ? this.transferCode().trim() || null : null }),
      `Đã ghi nhận ${row.fullName} đã nộp.`,
      this.collecting,
    );
  }

  protected async uncollect(row: AssignmentRow): Promise<void> {
    await this.run(
      () => this.charges.uncollect(this.fundId(), row.assignmentId, this.reason().trim()),
      `Đã bỏ xác nhận thu của ${row.fullName}.`,
      this.uncollecting,
    );
  }

  protected remind(row: AssignmentRow): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    firstValueFrom(this.charges.remindOne(this.fundId(), row.assignmentId))
      .then(() => this.notifications.success(`Đã nhắc ${row.fullName} đóng khoản này.`))
      .catch((error: unknown) => this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.'))
      .finally(() => this.busy.set(false));
  }

  private async run(action: () => Observable<unknown>, success: string, dialog: { set(value: null): void }): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.dialogError.set(null);
    try {
      await firstValueFrom(action());
      dialog.set(null);
      this.notifications.success(success);
      this.refresh.update((value) => value + 1);
      this.changed.emit();
    } catch (error) {
      this.dialogError.set(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
      this.refresh.update((value) => value + 1);
    } finally {
      this.busy.set(false);
    }
  }
}
