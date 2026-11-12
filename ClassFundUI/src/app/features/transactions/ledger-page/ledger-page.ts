import { Location } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DropDownListComponent } from '@progress/kendo-angular-dropdowns';
import { GridModule, RowClassArgs } from '@progress/kendo-angular-grid';
import { TextBoxComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { Subscription, filter, map } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { Permissions } from '../../../core/auth/permissions';
import { AppNotifications } from '../../../core/services/notification.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { CategoriesApi } from '../../categories/categories.api';
import { Category } from '../../categories/categories.models';
import { FundsApi } from '../../funds/funds.api';
import { CancelTxDialog } from '../cancel-tx-dialog/cancel-tx-dialog';
import { dmy, monthOptions, monthRange, vietnamToday } from '../ledger-dates';
import { TransactionDetail } from '../transaction-detail/transaction-detail';
import { TransactionsApi } from '../transactions.api';
import {
  PAGE_SIZE,
  Transaction,
  TransactionHistoryItem,
  TransactionPage,
  TransactionStatus,
  TransactionType,
} from '../transactions.models';
import { TxDialog } from '../tx-dialog/tx-dialog';

type TypeFilter = 'all' | TransactionType;

interface Dialog {
  kind: 'add' | 'edit' | 'cancel';
  type: TransactionType;
  transaction: Transaction | null;
}

const STATUS_OPTIONS: { value: TransactionStatus; label: string }[] = [
  { value: 'Active', label: 'Đang hiệu lực' },
  { value: 'Cancelled', label: 'Đã hủy' },
  { value: 'All', label: 'Tất cả' },
];

const TYPE_OPTIONS: { value: TypeFilter; label: string }[] = [
  { value: 'all', label: 'Tất cả' },
  { value: 'Income', label: 'Thu' },
  { value: 'Expense', label: 'Chi' },
];

/**
 * S-02 "Thu chi" – Canvas board `Ledger` / `LedgerTeacher`; FR-02 → FR-07, BR-14. Bộ lọc (tháng, loại, trạng thái, danh mục,
 * tìm theo nội dung) → `GET /transactions` phân trang server 20 / trang; panel chi tiết 340 px theo `/transactions/:txId`.
 * Thủ quỹ thêm / sửa / hủy qua T-01. Học sinh, phụ huynh: bản công khai (backend đã lọc).
 */
@Component({
  selector: 'app-ledger-page',
  imports: [
    FormsModule,
    ButtonComponent,
    DropDownListComponent,
    GridModule,
    LabelComponent,
    TextBoxComponent,
    AppState,
    PageHeader,
    VndMoneyPipe,
    CancelTxDialog,
    TransactionDetail,
    TxDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <app-page-header [title]="full() ? 'Thu chi' : 'Thu chi công khai'" [subtitle]="subtitle()">
        @if (canManage()) {
          <div class="head-actions">
            <button kendoButton type="button" (click)="openAdd('Income')">+ Khoản thu</button>
            <button kendoButton themeColor="primary" type="button" (click)="openAdd('Expense')">+ Khoản chi</button>
          </div>
        }
      </app-page-header>

      <section class="cf-card filters" aria-label="Bộ lọc">
        <div class="field month">
          <kendo-label [for]="monthPicker" text="Tháng" />
          <kendo-dropdownlist
            #monthPicker
            [data]="months()"
            textField="label"
            valueField="value"
            [valuePrimitive]="true"
            [value]="month()"
            (valueChange)="setFilter(month, $event)"
          />
        </div>
        <div class="field">
          <span class="lbl" id="lf-type">Loại</span>
          <div class="segmented" role="group" aria-labelledby="lf-type">
            @for (option of typeOptions; track option.value) {
              <button
                kendoButton
                type="button"
                [class.on]="type() === option.value"
                [attr.aria-pressed]="type() === option.value"
                (click)="setFilter(type, option.value)"
              >
                {{ option.label }}
              </button>
            }
          </div>
        </div>
        <div class="field status">
          <kendo-label [for]="statusPicker" text="Trạng thái" />
          <kendo-dropdownlist
            #statusPicker
            [data]="statusOptions"
            textField="label"
            valueField="value"
            [valuePrimitive]="true"
            [value]="status()"
            (valueChange)="setFilter(status, $event)"
          />
        </div>
        <div class="field category">
          <kendo-label [for]="categoryPicker" text="Danh mục" />
          <kendo-dropdownlist
            #categoryPicker
            [data]="categoryOptions()"
            textField="name"
            valueField="id"
            [valuePrimitive]="true"
            [value]="categoryId()"
            (valueChange)="setFilter(categoryId, $event)"
          />
          @if (!full()) {
            <span class="cf-muted hint">Theo toàn bộ lịch sử quỹ</span>
          }
        </div>
        <div class="field search">
          <kendo-label [for]="searchBox" text="Tìm theo nội dung" />
          <kendo-textbox
            #searchBox
            placeholder="Ví dụ: liên hoan"
            [clearButton]="true"
            [ngModel]="searchText()"
            (ngModelChange)="onSearch($event)"
          />
        </div>
        @if (hasFilter()) {
          <button kendoButton type="button" class="clear" (click)="clearFilters()">Xóa bộ lọc</button>
        }
      </section>

      <div class="layout">
        <section class="cf-card list" aria-label="Danh sách giao dịch">
          @switch (listKind()) {
            @case ('loading') {
              <app-state kind="loading" />
            }
            @case ('error') {
              <app-state kind="error" actionLabel="Thử lại" (action)="reload()" />
            }
            @case (null) {
              <kendo-grid
                [data]="items()"
                scrollable="none"
                [navigable]="true"
                [rowClass]="rowClass"
                aria-label="Giao dịch"
              >
                <kendo-grid-column title="Ngày" [width]="100">
                  <ng-template kendoGridCellTemplate let-tx>
                    <span class="cf-muted nowrap">{{ date(tx.occurredOn) }}</span>
                  </ng-template>
                </kendo-grid-column>
                <kendo-grid-column title="Nội dung">
                  <ng-template kendoGridCellTemplate let-tx>
                    <div class="desc">
                      <button type="button" class="rowbtn" [class.strike]="tx.isCancelled" (click)="select(tx.id)">
                        {{ tx.description }}
                      </button>
                      @if (full() && tx.isPublic === false) {
                        <span class="cf-tag cf-tag--warning">Không công khai</span>
                      }
                    </div>
                  </ng-template>
                </kendo-grid-column>
                <kendo-grid-column title="Danh mục" [width]="170">
                  <ng-template kendoGridCellTemplate let-tx>
                    <span class="cf-muted">{{ tx.categoryName }}</span>
                  </ng-template>
                </kendo-grid-column>
                <kendo-grid-column title="Trạng thái" [width]="104">
                  <ng-template kendoGridCellTemplate let-tx>
                    @if (tx.isCancelled) {
                      <span class="cf-tag cf-tag--danger">Đã hủy</span>
                    } @else {
                      <span class="cf-tag cf-tag--success">Hiệu lực</span>
                    }
                  </ng-template>
                </kendo-grid-column>
                <kendo-grid-column title="Số tiền" [width]="130" [headerClass]="'num-head'">
                  <ng-template kendoGridCellTemplate let-tx>
                    <span
                      class="cf-money amount"
                      [class.strike]="tx.isCancelled"
                      [class.cf-muted]="tx.isCancelled"
                      [class.cf-income]="!tx.isCancelled && tx.type === 'Income'"
                      [class.cf-expense]="!tx.isCancelled && tx.type === 'Expense'"
                      >{{ signed(tx) | vndMoney: true }}</span
                    >
                  </ng-template>
                </kendo-grid-column>
                <ng-template kendoGridNoRecordsTemplate>
                  <div class="empty" role="status">
                    <b>Không có giao dịch phù hợp</b>
                    <span class="cf-muted">Thử bỏ bớt điều kiện lọc hoặc đổi tháng.</span>
                    @if (hasFilter()) {
                      <button kendoButton type="button" (click)="clearFilters()">Xóa bộ lọc</button>
                    }
                  </div>
                </ng-template>
              </kendo-grid>
              <nav class="pager" aria-label="Phân trang">
                <span class="cf-muted">{{ total() }} {{ full() ? 'giao dịch' : 'giao dịch công khai' }} · {{ pageSize }} / trang</span>
                <span class="pages">
                  <button kendoButton type="button" aria-label="Trang trước" [disabled]="page() <= 1" (click)="goTo(page() - 1)">‹</button>
                  <span>Trang {{ page() }} / {{ pages() }}</span>
                  <button kendoButton type="button" aria-label="Trang sau" [disabled]="page() >= pages()" (click)="goTo(page() + 1)">›</button>
                </span>
              </nav>
            }
            @default {
              <app-state [kind]="listKind()!" />
            }
          }
        </section>

        <aside class="cf-card detail" aria-label="Chi tiết giao dịch">
          @if (selectedId()) {
            <app-transaction-detail
              [state]="detail()"
              [history]="history()"
              [categories]="categories()"
              [full]="full()"
              [canManage]="canManage()"
              (edit)="openEdit($event)"
              (cancel)="openCancel($event)"
              (closed)="select(null)"
            />
          } @else {
            <div class="cf-muted none">Chọn một giao dịch để xem chi tiết.</div>
          }
        </aside>
      </div>
    </section>

    @if (dialog(); as dialog) {
      @if (dialog.kind === 'cancel') {
        <app-cancel-tx-dialog
          [fundId]="fundId()"
          [transaction]="dialog.transaction!"
          (cancelled)="onCancelled($event)"
          (closed)="dialogState.set(null)"
        />
      } @else {
        <app-tx-dialog
          [fundId]="fundId()"
          [addType]="dialog.type"
          [transaction]="dialog.transaction"
          [categories]="categories()"
          [balance]="balance()"
          (saved)="onSaved($event, dialog.kind)"
          (closed)="dialogState.set(null)"
        />
      }
    }
  `,
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    app-page-header {
      margin-bottom: 0;
    }
    .head-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .filters {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: 12px;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 4px;
      min-width: 0;
    }
    .month {
      flex: 0 1 180px;
      min-width: 150px;
    }
    .status {
      flex: 0 1 170px;
      min-width: 150px;
    }
    .category {
      flex: 0 1 210px;
      min-width: 170px;
    }
    .search {
      flex: 1 1 220px;
    }
    .lbl {
      color: var(--cf-text-muted);
      font-size: 12px;
      font-weight: 600;
    }
    .hint {
      font-size: 12px;
    }
    .segmented {
      display: flex;
    }
    .segmented button {
      margin-left: -1px;
      border-radius: 0;
    }
    .segmented button:first-child {
      margin-left: 0;
      border-radius: var(--cf-radius-md) 0 0 var(--cf-radius-md);
    }
    .segmented button:last-child {
      border-radius: 0 var(--cf-radius-md) var(--cf-radius-md) 0;
    }
    .segmented button.on {
      background: var(--cf-primary-tint);
      color: var(--cf-primary-dark);
      font-weight: 600;
    }
    .clear {
      align-self: flex-end;
    }
    .layout {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: 16px;
    }
    .list {
      flex: 999 1 560px;
      min-width: 0;
      padding: 0;
      overflow: hidden;
    }
    .detail {
      flex: 1 1 340px;
      min-width: 0;
    }
    .none {
      padding: 24px 8px;
      text-align: center;
    }
    .nowrap {
      white-space: nowrap;
    }
    .desc {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 4px;
      min-width: 0;
    }
    .rowbtn {
      all: unset;
      cursor: pointer;
      color: var(--cf-text);
      font-weight: 600;
      overflow-wrap: anywhere;
    }
    .rowbtn:hover {
      color: var(--cf-primary);
      text-decoration: underline;
    }
    .rowbtn:focus-visible {
      outline: 2px solid var(--cf-primary);
      outline-offset: 2px;
    }
    .strike {
      text-decoration: line-through;
    }
    .amount {
      display: block;
      font-weight: 600;
      text-align: right;
    }
    /* Bảng theo Canvas Ledger: header nền xám 12 px đậm, không kẻ dọc, không tô xen kẽ, dòng chọn tô xanh nhạt. */
    :host ::ng-deep .list .k-grid {
      border: 0;
    }
    :host ::ng-deep .list .k-grid th,
    :host ::ng-deep .list .k-grid td {
      border-inline-start-width: 0;
    }
    :host ::ng-deep .list .k-grid .k-grid-header th {
      padding: 6px 16px;
      background: var(--cf-background);
      color: var(--cf-text-muted);
      font-size: 12px;
      font-weight: 600;
    }
    :host ::ng-deep .list .k-grid td {
      padding: 10px 16px;
      border-bottom: 1px solid var(--cf-subtle);
      vertical-align: top;
    }
    :host ::ng-deep .list .k-grid tr.k-alt,
    :host ::ng-deep .list .k-grid tr.k-table-alt-row {
      background: var(--cf-surface);
    }
    :host ::ng-deep .list .k-grid tbody tr.selected {
      background: var(--cf-primary-tint);
    }
    :host ::ng-deep .list .k-grid .num-head .k-column-title,
    :host ::ng-deep .list .k-grid .num-head {
      justify-content: flex-end;
      text-align: right;
    }
    /* Nhãn bộ lọc theo Canvas: 12 px đậm, màu phụ. */
    :host ::ng-deep .filters .k-label {
      color: var(--cf-text-muted);
      font-size: 12px;
      font-weight: 600;
    }
    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 36px 16px;
      text-align: center;
    }
    .pager {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      padding: 10px 16px;
      font-size: 13px;
    }
    .pages {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .pages button {
      min-height: 36px;
    }
  `,
})
export class LedgerPage {
  private readonly api = inject(TransactionsApi);
  private readonly funds = inject(FundsApi);
  private readonly categoriesApi = inject(CategoriesApi);
  private readonly auth = inject(AuthService);
  private readonly notifications = inject(AppNotifications);
  private readonly location = inject(Location);
  private readonly destroyRef = inject(DestroyRef);

  /** `:fundId` (route cha) và `:txId` (khi mở thẳng `/transactions/:txId`). */
  readonly fundId = input.required<string>();
  readonly txId = input<string>();

  protected readonly pageSize = PAGE_SIZE;
  protected readonly statusOptions = STATUS_OPTIONS;
  protected readonly typeOptions = TYPE_OPTIONS;
  private readonly today = vietnamToday();

  // Bộ lọc – mặc định tháng hiện tại, khoản đang hiệu lực (screen-specifications.md S-02).
  protected readonly month = signal(this.today.slice(0, 7));
  protected readonly type = signal<TypeFilter>('all');
  protected readonly status = signal<TransactionStatus>('Active');
  protected readonly categoryId = signal<string>('');
  protected readonly searchText = signal('');
  private readonly search = signal('');
  protected readonly page = signal(1);

  protected readonly list = signal<LoadState<TransactionPage>>(LOADING);
  protected readonly categories = signal<Category[]>([]);
  protected readonly selectedId = signal<string | null>(null);
  protected readonly detail = signal<LoadState<Transaction>>(LOADING);
  protected readonly history = signal<LoadState<TransactionHistoryItem[]>>(LOADING);
  protected readonly dialogState = signal<Dialog | null>(null);
  protected readonly dialog = this.dialogState.asReadonly();
  private readonly fundCreatedAt = signal<string | null>(null);
  protected readonly balance = signal<number | null>(null);

  private listRequest?: Subscription;
  private detailRequest?: Subscription;
  private searchTimer?: ReturnType<typeof setTimeout>;

  private readonly permissions = computed(() => this.auth.membership(this.fundId())?.permissions ?? []);
  protected readonly full = computed(() => this.permissions().includes(Permissions.LedgerViewFull));
  protected readonly canManage = computed(() => this.permissions().includes(Permissions.TransactionManage));
  protected readonly subtitle = computed(() => {
    if (this.canManage()) {
      return 'Bấm vào nội dung để xem chi tiết, sửa hoặc hủy';
    }
    return this.full()
      ? 'GVCN chỉ xem (không ghi thu chi) · lọc “Đã hủy” để xem khoản đã hủy và lý do'
      : 'Các khoản thủ quỹ công khai · lọc “Đã hủy” để xem khoản công khai đã hủy';
  });
  protected readonly months = computed(() => monthOptions(this.fundCreatedAt(), this.today));
  protected readonly categoryOptions = computed(() => [
    { id: '', name: 'Tất cả danh mục' },
    ...this.categories().map((category) => ({ id: category.id, name: category.name })),
  ]);
  protected readonly hasFilter = computed(
    () =>
      this.type() !== 'all' ||
      this.status() !== 'Active' ||
      !!this.categoryId() ||
      !!this.search() ||
      this.month() !== this.today.slice(0, 7),
  );
  protected readonly listKind = computed(() => stateKind(this.list(), () => false));
  protected readonly items = computed(() => {
    const state = this.list();
    return state.status === 'ready' ? state.data.items : [];
  });
  protected readonly total = computed(() => {
    const state = this.list();
    return state.status === 'ready' ? state.data.total : 0;
  });
  protected readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));
  private readonly query = computed(() => {
    const range = this.month() ? monthRange(this.month()) : { from: undefined, to: undefined };
    const type = this.type();
    return {
      ...range,
      type: type === 'all' ? undefined : type,
      categoryId: this.categoryId() || undefined,
      search: this.search() || undefined,
      status: this.status(),
      page: this.page(),
      pageSize: PAGE_SIZE,
    };
  });

  protected readonly rowClass = (context: RowClassArgs) => ({
    selected: (context.dataItem as Transaction).id === this.selectedId(),
  });

  constructor() {
    effect(() => {
      const fundId = this.fundId();
      untracked(() => {
        this.loadFund(fundId);
        this.loadCategories(fundId);
      });
    });
    effect(() => {
      const query = this.query();
      untracked(() => this.loadList(query));
    });
    effect(() => {
      const txId = this.txId();
      untracked(() => this.select(txId ?? null, false));
    });
    this.destroyRef.onDestroy(() => clearTimeout(this.searchTimer));
  }

  protected setFilter<T>(target: { set(value: T): void }, value: T): void {
    target.set(value);
    this.page.set(1);
  }

  /** Tìm sau khi ngừng gõ 300 ms để không gọi API mỗi phím. */
  protected onSearch(value: string | null): void {
    this.searchText.set(value ?? '');
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.setFilter(this.search, (value ?? '').trim()), 300);
  }

  protected clearFilters(): void {
    this.month.set(this.today.slice(0, 7));
    this.type.set('all');
    this.status.set('Active');
    this.categoryId.set('');
    this.searchText.set('');
    this.search.set('');
    this.page.set(1);
  }

  protected goTo(page: number): void {
    this.page.set(Math.min(Math.max(page, 1), this.pages()));
  }

  protected reload(): void {
    this.loadList(this.query());
  }

  /** Chọn khoản: cập nhật URL `/transactions/:txId` (không tạo lại trang, giữ bộ lọc) và tải chi tiết + lịch sử. */
  protected select(id: string | null, updateUrl = true): void {
    this.detailRequest?.unsubscribe();
    this.selectedId.set(id);
    if (updateUrl) {
      this.location.replaceState(`/funds/${this.fundId()}/transactions${id ? '/' + id : ''}`);
    }
    if (!id) {
      return;
    }
    this.detail.set(LOADING);
    this.history.set(this.full() ? LOADING : { status: 'ready', data: [] });
    const subscription = new Subscription();
    subscription.add(toLoadState(this.api.get(this.fundId(), id)).subscribe((state) => this.detail.set(state)));
    if (this.full()) {
      subscription.add(toLoadState(this.api.history(this.fundId(), id)).subscribe((state) => this.history.set(state)));
    }
    this.detailRequest = subscription;
  }

  protected openAdd(type: TransactionType): void {
    this.dialogState.set({ kind: 'add', type, transaction: null });
  }

  protected openEdit(transaction: Transaction): void {
    this.dialogState.set({ kind: 'edit', type: transaction.type, transaction });
  }

  protected openCancel(transaction: Transaction): void {
    this.dialogState.set({ kind: 'cancel', type: transaction.type, transaction });
  }

  protected onSaved(saved: Transaction, kind: Dialog['kind']): void {
    this.dialogState.set(null);
    if (kind === 'add') {
      // Khoản mới hiện ở tháng của nó, trạng thái đang hiệu lực.
      this.month.set(saved.occurredOn.slice(0, 7));
      this.status.set('Active');
      this.page.set(1);
    }
    this.afterWrite(saved, (balance) =>
      kind === 'add'
        ? `Đã lưu ${saved.type === 'Income' ? 'khoản thu' : 'khoản chi'} ${new VndMoneyPipe().transform(saved.amount)}.` +
          (balance === null ? '' : ` Số dư mới: ${new VndMoneyPipe().transform(balance)}.`)
        : 'Đã lưu thay đổi. Lịch sử thay đổi ghi giá trị trước / sau.',
    );
  }

  protected onCancelled(saved: Transaction): void {
    this.dialogState.set(null);
    this.afterWrite(saved, () => 'Đã hủy giao dịch. Khoản này không còn tính vào số dư.');
  }

  private afterWrite(saved: Transaction, message: (balance: number | null) => string): void {
    this.reload();
    this.select(saved.id);
    this.funds
      .list()
      .pipe(
        map((funds) => funds.find((fund) => fund.id === this.fundId())?.currentBalance ?? null),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (balance) => {
          this.balance.set(balance);
          this.notifications.success(message(balance));
          if (saved.isNegativeBalance) {
            this.notifications.warning('Số dư quỹ đang âm. Khoản vẫn được lưu (BR-04).');
          }
        },
        error: () => this.notifications.success(message(null)),
      });
  }

  private loadList(query: ReturnType<LedgerPage['query']>): void {
    this.listRequest?.unsubscribe();
    const silent = this.list().status === 'ready';
    this.listRequest = toLoadState(this.api.list(this.fundId(), query))
      .pipe(
        // Đổi trang / bộ lọc: giữ bảng cũ trong lúc tải để không nhấp nháy.
        filter((state) => !silent || state.status !== 'loading'),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((state) => this.list.set(state));
  }

  private loadCategories(fundId: string): void {
    // GVCN / thủ quỹ: kèm danh mục đã ẩn (lọc khoản cũ, giữ danh mục khi sửa); công khai: chỉ danh mục có khoản công khai (X-01).
    this.categoriesApi
      .list(fundId, { includeHidden: this.full() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (categories) => this.categories.set(categories), error: () => this.categories.set([]) });
  }

  private loadFund(fundId: string): void {
    this.funds
      .list()
      .pipe(
        map((funds) => funds.find((fund) => fund.id === fundId)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (fund) => {
          this.fundCreatedAt.set(fund?.createdAt ?? null);
          this.balance.set(fund?.currentBalance ?? null);
        },
        error: () => this.balance.set(null),
      });
  }

  protected date(value: string): string {
    return dmy(value);
  }

  protected signed(tx: Transaction): number {
    return tx.type === 'Income' ? tx.amount : -tx.amount;
  }
}
