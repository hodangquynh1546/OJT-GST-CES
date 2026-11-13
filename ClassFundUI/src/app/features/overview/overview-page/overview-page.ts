import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { ChartModule } from '@progress/kendo-angular-charts';
import { forkJoin } from 'rxjs';

import { Permissions } from '../../../core/auth/permissions';
import { CurrentFund } from '../../../core/services/current-fund.service';
import { AppNotifications } from '../../../core/services/notification.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { CategoriesApi } from '../../categories/categories.api';
import { Category } from '../../categories/categories.models';
import { dmy, vietnamToday } from '../../transactions/ledger-dates';
import { Transaction, TransactionType } from '../../transactions/transactions.models';
import { TxDialog } from '../../transactions/tx-dialog/tx-dialog';
import { ReportsApi } from '../../reports/reports.api';
import { OverviewApi } from '../overview.api';
import { FundOverview, TrendPoint } from '../overview.models';
import { buildTrend, lastThreeMonths, yearsNeeded } from '../overview-trend';

/**
 * S-01 "Tổng quan" – Canvas board `Overview`; FR-08, BR-02, BR-04, BR-15. Số tổng giống nhau ở mọi vai trò; học sinh / phụ huynh
 * chỉ thấy khoản công khai trong danh sách gần đây (backend đã lọc). Thủ quỹ thêm khoản thu / chi bằng T-01 của M6.
 * Biểu đồ 3 tháng có trạng thái riêng: lỗi `reports/yearly` không làm mất các thẻ số liệu.
 */
@Component({
  selector: 'app-overview-page',
  imports: [ButtonComponent, ChartModule, RouterLink, AppState, PageHeader, TxDialog, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <app-page-header title="Tổng quan" [subtitle]="subtitle()">
        @if (canManage() && overview()) {
          <div class="head-actions">
            <button kendoButton type="button" (click)="openAdd('Income')">+ Khoản thu</button>
            <button kendoButton themeColor="primary" type="button" (click)="openAdd('Expense')">+ Khoản chi</button>
          </div>
        }
      </app-page-header>

      @if (overview(); as data) {
        @if (data.currentBalance < 0) {
          <div class="cf-alert cf-alert--warning" role="status" data-testid="negative-balance">
            Số dư quỹ đang âm. Khoản chi vẫn được lưu nhưng cần kiểm tra lại các khoản thu chi.
          </div>
        }
        @if (data.treasurerStatus === 'Unassigned') {
          <div class="cf-alert cf-alert--warning" role="status" data-testid="no-treasurer">
            Chưa phân công thủ quỹ – chưa ai ghi được thu chi.
            @if (canAssign()) {
              <a [routerLink]="link('treasurers')">Phân công thủ quỹ</a>
            }
          </div>
        }
        @if (!full()) {
          <div class="cf-alert" role="note" data-testid="public-note">
            Bạn đang xem bản công khai: chỉ hiện các khoản thu chi được thủ quỹ công khai. Số dư và các số tổng là của cả quỹ.
          </div>
        }

        <div class="cf-kpis" aria-label="Số liệu quỹ">
          <article class="cf-card kpi">
            <span class="cf-muted label">Số dư hiện tại</span>
            <b class="value cf-money" [class.cf-expense]="data.currentBalance < 0" data-testid="kpi-balance">
              {{ data.currentBalance | vndMoney }}
            </b>
          </article>
          <article class="cf-card kpi">
            <span class="cf-muted label">Tổng thu</span>
            <b class="value cf-money cf-income" data-testid="kpi-income">{{ data.totalIncome | vndMoney }}</b>
          </article>
          <article class="cf-card kpi">
            <span class="cf-muted label">Tổng chi</span>
            <b class="value cf-money cf-expense" data-testid="kpi-expense">{{ data.totalExpense | vndMoney }}</b>
          </article>
          <article class="cf-card kpi">
            <span class="cf-muted label">Chênh lệch tháng này</span>
            <b class="value cf-money" [class.cf-income]="data.currentMonth.net > 0" [class.cf-expense]="data.currentMonth.net < 0" data-testid="kpi-month">
              {{ data.currentMonth.net | vndMoney: true }}
            </b>
            <span class="cf-muted sub">
              Thu {{ data.currentMonth.income | vndMoney }} · Chi {{ data.currentMonth.expense | vndMoney }}
            </span>
          </article>
        </div>

        <div class="layout">
          <div class="main">
            <section class="cf-card" aria-labelledby="ov-recent">
              <div class="card-head">
                <h2 id="ov-recent">Giao dịch gần đây</h2>
                <a [routerLink]="link('transactions')">Xem tất cả ›</a>
              </div>
              @if (data.recentTransactions.length === 0) {
                <p class="cf-muted none" role="status">
                  Chưa có khoản thu chi nào.{{ canManage() ? ' Bấm “+ Khoản thu” hoặc “+ Khoản chi” để ghi khoản đầu tiên.' : '' }}
                </p>
              } @else {
                <ul class="recent" data-testid="recent">
                  @for (item of data.recentTransactions; track item.id) {
                    <li>
                      <span class="date cf-muted">{{ dmy(item.occurredOn) }}</span>
                      <span class="what">
                        <b>{{ item.description }}</b>
                        <span class="cf-muted">{{ item.categoryName ?? 'Khoản khác' }}</span>
                      </span>
                      <span class="cf-money amount" [class.cf-income]="item.type === 'Income'" [class.cf-expense]="item.type === 'Expense'">
                        {{ signed(item) | vndMoney: true }}
                      </span>
                    </li>
                  }
                </ul>
              }
            </section>

            <section class="cf-card" aria-labelledby="ov-chart">
              <div class="card-head">
                <h2 id="ov-chart">Thu chi 3 tháng gần nhất</h2>
              </div>
              @if (trend(); as points) {
                <kendo-chart class="chart" [style.height.px]="240" data-testid="trend-chart">
                  <kendo-chart-category-axis>
                    <kendo-chart-category-axis-item [categories]="labels()" />
                  </kendo-chart-category-axis>
                  <kendo-chart-value-axis>
                    <kendo-chart-value-axis-item>
                      <kendo-chart-value-axis-item-labels format="n0" />
                    </kendo-chart-value-axis-item>
                  </kendo-chart-value-axis>
                  <kendo-chart-series>
                    <kendo-chart-series-item type="column" name="Thu" [data]="incomes()" [color]="colors.income" />
                    <kendo-chart-series-item type="column" name="Chi" [data]="expenses()" [color]="colors.expense" />
                  </kendo-chart-series>
                  <kendo-chart-legend position="bottom" />
                </kendo-chart>
                <table class="sr-only" aria-label="Thu chi 3 tháng gần nhất">
                  <thead>
                    <tr><th scope="col">Tháng</th><th scope="col">Thu</th><th scope="col">Chi</th></tr>
                  </thead>
                  <tbody>
                    @for (point of points; track point.label) {
                      <tr>
                        <th scope="row">{{ point.label }}</th>
                        <td>{{ point.income | vndMoney }}</td>
                        <td>{{ point.expense | vndMoney }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              } @else {
                @switch (trendKind()) {
                  @case ('loading') {
                    <app-state kind="loading" />
                  }
                  @default {
                    <app-state kind="error" title="Không tải được biểu đồ" actionLabel="Thử lại" (action)="loadTrend()" />
                  }
                }
              }
            </section>
          </div>

          <aside class="side">
            <section class="cf-card" aria-labelledby="ov-info">
              <div class="card-head">
                <h2 id="ov-info">Thông tin quỹ</h2>
              </div>
              <ul class="managers" data-testid="managers">
                @for (manager of data.managers; track $index) {
                  <li>
                    <span>{{ manager.fullName }}</span>
                    <span class="cf-tag" [class.cf-tag--success]="manager.role === 'Treasurer'">
                      {{ manager.role === 'Teacher' ? 'GVCN' : 'Thủ quỹ' }}
                    </span>
                  </li>
                }
              </ul>
              @if (data.treasurerStatus === 'Unassigned') {
                <p class="cf-muted small">Chưa có thủ quỹ.</p>
              }
            </section>

            @if (canAssign() || canManage()) {
              <section class="cf-card links" aria-label="Lối tắt">
                @if (canAssign()) {
                  <a [routerLink]="link('students')">Học sinh & phụ huynh</a>
                  <a [routerLink]="link('treasurers')">Phân công thủ quỹ</a>
                  <a [routerLink]="link('settings')">Đổi tên quỹ</a>
                }
                @if (canManage()) {
                  <a [routerLink]="link('categories')">Danh mục thu chi</a>
                  <a [routerLink]="link('members')">Thành viên quỹ</a>
                }
              </section>
            }
          </aside>
        </div>
      } @else {
        @switch (kind()) {
          @case ('error') {
            <app-state class="cf-card" kind="error" actionLabel="Thử lại" (action)="load()" />
          }
          @case ('loading') {
            <app-state kind="loading" />
          }
          @default {
            <app-state [kind]="kind() === 'forbidden' ? 'forbidden' : 'not-found'" />
          }
        }
      }
    </section>

    @if (adding(); as type) {
      <app-tx-dialog
        [fundId]="fundId()"
        [addType]="type"
        [transaction]="null"
        [categories]="categories()"
        [balance]="overview()?.currentBalance ?? null"
        (saved)="onSaved($event)"
        (closed)="adding.set(null)"
      />
    }
  `,
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 16px;
      max-width: var(--cf-content-max);
    }
    app-page-header {
      margin-bottom: 0;
    }
    .head-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .kpi {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .label {
      font-size: 13px;
    }
    .value {
      font-size: 24px;
      line-height: 32px;
      text-align: left;
    }
    .sub {
      font-size: 12px;
    }
    .layout {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: 16px;
    }
    .main {
      display: flex;
      flex: 999 1 600px;
      flex-direction: column;
      gap: 16px;
      min-width: 0;
    }
    .side {
      display: flex;
      flex: 1 1 280px;
      flex-direction: column;
      gap: 16px;
      min-width: 0;
    }
    .card-head {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      margin-bottom: 8px;
    }
    h2 {
      margin: 0;
      font-size: 16px;
      line-height: 24px;
    }
    .none {
      margin: 0;
      padding: 16px 0;
      text-align: center;
    }
    .recent,
    .managers {
      display: flex;
      flex-direction: column;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .recent li,
    .managers li {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 8px 0;
      border-top: 1px solid var(--cf-border);
    }
    .recent li:first-child,
    .managers li:first-child {
      border-top: 0;
    }
    .date {
      flex: 0 0 84px;
      font-size: 13px;
    }
    .what {
      display: flex;
      flex: 1 1 auto;
      flex-direction: column;
      min-width: 0;
      overflow-wrap: anywhere;
    }
    .what .cf-muted {
      font-size: 12px;
    }
    .amount {
      flex: 0 0 auto;
      font-weight: 600;
    }
    .managers li {
      justify-content: space-between;
    }
    .small {
      margin: 8px 0 0;
      font-size: 12px;
    }
    .links {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      margin: -1px;
      padding: 0;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }
  `,
})
export class OverviewPage {
  private readonly api = inject(OverviewApi);
  private readonly reports = inject(ReportsApi);
  private readonly categoriesApi = inject(CategoriesApi);
  private readonly currentFund = inject(CurrentFund);
  private readonly notifications = inject(AppNotifications);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);

  /** `:fundId` trên URL (component input binding). */
  readonly fundId = input.required<string>();

  protected readonly state = signal<LoadState<FundOverview>>(LOADING);
  protected readonly trendState = signal<LoadState<TrendPoint[]>>(LOADING);
  protected readonly categories = signal<Category[]>([]);
  protected readonly adding = signal<TransactionType | null>(null);

  protected readonly overview = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data : null;
  });
  protected readonly trend = computed(() => {
    const state = this.trendState();
    return state.status === 'ready' ? state.data : null;
  });
  protected readonly kind = computed(() => stateKind(this.state(), () => false));
  protected readonly trendKind = computed(() => stateKind(this.trendState(), () => false));
  protected readonly labels = computed(() => (this.trend() ?? []).map((point) => point.label));
  protected readonly incomes = computed(() => (this.trend() ?? []).map((point) => point.income));
  protected readonly expenses = computed(() => (this.trend() ?? []).map((point) => point.expense));

  protected readonly full = computed(() => this.currentFund.can(Permissions.LedgerViewFull));
  protected readonly canManage = computed(() => this.currentFund.can(Permissions.TransactionManage));
  protected readonly canAssign = computed(() => this.currentFund.can(Permissions.TreasurerAssign));
  protected readonly subtitle = computed(() => this.currentFund.membership()?.fundName ?? '');

  /** Màu cột lấy từ token Design System (không viết mã màu trong component); Kendo Chart cần giá trị cụ thể. */
  protected readonly colors = this.readColors();

  protected readonly dmy = dmy;

  constructor() {
    effect(() => {
      this.fundId();
      untracked(() => {
        this.load();
      });
    });
  }

  protected load(): void {
    this.state.set(LOADING);
    toLoadState(this.api.get(this.fundId()))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((state) => this.state.set(state));
    this.loadTrend();
  }

  protected loadTrend(): void {
    this.trendState.set(LOADING);
    const months = lastThreeMonths(vietnamToday());
    toLoadState(forkJoin(yearsNeeded(months).map((year) => this.reports.yearly(this.fundId(), year))))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((state) =>
        this.trendState.set(state.status === 'ready' ? { status: 'ready', data: buildTrend(months, state.data) } : state),
      );
  }

  protected link(path: string): string[] {
    return ['/funds', this.fundId(), path];
  }

  /** Thu hiện dương, chi hiện âm. */
  protected signed(item: Transaction): number {
    return item.type === 'Income' ? item.amount : -item.amount;
  }

  protected openAdd(type: TransactionType): void {
    this.categoriesApi
      .list(this.fundId(), { includeHidden: true })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (categories) => this.categories.set(categories),
        error: () => this.categories.set([]),
      });
    this.adding.set(type);
  }

  protected onSaved(saved: Transaction): void {
    this.adding.set(null);
    const money = new VndMoneyPipe();
    this.notifications.success(
      `Đã lưu ${saved.type === 'Income' ? 'khoản thu' : 'khoản chi'} ${money.transform(saved.amount)}.`,
    );
    if (saved.isNegativeBalance) {
      this.notifications.warning('Số dư quỹ đang âm. Khoản vẫn được lưu (BR-04).');
    }
    // Tải lại số liệu mà không nháy trạng thái loading.
    this.api
      .get(this.fundId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (data) => this.state.set({ status: 'ready', data }) });
    this.loadTrend();
  }

  private readColors(): { income: string | undefined; expense: string | undefined } {
    const style = this.document.defaultView?.getComputedStyle(this.document.documentElement);
    return {
      income: style?.getPropertyValue('--cf-income').trim() || undefined,
      expense: style?.getPropertyValue('--cf-expense').trim() || undefined,
    };
  }
}
