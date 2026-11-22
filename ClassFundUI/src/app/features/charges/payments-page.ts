import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { Observable, catchError, firstValueFrom, of, switchMap } from 'rxjs';

import { AppNotifications } from '../../core/services/notification.service';
import { AppState } from '../../shared/components/app-state/app-state';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { ScopeBadge } from '../../shared/components/scope-badge/scope-badge';
import { ApiError } from '../../shared/models/api-error';
import { PAYMENT_METHOD_LABEL } from '../../shared/models/labels';
import { PagedResult } from '../../shared/models/paged-result';
import { VndMoneyPipe } from '../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../shared/state/load-state';
import { AttachmentsApi, saveBlob } from '../attachments/attachments.api';
import { isImage } from '../attachments/attachments.models';
import { ChargesApi } from './charges.api';
import './charges.errors';
import { PaymentQueueItem } from './charges.models';
import { formatDateTime } from './charges.utils';

const REASON_MAX = 500;

/** H-04 Xác nhận thanh toán (FR-21): hàng chờ bên trái, chi tiết + minh chứng + xác nhận / từ chối bên phải. */
@Component({
  selector: 'app-payments-page',
  imports: [FormsModule, ButtonComponent, PageHeader, ScopeBadge, AppState, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <app-page-header title="Xác nhận thanh toán" subtitle="Đối chiếu số tiền và minh chứng. Thanh toán đã xác nhận tự ghi vào sổ thu của đúng phạm vi." />

      @if (kind(); as value) {
        <app-state [kind]="value" [title]="value === 'empty' ? 'Không có thanh toán nào đang chờ' : undefined" [message]="value === 'empty' ? 'Khi học sinh nộp tiền, khoản đó sẽ hiện ở đây để bạn xác nhận.' : undefined" />
      } @else {
        <div class="layout">
          <section class="cf-card queue" aria-labelledby="queue-title">
            <div class="queue-head">
              <h2 id="queue-title">Hàng chờ</h2>
              <span class="cf-tag cf-tag--warning">{{ total() }} chờ</span>
            </div>
            <ul class="items">
              @for (item of items(); track item.id) {
                <li>
                  <button type="button" class="item" [class.selected]="item.id === selectedId()" [attr.aria-current]="item.id === selectedId()" (click)="select(item.id)">
                    <span class="row"><b>{{ item.payerName }}</b><b class="amount">{{ item.amount | vndMoney }}</b></span>
                    <span class="row meta">
                      <app-scope-badge [scope]="item.eventId ? 'Event' : 'General'" [eventName]="item.eventName" />
                      <span class="cf-muted">{{ item.chargeTitle }} · {{ methodLabel(item) }}</span>
                    </span>
                  </button>
                </li>
              }
            </ul>
          </section>

          @if (selected(); as item) {
            <section class="cf-card detail" aria-label="Chi tiết thanh toán">
              <header class="d-head">
                <div>
                  <h2>{{ item.payerName }} · {{ item.chargeTitle }}</h2>
                  <span class="cf-muted">Gửi lúc {{ time(item) }}</span>
                </div>
                <span class="cf-tag cf-tag--warning">Chờ xác nhận</span>
              </header>

              <dl class="facts">
                <dt>Phạm vi</dt><dd><app-scope-badge [scope]="item.eventId ? 'Event' : 'General'" [eventName]="item.eventName" /></dd>
                <dt>Học sinh</dt><dd>{{ item.payerName }} · <span class="cf-muted">{{ item.payerEmail }}</span></dd>
                <dt>Số tiền nộp</dt><dd><b>{{ item.amount | vndMoney }}</b></dd>
                @if (item.balanceApplied > 0) {
                  <dt>Đã trừ từ số dư</dt><dd>{{ item.balanceApplied | vndMoney }}</dd>
                  <dt>Cần nhận thêm</dt><dd><b>{{ item.amount - item.balanceApplied | vndMoney }}</b></dd>
                }
                <dt>Hình thức</dt><dd>{{ methodLabel(item) }}</dd>
                @if (item.transferCode) {
                  <dt>Mã giao dịch</dt><dd>{{ item.transferCode }}</dd>
                }
              </dl>

              <h3>Minh chứng</h3>
              @if (item.attachments.length === 0) {
                <p class="cf-muted proof-none">{{ item.method === 'Cash' ? 'Tiền mặt – không có ảnh minh chứng.' : 'Không có ảnh minh chứng.' }}</p>
              } @else {
                @for (file of item.attachments; track file.id) {
                  @if (isImage(file.contentType)) {
                    @if (preview(); as url) {
                      <img class="proof" [src]="url" [alt]="'Minh chứng ' + file.fileName" />
                    } @else {
                      <p class="cf-muted proof-none">Đang tải ảnh…</p>
                    }
                  } @else {
                    <button kendoButton fillMode="outline" type="button" (click)="download(file.id, file.fileName)">Tải {{ file.fileName }}</button>
                  }
                }
              }

              <div class="reject">
                <label for="reason">Lý do từ chối <span class="cf-muted">(bắt buộc nếu từ chối)</span></label>
                <textarea id="reason" rows="3" class="text" [maxLength]="reasonMax" [class.invalid]="reasonError()" [ngModel]="reason()" (ngModelChange)="reason.set($event)"></textarea>
                @if (reasonError(); as message) { <span class="k-form-error" role="alert">{{ message }}</span> }
              </div>

              <footer class="d-actions">
                <button kendoButton fillMode="outline" themeColor="error" type="button" [disabled]="busy()" (click)="reject(item)">Từ chối</button>
                <button kendoButton themeColor="primary" type="button" [disabled]="busy()" (click)="confirm(item)">{{ busy() ? 'Đang xử lý…' : 'Xác nhận đã nhận tiền' }}</button>
              </footer>
            </section>
          }
        </div>
      }
    </div>
  `,
  styles: `
    .page {
      max-width: 1200px;
    }
    .layout {
      display: grid;
      grid-template-columns: 420px 1fr;
      gap: 16px;
      align-items: start;
    }
    .queue {
      padding: 0;
    }
    .queue-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 16px;
    }
    h2 {
      margin: 0;
      font-size: 16px;
      line-height: 24px;
    }
    h3 {
      margin: 16px 0 8px;
      font-size: 14px;
    }
    .items {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .item {
      display: flex;
      flex-direction: column;
      gap: 6px;
      width: 100%;
      padding: 12px 16px;
      border: 0;
      border-top: 1px solid var(--cf-border);
      border-left: 3px solid transparent;
      background: transparent;
      font: inherit;
      text-align: left;
      cursor: pointer;
    }
    .item.selected {
      border-left-color: var(--cf-ink);
      background: var(--cf-subtle);
    }
    .row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .meta {
      justify-content: flex-start;
      font-size: 13px;
    }
    .amount {
      font-variant-numeric: tabular-nums;
    }
    .d-head {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 12px;
    }
    .facts {
      display: grid;
      grid-template-columns: 140px 1fr;
      gap: 10px 12px;
      margin: 0;
    }
    .facts dt {
      color: var(--cf-text-muted);
    }
    .facts dd {
      margin: 0;
    }
    .proof {
      display: block;
      max-width: 100%;
      max-height: 360px;
      border: 1px solid var(--cf-border);
      border-radius: var(--cf-radius-lg);
      object-fit: contain;
    }
    .proof-none {
      margin: 0;
    }
    .reject {
      display: flex;
      flex-direction: column;
      gap: 6px;
      margin-top: 16px;
    }
    .text {
      box-sizing: border-box;
      padding: 8px 12px;
      border: 1px solid var(--cf-border-strong);
      border-radius: var(--cf-radius-lg);
      font: inherit;
      resize: vertical;
    }
    .text.invalid {
      border-color: var(--cf-expense);
    }
    .d-actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
      margin-top: 16px;
    }
  `,
})
export class PaymentsPage {
  private readonly api = inject(ChargesApi);
  private readonly files = inject(AttachmentsApi);
  private readonly notifications = inject(AppNotifications);

  readonly fundId = input.required<string>();

  protected readonly reasonMax = REASON_MAX;
  protected readonly isImage = isImage;
  protected readonly refresh = signal(0);
  protected readonly selectedId = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly reason = signal('');
  protected readonly reasonError = signal<string | null>(null);

  private readonly state = toSignal(
    toObservable(computed(() => ({ fundId: this.fundId(), refresh: this.refresh() }))).pipe(
      switchMap(({ fundId }) => toLoadState(this.api.payments(fundId, { status: 'Submitted', page: 1, pageSize: 100 }))),
    ),
    { initialValue: LOADING as LoadState<PagedResult<PaymentQueueItem>> },
  );

  private lastItems: PaymentQueueItem[] | null = null;
  protected readonly items = computed<PaymentQueueItem[]>(() => {
    const state = this.state();
    if (state.status === 'ready') {
      this.lastItems = state.data.items;
    }
    return this.lastItems ?? [];
  });
  protected readonly total = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data.total : this.items().length;
  });
  protected readonly kind = computed(() => {
    const state = this.state();
    if (state.status === 'error' || (state.status === 'loading' && this.lastItems === null)) {
      return stateKind(state);
    }
    return this.items().length === 0 ? 'empty' : null;
  });
  protected readonly selected = computed(() => {
    const items = this.items();
    return items.find((item) => item.id === this.selectedId()) ?? items[0] ?? null;
  });

  // ---------- Ảnh minh chứng (tải qua API có kiểm quyền, hiện bằng blob URL) ----------

  private readonly proofFile = computed(() => this.selected()?.attachments.find((file) => isImage(file.contentType)) ?? null);
  private objectUrl: string | null = null;
  protected readonly preview = toSignal(
    toObservable(computed(() => ({ fundId: this.fundId(), file: this.proofFile() }))).pipe(
      switchMap(({ fundId, file }) =>
        file
          ? this.files.download(fundId, file.id).pipe(
              switchMap((blob) => {
                if (this.objectUrl) {
                  URL.revokeObjectURL(this.objectUrl);
                }
                this.objectUrl = URL.createObjectURL(blob);
                return of(this.objectUrl);
              }),
              catchError(() => of<string | null>(null)),
            )
          : of<string | null>(null),
      ),
    ),
    { initialValue: null },
  );

  constructor() {
    // Đổi dòng đang xem thì xóa lý do đã gõ dở của dòng trước.
    effect(() => {
      this.selected();
      this.reason.set('');
      this.reasonError.set(null);
    });
  }

  protected select(id: string): void {
    this.selectedId.set(id);
  }

  protected methodLabel(item: PaymentQueueItem): string {
    return PAYMENT_METHOD_LABEL[item.method];
  }

  protected time(item: PaymentQueueItem): string {
    return formatDateTime(item.submittedAt);
  }

  protected async download(attachmentId: string, fileName: string): Promise<void> {
    try {
      saveBlob(await firstValueFrom(this.files.download(this.fundId(), attachmentId)), fileName);
    } catch {
      this.notifications.error('Không tải được file. Vui lòng thử lại.');
    }
  }

  // ---------- Xử lý ----------

  protected confirm(item: PaymentQueueItem): Promise<void> {
    return this.handle(() => this.api.confirm(this.fundId(), item.id), `Đã xác nhận khoản đóng của ${item.payerName}.`);
  }

  protected reject(item: PaymentQueueItem): Promise<void> {
    const reason = this.reason().trim();
    if (!reason) {
      this.reasonError.set('Vui lòng nhập lý do từ chối.');
      return Promise.resolve();
    }
    return this.handle(() => this.api.reject(this.fundId(), item.id, reason), `Đã từ chối khoản đóng của ${item.payerName}.`);
  }

  private async handle(action: () => Observable<unknown>, success: string): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      await firstValueFrom(action());
      this.selectedId.set(null);
      this.notifications.success(success);
    } catch (error) {
      this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      this.refresh.update((value) => value + 1);
      this.busy.set(false);
    }
  }
}
