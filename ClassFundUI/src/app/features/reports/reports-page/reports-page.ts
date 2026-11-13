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
import { RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DropDownListComponent } from '@progress/kendo-angular-dropdowns';
import { LabelComponent } from '@progress/kendo-angular-label';
import { Subscription, map } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { Permissions } from '../../../core/auth/permissions';
import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { FundsApi } from '../../funds/funds.api';
import { dmy, monthOptions, vietnamToday } from '../../transactions/ledger-dates';
import { Transaction, TransactionType } from '../../transactions/transactions.models';
import { ReportsApi } from '../reports.api';
import { CategoryReportLine, MonthFigures, MonthlyReport, YearlyReport } from '../reports.models';

type ReportView = 'month' | 'year';

interface YearRow extends MonthFigures {
  /** Tháng sau tháng hiện tại (giờ Việt Nam): để trống. */
  future: boolean;
}

/** Chiều cao cột cao nhất của biểu đồ (px) trong khung 170 px – Canvas `Reports`. */
const BAR_MAX = 144;

const number = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });
const MINUS = '−';

/**
 * S-03 "Báo cáo" – Canvas board `Reports` (thủ quỹ, GVCN, học sinh, phụ huynh); FR-08, FR-09, BR-10, BR-15, P1.
 * Hai chế độ "Báo cáo tháng | 12 tháng" + chọn tháng / năm (từ khi tạo quỹ tới hiện tại). Số tổng giống nhau ở mọi vai trò;
 * học sinh, phụ huynh thấy bản công khai: phần không công khai gộp "Khoản khác", không số lượng (backend đã gộp).
 */
@Component({
  selector: 'app-reports-page',
  imports: [RouterLink, ButtonComponent, DropDownListComponent, LabelComponent, AppState, PageHeader, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <app-page-header title="Báo cáo" [subtitle]="subtitle()">
        <div class="controls">
          <div class="segmented" role="group" aria-label="Loại báo cáo">
            <button kendoButton type="button" [class.on]="view() === 'month'" [attr.aria-pressed]="view() === 'month'" (click)="view.set('month')">
              Báo cáo tháng
            </button>
            <button kendoButton type="button" [class.on]="view() === 'year'" [attr.aria-pressed]="view() === 'year'" (click)="view.set('year')">
              12 tháng
            </button>
          </div>
          @if (view() === 'month') {
            <div class="field">
              <kendo-label [for]="monthPicker" text="Tháng" />
              <kendo-dropdownlist
                #monthPicker
                [data]="months()"
                textField="label"
                valueField="value"
                [valuePrimitive]="true"
                [value]="month()"
                (valueChange)="month.set($event)"
              />
            </div>
          } @else {
            <div class="field">
              <kendo-label [for]="yearPicker" text="Năm" />
              <kendo-dropdownlist
                #yearPicker
                [data]="years()"
                textField="label"
                valueField="value"
                [valuePrimitive]="true"
                [value]="year()"
                (valueChange)="year.set($event)"
              />
            </div>
          }
        </div>
      </app-page-header>

      @if (!full()) {
        <div class="public-note" role="note">
          Bản công khai: số tổng giống báo cáo của thủ quỹ. Các khoản không công khai được gộp vào “Khoản khác” – không hiện nội
          dung, ngày, danh mục hay số lượng khoản. Khoản công khai đã hủy xem trong Thu chi (lọc “Đã hủy”).
        </div>
      }

      @if (view() === 'month') {
        @switch (monthlyKind()) {
          @case (null) {
            @if (monthlyData(); as report) {
              <div class="kpis" aria-label="Số liệu tháng">
                <div class="cf-card kpi">
                  <span class="cf-muted">Số dư đầu tháng</span>
                  <b class="cf-money">{{ report.openingBalance | vndMoney }}</b>
                </div>
                <div class="cf-card kpi">
                  <span class="cf-muted">Tổng thu</span>
                  <b class="cf-money cf-income">{{ report.totalIncome | vndMoney: true }}</b>
                </div>
                <div class="cf-card kpi">
                  <span class="cf-muted">Tổng chi</span>
                  <b class="cf-money cf-expense">{{ -report.totalExpense | vndMoney: true }}</b>
                </div>
                <div class="cf-card kpi">
                  <span class="cf-muted">Chênh lệch</span>
                  <b class="cf-money">{{ report.netChange | vndMoney: true }}</b>
                </div>
                <div class="cf-card kpi">
                  <span class="cf-muted">Số dư cuối tháng</span>
                  <b class="cf-money closing">{{ report.closingBalance | vndMoney }}</b>
                </div>
              </div>
              <p class="hint cf-muted">
                Số dư đầu tháng {{ monthLabel() }} = số dư cuối tháng trước ({{ report.openingBalance | vndMoney }}). Sửa hoặc hủy
                giao dịch tháng cũ sẽ tự tính lại mọi tháng sau.
              </p>

              <div class="categories">
                @for (group of groups(); track group.type) {
                  <section class="cf-card table-card" [attr.aria-labelledby]="'rp-' + group.type">
                    <h2 [id]="'rp-' + group.type">{{ group.title }}</h2>
                    <table class="tbl cat">
                      <colgroup>
                        <col />
                        <col class="w-count" />
                        <col class="w-amount" />
                      </colgroup>
                      <thead>
                        <tr>
                          <th scope="col">Danh mục</th>
                          <th scope="col" class="num">Số khoản</th>
                          <th scope="col" class="num">Tổng</th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (line of group.lines; track line.categoryId ?? 'other') {
                          <tr>
                            @if (line.categoryId === null) {
                              <td class="other">{{ line.categoryName }} (không công khai chi tiết)</td>
                              <td class="num cf-muted">—</td>
                            } @else {
                              <td>{{ line.categoryName }}</td>
                              <td class="num">{{ line.count }}</td>
                            }
                            <td class="cf-money" [class.cf-income]="group.type === 'Income'" [class.cf-expense]="group.type === 'Expense'">
                              {{ signedAmount(group.type, line.amount) | vndMoney: true }}
                            </td>
                          </tr>
                        } @empty {
                          <tr>
                            <td colspan="3" class="cf-muted">{{ group.empty }}</td>
                          </tr>
                        }
                      </tbody>
                      <tfoot>
                        <tr>
                          <th scope="row">{{ group.totalLabel }}</th>
                          <td></td>
                          <td class="cf-money total" [class.cf-income]="group.type === 'Income'" [class.cf-expense]="group.type === 'Expense'">
                            {{ signedAmount(group.type, group.total) | vndMoney: true }}
                          </td>
                        </tr>
                      </tfoot>
                    </table>
                  </section>
                }
              </div>

              <section class="cf-card table-card" aria-labelledby="rp-transactions">
                <div class="card-head">
                  <h2 id="rp-transactions">
                    {{ full() ? 'Giao dịch trong tháng' : 'Khoản công khai trong tháng' }} ({{ report.transactions.length }})
                  </h2>
                  <a [routerLink]="['/funds', fundId(), 'transactions']">Mở trong Thu chi</a>
                </div>
                @if (report.transactions.length) {
                  <table class="tbl tx" aria-label="Danh sách khoản trong tháng">
                    <colgroup>
                      <col class="w-date" />
                      <col class="w-desc" />
                      <col />
                      <col class="w-tx-amount" />
                    </colgroup>
                    <tbody>
                      @for (tx of oldestFirst(report.transactions); track tx.id) {
                        <tr>
                          <td class="cf-muted date">{{ date(tx.occurredOn) }}</td>
                          <td class="desc">{{ tx.description }}</td>
                          <td class="cf-muted">{{ tx.categoryName }}</td>
                          <td class="cf-money amount" [class.cf-income]="tx.type === 'Income'" [class.cf-expense]="tx.type === 'Expense'">
                            {{ signedAmount(tx.type, tx.amount) | vndMoney: true }}
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                } @else {
                  <p class="none cf-muted" role="status">
                    {{ full() ? 'Không có giao dịch trong tháng này.' : 'Không có khoản công khai trong tháng này.' }}
                  </p>
                }
                <p class="foot cf-muted">
                  @if (full()) {
                    Không gồm khoản đã hủy – xem bằng bộ lọc “Đã hủy” trong Thu chi.
                  } @else {
                    Chỉ khoản công khai đang hiệu lực. Khoản không công khai nằm trong dòng “Khoản khác” ở bảng danh mục.
                  }
                </p>
              </section>
            }
          }
          @case ('error') {
            <app-state kind="error" actionLabel="Thử lại" (action)="reload()" />
          }
          @default {
            <app-state [kind]="monthlyKind()!" />
          }
        }
      } @else {
        @switch (yearlyKind()) {
          @case (null) {
            <section class="cf-card chart-card" aria-labelledby="rp-chart">
              <h2 id="rp-chart">Thu chi theo tháng – {{ year() }}</h2>
              <div class="chart" role="img" [attr.aria-label]="chartLabel()">
                @for (row of yearRows(); track row.month) {
                  <div class="bars" [title]="barTitle(row)">
                    <span class="bar income" [style.height.px]="barHeight(row.totalIncome)"></span>
                    <span class="bar expense" [style.height.px]="barHeight(row.totalExpense)"></span>
                  </div>
                }
              </div>
              <div class="axis" aria-hidden="true">
                @for (row of yearRows(); track row.month) {
                  <span>T{{ row.month }}</span>
                }
              </div>
            </section>

            <section class="cf-card table-card" aria-labelledby="rp-year">
              <h2 id="rp-year">Bảng 12 tháng <span class="unit cf-muted">· đơn vị: đồng</span></h2>
              <table class="tbl year">
                <thead>
                  <tr>
                    <th scope="col">Tháng</th>
                    <th scope="col" class="num">Đầu tháng</th>
                    <th scope="col" class="num">Thu</th>
                    <th scope="col" class="num">Chi</th>
                    <th scope="col" class="num">Chênh lệch</th>
                    <th scope="col" class="num">Cuối tháng</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of yearRows(); track row.month) {
                    <tr [class.future]="row.future">
                      <th scope="row">T{{ row.month }}/{{ year() }}</th>
                      @if (row.future) {
                        <td colspan="5"></td>
                      } @else {
                        <td class="cf-money">{{ plain(row.openingBalance) }}</td>
                        <td class="cf-money cf-income">{{ plain(row.totalIncome, true) }}</td>
                        <td class="cf-money cf-expense">{{ plain(-row.totalExpense, true) }}</td>
                        <td class="cf-money">{{ plain(row.netChange, true) }}</td>
                        <td class="cf-money total">{{ plain(row.closingBalance) }}</td>
                      }
                    </tr>
                  }
                </tbody>
              </table>
              <p class="foot cf-muted">Tháng tương lai để trống. Cuối tháng trước luôn bằng đầu tháng sau.</p>
            </section>
          }
          @case ('error') {
            <app-state kind="error" actionLabel="Thử lại" (action)="reload()" />
          }
          @default {
            <app-state [kind]="yearlyKind()!" />
          }
        }
      }
    </section>
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
    .controls {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: 12px;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 4px;
      width: 180px;
    }
    :host ::ng-deep .controls .k-label {
      color: var(--cf-text-muted);
      font-size: 12px;
      font-weight: 600;
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
    .public-note {
      padding: 10px 14px;
      border-radius: var(--cf-radius-md);
      background: var(--cf-success-bg);
      color: var(--cf-success-fg);
    }
    .kpis {
      display: grid;
      grid-template-columns: repeat(5, minmax(0, 1fr));
      gap: 12px;
    }
    .kpi {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .kpi b {
      font-size: 20px;
      text-align: left;
    }
    .closing {
      color: var(--cf-primary);
    }
    .hint {
      margin: -4px 0 0;
      font-size: 13px;
    }
    .categories {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 16px;
      align-items: start;
    }
    .table-card {
      padding: 0;
      overflow: hidden;
    }
    h2 {
      margin: 0;
      font-size: 16px;
    }
    .table-card > h2 {
      padding: 14px 16px 10px;
    }
    .card-head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      padding: 14px 16px 10px;
    }
    .unit {
      font-size: 12px;
      font-weight: 400;
    }
    .tbl {
      width: 100%;
      border-collapse: collapse;
      table-layout: fixed;
    }
    .tbl th,
    .tbl td {
      padding: 9px 12px;
      text-align: left;
      overflow-wrap: anywhere;
    }
    .tbl thead th {
      padding: 8px 12px;
      background: var(--cf-background);
      color: var(--cf-text-muted);
      font-size: 12px;
      font-weight: 600;
    }
    .tbl tbody th {
      font-weight: 400;
    }
    .tbl tbody tr {
      border-bottom: 1px solid var(--cf-subtle);
    }
    .tbl .num,
    .tbl .cf-money {
      text-align: right;
    }
    .w-count {
      width: 94px;
    }
    .w-amount {
      width: 154px;
    }
    .w-date {
      width: 120px;
    }
    .w-desc {
      width: 50%;
    }
    .w-tx-amount {
      width: 164px;
    }
    .tbl tfoot th,
    .tbl .total {
      font-weight: 700;
    }
    .other {
      font-style: italic;
    }
    .tx .amount {
      font-weight: 600;
    }
    .desc {
      font-weight: 600;
    }
    .none {
      margin: 0;
      padding: 16px;
      text-align: center;
    }
    .foot {
      margin: 0;
      padding: 10px 16px 12px;
      font-size: 12px;
    }
    .chart-card {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .chart,
    .axis {
      display: grid;
      grid-template-columns: repeat(12, minmax(0, 1fr));
      gap: 8px;
    }
    .chart {
      align-items: end;
      height: 170px;
      border-bottom: 1px solid var(--cf-border);
    }
    .bars {
      display: flex;
      align-items: flex-end;
      justify-content: center;
      gap: 3px;
      height: 100%;
    }
    .bar {
      width: 12px;
      border-radius: 2px 2px 0 0;
    }
    .bar.income {
      background: var(--cf-income);
    }
    .bar.expense {
      background: #e8a39d;
    }
    .axis {
      margin-top: -4px;
      color: var(--cf-text-muted);
      font-size: 12px;
      text-align: center;
    }
    .year tbody th {
      font-weight: 400;
    }
    .year tr.future th {
      color: var(--cf-text-muted);
    }
  `,
})
export class ReportsPage {
  private readonly api = inject(ReportsApi);
  private readonly funds = inject(FundsApi);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  /** `:fundId` của route cha. */
  readonly fundId = input.required<string>();

  private readonly today = vietnamToday();
  private readonly currentMonth = this.today.slice(0, 7);
  private readonly currentYear = Number(this.today.slice(0, 4));

  protected readonly view = signal<ReportView>('month');
  protected readonly month = signal(this.currentMonth);
  protected readonly year = signal(this.currentYear);
  private readonly fundCreatedAt = signal<string | null>(null);

  protected readonly monthly = signal<LoadState<MonthlyReport>>(LOADING);
  protected readonly yearly = signal<LoadState<YearlyReport>>(LOADING);
  private request?: Subscription;

  private readonly membership = computed(() => this.auth.membership(this.fundId()));
  protected readonly full = computed(() => (this.membership()?.permissions ?? []).includes(Permissions.LedgerViewFull));
  protected readonly subtitle = computed(() => {
    const membership = this.membership();
    if ((membership?.permissions ?? []).includes(Permissions.TransactionManage)) {
      return 'Đầy đủ, tự tính từ giao dịch đang hiệu lực';
    }
    if (this.full()) {
      return 'Đầy đủ · GVCN chỉ xem';
    }
    return membership?.role === 'Parent' ? 'Bản công khai cho phụ huynh · mọi tháng' : 'Bản công khai cho học sinh · mọi tháng';
  });

  /** Mọi tháng từ khi tạo quỹ tới tháng hiện tại, mới nhất trước (FR-08 "mọi tháng"). */
  protected readonly months = computed(() => monthOptions(this.fundCreatedAt(), this.today).filter((option) => option.value));
  protected readonly years = computed(() => {
    const first = this.fundCreatedAt() ? Number(vietnamToday(new Date(this.fundCreatedAt()!)).slice(0, 4)) : this.currentYear;
    const years = [];
    for (let year = this.currentYear; year >= Math.min(first, this.currentYear); year--) {
      years.push({ value: year, label: `Năm ${year}` });
    }
    return years;
  });
  protected readonly monthLabel = computed(() => `${this.month().slice(5)}/${this.month().slice(0, 4)}`);

  protected readonly monthlyKind = computed(() => stateKind(this.monthly(), () => false));
  protected readonly monthlyData = computed(() => {
    const state = this.monthly();
    return state.status === 'ready' ? state.data : null;
  });
  protected readonly groups = computed(() => {
    const report = this.monthlyData();
    const lines = report?.byCategory ?? [];
    const group = (type: TransactionType, title: string, totalLabel: string, empty: string, total: number) => ({
      type,
      title,
      totalLabel,
      empty,
      total,
      lines: lines.filter((line: CategoryReportLine) => line.type === type),
    });
    return [
      group('Income', 'Thu theo danh mục', 'Tổng thu', 'Không có khoản thu trong tháng.', report?.totalIncome ?? 0),
      group('Expense', 'Chi theo danh mục', 'Tổng chi', 'Không có khoản chi trong tháng.', report?.totalExpense ?? 0),
    ];
  });

  protected readonly yearlyKind = computed(() => stateKind(this.yearly(), () => false));
  protected readonly yearRows = computed<YearRow[]>(() => {
    const state = this.yearly();
    if (state.status !== 'ready') {
      return [];
    }
    return state.data.months.map((figures) => ({
      ...figures,
      future: `${state.data.year}-${String(figures.month).padStart(2, '0')}` > this.currentMonth,
    }));
  });
  private readonly barScale = computed(() =>
    Math.max(0, ...this.yearRows().flatMap((row) => (row.future ? [] : [row.totalIncome, row.totalExpense]))),
  );
  protected readonly chartLabel = computed(() => {
    const active = this.yearRows().filter((row) => !row.future && (row.totalIncome > 0 || row.totalExpense > 0));
    return active.length
      ? `Biểu đồ cột thu chi 12 tháng năm ${this.year()}: có dữ liệu từ tháng ${active[0].month} đến tháng ${active[active.length - 1].month}`
      : `Biểu đồ cột thu chi 12 tháng năm ${this.year()}: chưa có giao dịch`;
  });

  constructor() {
    effect(() => {
      const fundId = this.fundId();
      untracked(() => this.loadFund(fundId));
    });
    effect(() => {
      const key = { fundId: this.fundId(), view: this.view(), month: this.month(), year: this.year() };
      untracked(() => this.load(key.fundId, key.view, key.month, key.year));
    });
    this.destroyRef.onDestroy(() => this.request?.unsubscribe());
  }

  protected reload(): void {
    this.load(this.fundId(), this.view(), this.month(), this.year());
  }

  protected date(value: string): string {
    return dmy(value);
  }

  protected signedAmount(type: TransactionType, amount: number): number {
    return type === 'Income' ? amount : -amount;
  }

  /** Danh sách theo ngày tăng dần như Canvas (API trả mới nhất trước). */
  protected oldestFirst(items: Transaction[]): Transaction[] {
    return [...items].reverse();
  }

  /** Bảng 12 tháng: số không kèm "đ" (đơn vị ghi ở tiêu đề); 0 không có dấu. */
  protected plain(value: number, signed = false): string {
    const sign = value < 0 ? MINUS : signed && value > 0 ? '+' : '';
    return `${sign}${number.format(Math.abs(value))}`;
  }

  protected barHeight(value: number): number {
    const scale = this.barScale();
    return scale > 0 ? Math.round((value / scale) * BAR_MAX) : 0;
  }

  protected barTitle(row: YearRow): string {
    return row.future ? `Tháng ${row.month}` : `Tháng ${row.month}: thu ${this.plain(row.totalIncome)} đ, chi ${this.plain(row.totalExpense)} đ`;
  }

  private load(fundId: string, view: ReportView, month: string, year: number): void {
    this.request?.unsubscribe();
    if (view === 'month') {
      const [y, m] = month.split('-').map(Number);
      this.request = toLoadState(this.api.monthly(fundId, y, m)).subscribe((state) => this.monthly.set(state));
    } else {
      this.request = toLoadState(this.api.yearly(fundId, year)).subscribe((state) => this.yearly.set(state));
    }
  }

  private loadFund(fundId: string): void {
    this.funds
      .list()
      .pipe(
        map((funds) => funds.find((fund) => fund.id === fundId)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({ next: (fund) => this.fundCreatedAt.set(fund?.createdAt ?? null), error: () => this.fundCreatedAt.set(null) });
  }
}
