import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DatePickerComponent } from '@progress/kendo-angular-dateinputs';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { DropDownListComponent } from '@progress/kendo-angular-dropdowns';
import { CellTemplateDirective, ColumnComponent, GridComponent, NoRecordsTemplateDirective } from '@progress/kendo-angular-grid';
import { SVGIconComponent } from '@progress/kendo-angular-icons';
import { plusIcon } from '@progress/kendo-svg-icons';
import { BehaviorSubject, catchError, combineLatest, map, of, switchMap } from 'rxjs';

import { Permissions } from '../../../core/auth/permissions';
import { CurrentFund } from '../../../core/services/current-fund.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { ScopeBadge } from '../../../shared/components/scope-badge/scope-badge';
import { VnDatePipe } from '../../../shared/pipes/vn-date.pipe';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { defaultRange, toApiDate } from '../../audit/audit-format';
import { CategoriesApi } from '../../categories/categories.api';
import { Category } from '../../categories/categories.models';
import { AttachmentGallery } from '../attachment-gallery/attachment-gallery';
import { ExpenseDialog } from '../expense-dialog/expense-dialog';
import { LedgerApi } from '../ledger.api';
import '../ledger.errors';
import { LEDGER_PAGE_SIZE, LedgerEntry, LedgerEntryDetail, LedgerList, LedgerTypeFilter, entryTags, signedAmount } from '../ledger.models';

const ALL_CATEGORIES: Partial<Category> = { id: undefined, name: 'Mọi danh mục' };

/**
 * L-01 · Thu chi chung (FR-26 → FR-29) theo canvas LedgerBody: KPI, lọc loại / danh mục / ngày, bảng giao dịch.
 * Thủ quỹ: nút "Ghi giao dịch" (L-02, cũng mở thẳng bằng `…/ledger/new`), "Xem" sang L-03.
 * Học sinh: chỉ đọc, "Xem" mở hộp thoại chứng từ chi công khai (OQ-14).
 */
@Component({
  selector: 'app-ledger-page',
  imports: [
    AppState,
    AttachmentGallery,
    ButtonComponent,
    CellTemplateDirective,
    ColumnComponent,
    DatePickerComponent,
    DialogActionsComponent,
    DialogComponent,
    DropDownListComponent,
    ExpenseDialog,
    FormsModule,
    GridComponent,
    NoRecordsTemplateDirective,
    RouterLink,
    SVGIconComponent,
    ScopeBadge,
    VnDatePipe,
    VndMoneyPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="head">
        <div class="head-t">
          <div class="title-row"><h1>{{ canManage() ? 'Thu chi chung' : 'Chi tiêu' }}</h1><app-scope-badge scope="General" /></div>
          <p class="cf-muted">Sổ chỉ ghi thêm; ghi sai thì tạo bút toán đảo. Thu chi của sự kiện xem trong từng sự kiện.</p>
        </div>
        @if (canManage()) {
          <div class="actions">
            <button kendoButton themeColor="primary" type="button" (click)="openNew()">
              <kendo-svgicon [icon]="plus" />Ghi giao dịch
            </button>
          </div>
        }
      </div>

      @if (data(); as result) {
        <div class="kpis">
          <div class="cf-card kpi"><span class="kpi-l">Số dư quỹ chung</span><span class="kpi-v">{{ result.summary.balance | vndMoney }}</span></div>
          <div class="cf-card kpi">
            <span class="kpi-l">Thu {{ periodLabel() }}</span><span class="kpi-v cf-income">{{ result.summary.totalIncome | vndMoney: true }}</span>
          </div>
          <div class="cf-card kpi">
            <span class="kpi-l">Chi {{ periodLabel() }}</span><span class="kpi-v cf-expense">{{ -result.summary.totalExpense | vndMoney: true }}</span>
          </div>
        </div>
      }

      <section class="cf-card list" aria-label="Lịch sử giao dịch quỹ chung">
        <div class="card-h">
          <div class="tabs" role="tablist" aria-label="Loại giao dịch">
            @for (option of typeOptions; track option.value) {
              <button
                type="button"
                role="tab"
                class="tab"
                [class.on]="type() === option.value"
                [attr.aria-selected]="type() === option.value"
                (click)="setType(option.value)"
              >
                {{ option.label }}
              </button>
            }
          </div>
          <div class="actions">
            <kendo-dropdownlist
              class="category"
              [data]="categories()"
              textField="name"
              valueField="id"
              [valuePrimitive]="true"
              [defaultItem]="allCategories"
              [ngModel]="categoryId()"
              (ngModelChange)="setCategory($event)"
              [attr.aria-label]="'Danh mục'"
            />
            <kendo-datepicker
              class="date"
              format="dd/MM/yyyy"
              [max]="maxFrom()"
              [ngModel]="from()"
              (ngModelChange)="setFrom($event)"
              [attr.aria-label]="'Từ ngày'"
            />
            <kendo-datepicker
              class="date"
              format="dd/MM/yyyy"
              [min]="minTo()"
              [ngModel]="to()"
              (ngModelChange)="setTo($event)"
              [attr.aria-label]="'Đến ngày'"
            />
          </div>
        </div>

        @if (errorKind(); as kind) {
          <app-state [kind]="kind" />
        } @else if (data(); as result) {
          <kendo-grid [data]="result.items" [loading]="loading()" class="ledger-grid">
            <kendo-grid-column title="Ngày" [width]="80">
              <ng-template kendoGridCellTemplate let-row>
                <span class="cf-muted">{{ shortDate(row.transactionDate) }}</span>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Nội dung">
              <ng-template kendoGridCellTemplate let-row>
                <div class="entry-title" [class.strike]="row.reversedByEntryId">{{ row.title }}</div>
                @if (tags(row).length) {
                  <div class="title-row tags">
                    @for (tag of tags(row); track tag.text) {
                      <span class="cf-tag" [class.cf-tag--warning]="tag.tone === 'warning'" [class.cf-tag--danger]="tag.tone === 'danger'">{{ tag.text }}</span>
                    }
                  </div>
                }
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Danh mục" [width]="160">
              <ng-template kendoGridCellTemplate let-row>{{ row.categoryName ?? '–' }}</ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Người ghi" field="performedByName" [width]="180" />
            <kendo-grid-column title="Số tiền" [width]="150" headerClass="num-head">
              <ng-template kendoGridCellTemplate let-row>
                <span class="cf-money amount" [class.cf-income]="row.type === 'Income'" [class.cf-expense]="row.type === 'Expense'">
                  {{ signed(row) | vndMoney: true }}
                </span>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Chi tiết" [width]="100" headerClass="right-head">
              <ng-template kendoGridCellTemplate let-row>
                <div class="right">
                  @if (canManage()) {
                    <a class="cf-link-button small-btn" [routerLink]="[row.id]">Xem</a>
                  } @else {
                    <button kendoButton fillMode="outline" size="small" type="button" (click)="view(row)">Xem</button>
                  }
                </div>
              </ng-template>
            </kendo-grid-column>
            <ng-template kendoGridNoRecordsTemplate>
              <app-state kind="empty-filter" title="Không có giao dịch phù hợp" message="Thử đổi loại, danh mục hoặc khoảng ngày." />
            </ng-template>
          </kendo-grid>
          <div class="card-f">
            <span class="cf-muted small">Hiển thị {{ result.items.length }} / {{ result.total }} giao dịch · {{ rangeLabel() }}</span>
            <div class="actions">
              <button kendoButton fillMode="outline" size="small" type="button" [disabled]="page() <= 1 || loading()" (click)="go(-1)">Trước</button>
              <button kendoButton fillMode="outline" size="small" type="button" [disabled]="page() >= pageCount() || loading()" (click)="go(1)">Sau</button>
            </div>
          </div>
        } @else {
          <app-state kind="loading" />
        }
      </section>
    </div>

    @if (creating() && data(); as result) {
      <app-expense-dialog [fundId]="fundId()" [balance]="result.summary.balance" [allowIncome]="true" (closed)="closeNew()" (saved)="onSaved()" />
    }

    @if (viewing(); as detail) {
      <kendo-dialog [width]="560" [maxWidth]="'calc(100vw - 32px)'" (close)="viewing.set(null)">
        <div class="view">
          <div class="title-row"><h2>{{ detail.entry.title }}</h2><app-scope-badge scope="General" /></div>
          <dl class="meta">
            <dt>Ngày</dt>
            <dd>{{ detail.entry.transactionDate | vnDate }}</dd>
            <dt>Danh mục</dt>
            <dd>{{ detail.entry.categoryName ?? '–' }}</dd>
            <dt>Số tiền</dt>
            <dd class="cf-money left" [class.cf-income]="detail.entry.type === 'Income'" [class.cf-expense]="detail.entry.type === 'Expense'">
              {{ signed(detail.entry) | vndMoney: true }}
            </dd>
            @if (detail.payerName) {
              <dt>Thu từ</dt>
              <dd>{{ detail.payerName }}</dd>
            }
            <dt>Người thực hiện</dt>
            <dd>{{ detail.entry.performedByName }}</dd>
            @if (detail.purpose) {
              <dt>Mục đích</dt>
              <dd>{{ detail.purpose }}</dd>
            }
          </dl>
          @if (detail.attachments.length) {
            <app-attachment-gallery [fundId]="fundId()" [attachments]="detail.attachments" />
          } @else if (detail.entry.type === 'Expense') {
            <div class="cf-alert">Khoản chi này không kèm chứng từ (≤ 500.000 đ được phép không có chứng từ).</div>
          }
          <p class="cf-muted small">Bạn xem được các khoản chi công khai và các khoản bạn đã nộp; không xem khoản đóng của bạn khác.</p>
        </div>
        <kendo-dialog-actions layout="end">
          <button kendoButton themeColor="primary" type="button" (click)="viewing.set(null)">Đóng</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }
  `,
  // Theo canvas L-01 (LedgerBody.dc.html + cf.css); màu dùng token --cf-*.
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 24px;
      max-width: var(--cf-content-max);
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      justify-content: space-between;
      gap: 16px;
    }
    .head-t {
      display: flex;
      flex-direction: column;
      gap: 4px;
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
    .title-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .kpis {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
    }
    .kpi {
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding: 16px 20px;
    }
    .kpi-l {
      color: var(--cf-text-muted);
      font-size: 13px;
      font-weight: 500;
    }
    .kpi-v {
      font-size: 24px;
      line-height: 32px;
      font-weight: 600;
      letter-spacing: -0.02em;
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
    .category {
      width: 170px;
    }
    .date {
      width: 150px;
    }
    .tabs {
      display: inline-flex;
      flex-wrap: wrap;
      gap: 4px;
      padding: 4px;
      border-radius: var(--cf-radius-lg);
      background: var(--cf-subtle);
    }
    .tab {
      display: inline-flex;
      align-items: center;
      height: 28px;
      padding: 0 12px;
      border: 0;
      border-radius: var(--cf-radius-md);
      background: transparent;
      color: var(--cf-text-muted);
      font: inherit;
      font-size: 13px;
      font-weight: 500;
      cursor: pointer;
    }
    .tab.on {
      background: var(--cf-surface);
      color: var(--cf-ink);
      box-shadow: 0 1px 2px rgba(0, 0, 0, 0.08);
    }
    .entry-title {
      font-weight: 500;
    }
    .strike {
      color: var(--cf-text-muted);
      text-decoration: line-through;
    }
    .tags {
      margin-top: 4px;
    }
    .amount {
      display: block;
    }
    :host ::ng-deep .num-head .k-cell-inner,
    :host ::ng-deep .right-head .k-cell-inner {
      justify-content: flex-end;
    }
    kendo-svgicon {
      margin-right: 2px;
    }
    .right {
      display: flex;
      justify-content: flex-end;
    }
    .small-btn {
      min-height: 32px;
      padding: 0 10px;
      font-size: 13px;
    }
    .card-f {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      padding: 12px 16px;
      border-top: 1px solid var(--cf-border);
    }
    .view {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    dl.meta {
      display: grid;
      grid-template-columns: 150px 1fr;
      gap: 10px 16px;
      margin: 0;
    }
    dl.meta dt {
      color: var(--cf-text-muted);
    }
    dl.meta dd {
      margin: 0;
    }
    .left {
      text-align: left;
    }
  `,
})
export class LedgerPage {
  private readonly api = inject(LedgerApi);
  private readonly categoriesApi = inject(CategoriesApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly currentFund = inject(CurrentFund);
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  protected readonly plus = plusIcon;
  protected readonly allCategories = ALL_CATEGORIES;
  protected readonly typeOptions: readonly { value: LedgerTypeFilter; label: string }[] = [
    { value: 'all', label: 'Tất cả' },
    { value: 'Income', label: 'Thu' },
    { value: 'Expense', label: 'Chi' },
  ];
  protected readonly tags = entryTags;
  protected readonly signed = signedAmount;

  private readonly range = defaultRange();
  protected readonly type = signal<LedgerTypeFilter>('all');
  protected readonly categoryId = signal<string>('');
  protected readonly from = signal<Date | null>(this.range.from);
  protected readonly to = signal<Date | null>(this.range.to);
  protected readonly page = signal(1);
  protected readonly creating = signal(this.route.snapshot.data['openNew'] === true);
  protected readonly viewing = signal<LedgerEntryDetail | null>(null);
  protected readonly maxFrom = computed(() => this.to() ?? new Date(2100, 0, 1));
  protected readonly minTo = computed(() => this.from() ?? new Date(2000, 0, 1));
  protected readonly canManage = computed(() => this.currentFund.can(Permissions.LedgerManage));

  protected readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), { initialValue: '' });

  protected readonly categories = toSignal(
    toObservable(this.fundId).pipe(switchMap((fundId) => this.categoriesApi.list(fundId).pipe(catchError(() => of<Category[]>([]))))),
    { initialValue: [] as Category[] },
  );

  private readonly query = computed(() => ({
    fundId: this.fundId(),
    type: this.type() === 'all' ? null : (this.type() as Exclude<LedgerTypeFilter, 'all'>),
    categoryId: this.categoryId() || null,
    from: toApiDate(this.from()),
    to: toApiDate(this.to()),
    page: this.page(),
    pageSize: LEDGER_PAGE_SIZE,
  }));

  private readonly state = toSignal(
    combineLatest([toObservable(this.query), this.reload$]).pipe(switchMap(([query]) => toLoadState(this.api.list(query.fundId, query)))),
    { initialValue: LOADING as LoadState<LedgerList> },
  );

  /** Dữ liệu gần nhất: giữ bảng cũ khi đang tải bộ lọc / trang mới. */
  protected readonly data = signal<LedgerList | null>(null);
  protected readonly loading = computed(() => this.state().status === 'loading');
  protected readonly errorKind = computed(() => {
    const current = this.state();
    return current.status === 'error' ? stateKind(current) : null;
  });
  protected readonly pageCount = computed(() => Math.max(1, Math.ceil((this.data()?.total ?? 0) / LEDGER_PAGE_SIZE)));
  protected readonly periodLabel = computed(() => monthLabel(this.from(), this.to()) ?? 'trong kỳ');
  protected readonly rangeLabel = computed(() => {
    const month = monthLabel(this.from(), this.to());
    if (month) {
      return `${month}/${this.from()!.getFullYear()}`;
    }
    return this.from() || this.to() ? 'theo khoảng ngày đã chọn' : 'mọi thời gian';
  });

  constructor() {
    effect(() => {
      const current = this.state();
      if (current.status === 'ready') {
        this.data.set(current.data);
      }
    });
  }

  protected shortDate(value: string): string {
    const [, month, day] = value.split('-');
    return `${day}/${month}`;
  }

  protected setType(value: LedgerTypeFilter): void {
    this.type.set(value);
    this.page.set(1);
  }

  protected setCategory(value: string | null): void {
    this.categoryId.set(value ?? '');
    this.page.set(1);
  }

  protected setFrom(value: Date | null): void {
    this.from.set(value);
    this.page.set(1);
  }

  protected setTo(value: Date | null): void {
    this.to.set(value);
    this.page.set(1);
  }

  protected go(step: number): void {
    this.page.update((current) => Math.min(Math.max(1, current + step), this.pageCount()));
  }

  protected openNew(): void {
    this.creating.set(true);
  }

  protected closeNew(): void {
    this.creating.set(false);
    if (this.route.snapshot.data['openNew']) {
      void this.router.navigate(['..'], { relativeTo: this.route });
    }
  }

  protected onSaved(): void {
    this.closeNew();
    this.reload$.next();
  }

  protected view(row: LedgerEntry): void {
    this.api.get(this.fundId(), row.id).subscribe({ next: (detail) => this.viewing.set(detail) });
  }
}

/** "tháng 11" khi khoảng lọc nằm trong một tháng; ngược lại null. */
function monthLabel(from: Date | null, to: Date | null): string | null {
  if (!from || !to || from.getFullYear() !== to.getFullYear() || from.getMonth() !== to.getMonth()) {
    return null;
  }
  return `tháng ${from.getMonth() + 1}`;
}
