import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { firstValueFrom, forkJoin, switchMap } from 'rxjs';

import { Permissions } from '../../core/auth/permissions';
import { CurrentFund } from '../../core/services/current-fund.service';
import { AppNotifications } from '../../core/services/notification.service';
import { AppState } from '../../shared/components/app-state/app-state';
import { ScopeBadge } from '../../shared/components/scope-badge/scope-badge';
import { ApiError } from '../../shared/models/api-error';
import { PagedResult } from '../../shared/models/paged-result';
import { ASSIGNMENT_STATUS_LABEL, AssignmentStatus } from '../../shared/models/labels';
import { VnDatePipe } from '../../shared/pipes/vn-date.pipe';
import { VndMoneyPipe } from '../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../shared/state/load-state';
import { CategoriesApi } from '../categories/categories.api';
import { Category } from '../categories/categories.models';
import { MembersApi } from '../members/members.api';
import { ChargesApi } from './charges.api';
import { CHARGE_FIELD_FOR } from './charges.errors';
import { CHARGE_STATUS_LABEL, Charge } from './charges.models';
import { percentOf, todayInVietnam } from './charges.utils';
import { PayDialog, PayTarget } from './pay-dialog';

type Tab = 'Open' | 'Closed';

interface Lists {
  open: Charge[];
  closed: Charge[];
}

/**
 * H-01 Khoản thu chung và H-02 tạo khoản thu (FR-18, FR-24): thủ quỹ thấy tiến độ thu và mở trang theo dõi;
 * học sinh thấy tình trạng của mình và nộp tiền ngay trên dòng.
 */
@Component({
  selector: 'app-charges-page',
  imports: [FormsModule, RouterLink, ButtonComponent, DialogComponent, DialogActionsComponent, ScopeBadge, AppState, PayDialog, VndMoneyPipe, VnDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="head">
        <div class="text">
          <div class="title-row">
            <h1>Khoản thu chung</h1>
            <app-scope-badge scope="General" />
          </div>
          <span class="cf-muted">
            {{ canManage() ? 'Chọn một khoản thu để theo dõi ai đã đóng / chưa đóng.' : 'Các khoản thu chung của lớp và tình trạng của bạn. Nộp tiền ngay trên từng khoản.' }}
          </span>
        </div>
        @if (canManage() && !archived()) {
          <button kendoButton themeColor="primary" type="button" (click)="openCreate()">+ Tạo khoản thu</button>
        }
      </header>

      @if (kind(); as value) {
        <app-state [kind]="value" [title]="value === 'empty' ? 'Chưa có khoản thu nào' : undefined" />
      } @else {
        <div class="tabs" role="tablist" aria-label="Trạng thái khoản thu">
          <button type="button" role="tab" [class.active]="tab() === 'Open'" [attr.aria-selected]="tab() === 'Open'" (click)="tab.set('Open')">
            Đang mở {{ lists().open.length }}
          </button>
          <button type="button" role="tab" [class.active]="tab() === 'Closed'" [attr.aria-selected]="tab() === 'Closed'" (click)="tab.set('Closed')">
            Đã đóng {{ lists().closed.length }}
          </button>
        </div>

        <section class="cf-card list" aria-label="Danh sách khoản thu">
          @for (charge of visible(); track charge.id) {
            <article class="charge">
              <div class="main">
                <div class="head">
                  <b class="title">{{ charge.title }}</b>
                  <span class="cf-tag" [class.cf-tag--success]="charge.status === 'Open'">{{ statusLabel(charge) }}</span>
                  @if (!canManage() && charge.mine; as mine) {
                    <span class="cf-muted mine">Của tôi: <span [class]="mineClass(mine.status)">{{ assignmentLabel(mine.status) }}</span></span>
                  }
                </div>
                <div class="cf-muted meta">
                  {{ charge.amountPerPerson | vndMoney }}/người · hạn {{ charge.dueDate | vnDate }}@if (charge.categoryName) { · {{ charge.categoryName }} }@if (charge.purpose) { · {{ charge.purpose }} }
                </div>
                @if (charge.progress; as progress) {
                  <div class="progress">
                    <div class="bar" role="progressbar" [attr.aria-valuenow]="percent(progress.paidCount, progress.assignedCount)" aria-valuemin="0" aria-valuemax="100">
                      <span class="paid" [style.width.%]="percent(progress.paidCount, progress.assignedCount)"></span>
                      <span class="await" [style.width.%]="percent(progress.awaitingCount, progress.assignedCount)"></span>
                    </div>
                    <span class="cf-muted count">{{ progress.paidCount }}/{{ progress.assignedCount }} đã đóng</span>
                  </div>
                }
              </div>
              <div class="side">
                @if (charge.progress; as progress) {
                  <span class="cf-muted">{{ progress.awaitingCount }} chờ xác nhận</span>
                }
                @if (canManage()) {
                  <a class="cf-link-button" [routerLink]="['/funds', fundId(), 'charges', charge.id]">Theo dõi</a>
                } @else if (charge.mine?.status === 'Unpaid' && charge.status === 'Open') {
                  <button kendoButton themeColor="primary" type="button" (click)="pay(charge)">Nộp tiền</button>
                } @else if (charge.mine?.status === 'AwaitingConfirmation') {
                  <a class="cf-link-button" [routerLink]="['/funds', fundId(), 'my-payments']">Xem</a>
                }
              </div>
            </article>
          } @empty {
            <p class="cf-muted none">{{ tab() === 'Open' ? 'Chưa có khoản thu nào đang mở.' : 'Chưa có khoản thu nào đã đóng.' }}</p>
          }
        </section>
      }
    </div>

    @if (creating()) {
      <kendo-dialog title="Tạo khoản thu chung" [width]="520" (close)="closeCreate()">
        <p class="cf-muted dlg-sub">Áp cho mọi thành viên đang hoạt động, gồm cả thủ quỹ.</p>
        <form class="form" novalidate (submit)="$event.preventDefault()">
          <div class="field">
            <label for="charge-title">Tên khoản thu <span class="req" aria-hidden="true">*</span></label>
            <input id="charge-title" class="text" maxlength="150" [class.invalid]="errors().title" [ngModel]="title()" (ngModelChange)="title.set($event)" name="title" />
            @if (errors().title; as message) { <span class="k-form-error" role="alert">{{ message }}</span> }
          </div>
          <div class="field">
            <label for="charge-purpose">Mục đích</label>
            <textarea id="charge-purpose" class="text area" rows="3" maxlength="1000" [ngModel]="purpose()" (ngModelChange)="purpose.set($event)" name="purpose"></textarea>
          </div>
          <div class="field">
            <label for="charge-category">Danh mục thu <span class="req" aria-hidden="true">*</span></label>
            <select id="charge-category" class="text" [class.invalid]="errors().category" [ngModel]="categoryId()" (ngModelChange)="categoryId.set($event)" name="category">
              <option value="">Chọn danh mục thu</option>
              @for (category of categories(); track category.id) {
                <option [value]="category.id">{{ category.name }}</option>
              }
            </select>
            @if (errors().category; as message) { <span class="k-form-error" role="alert">{{ message }}</span> }
            @if (categories().length === 0) {
              <span class="cf-muted hint">Chưa có danh mục thu nào. Tạo ở mục Danh mục rồi quay lại.</span>
            }
          </div>
          <div class="row">
            <div class="field">
              <label for="charge-amount">Số tiền mỗi người (đ) <span class="req" aria-hidden="true">*</span></label>
              <input id="charge-amount" class="text money" inputmode="numeric" autocomplete="off" [class.invalid]="errors().amount" [ngModel]="amountText()" (ngModelChange)="setAmount($event)" name="amount" />
              @if (errors().amount; as message) { <span class="k-form-error" role="alert">{{ message }}</span> }
            </div>
            <div class="field">
              <label for="charge-due">Hạn đóng <span class="req" aria-hidden="true">*</span></label>
              <input id="charge-due" type="date" class="text" [min]="today" [class.invalid]="errors().dueDate" [ngModel]="dueDate()" (ngModelChange)="dueDate.set($event)" name="dueDate" />
              @if (errors().dueDate; as message) { <span class="k-form-error" role="alert">{{ message }}</span> }
            </div>
          </div>
          <p class="preview" aria-live="polite">
            Áp cho <b>{{ memberCount() }} thành viên</b> → tổng phải thu <b>{{ total() | vndMoney }}</b>. Mọi người nhận thông báo “[Quỹ chung] Khoản thu mới”.
          </p>
          @if (createError(); as message) {
            <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
          }
        </form>
        <kendo-dialog-actions>
          <button kendoButton type="button" fillMode="outline" [disabled]="busy()" (click)="closeCreate()">Hủy</button>
          <button kendoButton type="button" themeColor="primary" [disabled]="busy()" (click)="create()">{{ busy() ? 'Đang tạo…' : 'Tạo khoản thu' }}</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }

    @if (paying(); as target) {
      <app-pay-dialog [fundId]="fundId()" [target]="target" (closed)="paying.set(null)" (paid)="onPaid($event)" />
    }
  `,
  styles: `
    .page {
      max-width: 1200px;
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 16px;
    }
    .text {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .title-row {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .title-row h1 {
      margin: 0;
    }
    .tabs {
      display: flex;
      gap: 4px;
      margin-bottom: 16px;
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
    .list {
      padding: 0;
    }
    .charge {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 16px;
      border-top: 1px solid var(--cf-border);
    }
    .charge:first-child {
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
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .title {
      font-size: 15px;
    }
    .mine {
      margin-left: auto;
    }
    .meta {
      font-size: 13px;
    }
    .progress {
      display: flex;
      align-items: center;
      gap: 12px;
      max-width: 460px;
      margin-top: 4px;
    }
    .bar {
      display: flex;
      flex: 1;
      height: 8px;
      overflow: hidden;
      border-radius: 4px;
      background: var(--cf-subtle);
    }
    .paid {
      background: var(--cf-ink);
    }
    .await {
      background: #f59e0b;
    }
    .count {
      min-width: 110px;
      font-size: 13px;
    }
    .side {
      display: flex;
      align-items: center;
      gap: 12px;
      white-space: nowrap;
    }
    .none {
      margin: 24px 0;
      text-align: center;
    }
    .status-Paid {
      color: var(--cf-success-fg);
      font-weight: 600;
    }
    .status-Awaiting {
      color: var(--cf-warning-fg);
      font-weight: 600;
    }
    .status-Unpaid {
      color: var(--cf-text);
      font-weight: 600;
    }
    .dlg-sub {
      margin: 0 0 12px;
    }
    .form {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 12px;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .req {
      color: var(--cf-expense);
    }
    .text {
      box-sizing: border-box;
      min-height: 40px;
      padding: 0 12px;
      border: 1px solid var(--cf-border-strong);
      border-radius: var(--cf-radius-lg);
      font: inherit;
    }
    .text.invalid {
      border-color: var(--cf-expense);
    }
    .area {
      padding: 8px 12px;
      resize: vertical;
    }
    .money {
      text-align: right;
    }
    .hint {
      font-size: 13px;
    }
    .preview {
      margin: 0;
      padding: 12px;
      border: 1px solid var(--cf-border);
      border-radius: var(--cf-radius-lg);
      background: var(--cf-background);
      font-size: 13px;
    }
  `,
})
export class ChargesPage {
  private readonly api = inject(ChargesApi);
  private readonly members = inject(MembersApi);
  private readonly categoryApi = inject(CategoriesApi);
  private readonly fund = inject(CurrentFund);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notifications = inject(AppNotifications);

  readonly fundId = input.required<string>();

  protected readonly today = todayInVietnam();
  protected readonly canManage = computed(() => this.fund.can(Permissions.ChargeManage));
  protected readonly tab = signal<Tab>('Open');
  protected readonly archived = signal(false);
  private readonly refresh = signal(0);

  private readonly state = toSignal(
    toObservable(computed(() => ({ fundId: this.fundId(), refresh: this.refresh() }))).pipe(
      switchMap(({ fundId }) =>
        toLoadState(
          forkJoin({
            open: this.api.list(fundId, { status: 'Open', page: 1, pageSize: 100 }),
            closed: this.api.list(fundId, { status: 'Closed', page: 1, pageSize: 100 }),
          }),
        ),
      ),
    ),
    { initialValue: LOADING as LoadState<{ open: PagedResult<Charge>; closed: PagedResult<Charge> }> },
  );

  protected readonly lists = computed<Lists>(() => {
    const state = this.state();
    return state.status === 'ready' ? { open: state.data.open.items, closed: state.data.closed.items } : { open: [], closed: [] };
  });
  protected readonly visible = computed(() => (this.tab() === 'Open' ? this.lists().open : this.lists().closed));
  protected readonly kind = computed(() => {
    const state = this.state();
    const empty = state.status === 'ready' && state.data.open.items.length + state.data.closed.items.length === 0 && !this.canManage();
    return state.status === 'ready' ? (empty ? 'empty' : null) : stateKind(state);
  });

  // ---------- Hiển thị ----------

  protected percent(part: number, total: number): number {
    return percentOf(part, total);
  }

  protected statusLabel(charge: Charge): string {
    return CHARGE_STATUS_LABEL[charge.status];
  }

  protected assignmentLabel(status: AssignmentStatus): string {
    return ASSIGNMENT_STATUS_LABEL[status];
  }

  protected mineClass(status: AssignmentStatus): string {
    return status === 'Paid' ? 'status-Paid' : status === 'AwaitingConfirmation' ? 'status-Awaiting' : 'status-Unpaid';
  }

  // ---------- Nộp tiền ----------

  protected readonly paying = signal<PayTarget | null>(null);

  protected pay(charge: Charge): void {
    if (charge.mine) {
      this.paying.set({ assignmentId: charge.mine.assignmentId, title: charge.title, amountDue: charge.mine.amountDue });
    }
  }

  protected onPaid(result: { selfDeclared: boolean }): void {
    this.paying.set(null);
    this.refresh.update((value) => value + 1);
    this.notifications.success(result.selfDeclared ? 'Đã tự ghi nhận khoản đóng của bạn.' : 'Đã gửi. Thủ quỹ sẽ xác nhận khoản đóng của bạn.');
  }

  // ---------- Tạo khoản thu ----------

  protected readonly creating = signal(false);
  protected readonly busy = signal(false);
  protected readonly createError = signal<string | null>(null);
  protected readonly title = signal('');
  protected readonly purpose = signal('');
  protected readonly amount = signal(0);
  protected readonly dueDate = signal('');
  protected readonly categoryId = signal('');
  protected readonly categories = signal<Category[]>([]);
  protected readonly errors = signal<{ title?: string; category?: string; amount?: string; dueDate?: string }>({});
  protected readonly memberCount = signal(0);
  protected readonly total = computed(() => this.amount() * this.memberCount());
  protected readonly amountText = computed(() => (this.amount() > 0 ? this.amount().toLocaleString('vi-VN') : ''));

  constructor() {
    if (this.route.snapshot.data['create']) {
      queueMicrotask(() => this.openCreate());
    }
  }

  protected openCreate(): void {
    this.title.set('');
    this.purpose.set('');
    this.amount.set(0);
    this.dueDate.set('');
    this.categoryId.set('');
    this.errors.set({});
    this.createError.set(null);
    this.creating.set(true);
    void firstValueFrom(this.categoryApi.list(this.fundId(), { type: 'Income', activeOnly: true })).then((items) => this.categories.set(items));
    void firstValueFrom(this.members.list(this.fundId(), { status: 'Active', page: 1, pageSize: 1 })).then((page) => this.memberCount.set(page.total));
  }

  protected closeCreate(): void {
    this.creating.set(false);
    if (this.route.snapshot.data['create']) {
      void this.router.navigate(['/funds', this.fundId(), 'charges']);
    }
  }

  protected setAmount(text: string): void {
    const digits = text.replace(/\D/g, '').slice(0, 12);
    this.amount.set(digits ? Number(digits) : 0);
  }

  protected async create(): Promise<void> {
    const errors: { title?: string; category?: string; amount?: string; dueDate?: string } = {};
    if (!this.title().trim()) {
      errors.title = 'Vui lòng nhập tên khoản thu.';
    }
    if (!this.categoryId()) {
      errors.category = 'Vui lòng chọn danh mục thu.';
    }
    if (this.amount() <= 0) {
      errors.amount = 'Số tiền mỗi người phải lớn hơn 0.';
    }
    if (!this.dueDate()) {
      errors.dueDate = 'Vui lòng chọn hạn đóng.';
    } else if (this.dueDate() < this.today) {
      errors.dueDate = 'Hạn đóng không được trước ngày hôm nay.';
    }
    this.errors.set(errors);
    this.createError.set(null);
    if (Object.keys(errors).length > 0 || this.busy()) {
      return;
    }

    this.busy.set(true);
    try {
      const charge = await firstValueFrom(
        this.api.create(this.fundId(), {
          title: this.title().trim(),
          purpose: this.purpose().trim() || null,
          amountPerPerson: this.amount(),
          dueDate: this.dueDate(),
          categoryId: this.categoryId(),
        }),
      );
      this.creating.set(false);
      this.tab.set('Open');
      this.refresh.update((value) => value + 1);
      this.notifications.success(`Đã tạo khoản thu “${charge.title}”.`);
      if (this.route.snapshot.data['create']) {
        void this.router.navigate(['/funds', this.fundId(), 'charges']);
      }
    } catch (error) {
      this.handleCreateError(error);
    } finally {
      this.busy.set(false);
    }
  }

  private handleCreateError(error: unknown): void {
    if (!(error instanceof ApiError)) {
      this.createError.set('Đã có lỗi xảy ra. Vui lòng thử lại.');
      return;
    }
    if (error.code === 'fund.archived') {
      this.archived.set(true);
    }
    const next: { title?: string; category?: string; amount?: string; dueDate?: string } = {};
    const field = CHARGE_FIELD_FOR[error.code];
    if (field === 'dueDate') {
      next.dueDate = error.message;
    } else if (field === 'amountPerPerson') {
      next.amount = error.message;
    } else if (field === 'categoryId') {
      next.category = error.message;
    }
    for (const [name, messages] of Object.entries(error.fieldErrors)) {
      if (name === 'title') next.title = messages[0];
      if (name === 'amountPerPerson') next.amount = messages[0];
      if (name === 'dueDate') next.dueDate = messages[0];
      if (name === 'categoryId') next.category = messages[0];
    }
    this.errors.set(next);
    this.createError.set(Object.keys(next).length > 0 ? null : error.message);
  }
}
