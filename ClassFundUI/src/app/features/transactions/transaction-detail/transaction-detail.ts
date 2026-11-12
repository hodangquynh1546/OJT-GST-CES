import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ButtonComponent } from '@progress/kendo-angular-buttons';

import { AppState } from '../../../shared/components/app-state/app-state';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LoadState, stateKind } from '../../../shared/state/load-state';
import { vnDateTime } from '../../treasurers/vn-date-time';
import { dmy } from '../ledger-dates';
import { Category } from '../../categories/categories.models';
import { Transaction, TransactionHistoryItem } from '../transactions.models';

interface HistoryLine {
  what: string;
  when: string;
  who: string;
  detail: string;
}

const MONEY = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });

/**
 * Panel chi tiết giao dịch 340 px của S-02 (Canvas `Ledger`; FR-07). GVCN / thủ quỹ: ghi chú, công khai, người ghi / sửa / hủy,
 * lý do hủy, lịch sử thay đổi. Học sinh / phụ huynh: chỉ nội dung, ngày, danh mục, số tiền; khoản đã hủy chỉ nhãn + ngày hủy.
 */
@Component({
  selector: 'app-transaction-detail',
  imports: [ButtonComponent, AppState, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (kind()) {
      @case ('loading') {
        <app-state kind="loading" />
      }
      @case (null) {
        @if (tx(); as tx) {
          <div class="head">
            <div class="head-text">
              <span class="cf-muted small">{{ tx.type === 'Income' ? 'Khoản thu' : 'Khoản chi' }} · {{ date(tx.occurredOn) }}</span>
              <h2>{{ tx.description }}</h2>
            </div>
            <button kendoButton type="button" class="close" aria-label="Đóng chi tiết" (click)="closed.emit()">✕</button>
          </div>
          <b
            class="cf-money amount"
            [class.cf-income]="!tx.isCancelled && tx.type === 'Income'"
            [class.cf-expense]="!tx.isCancelled && tx.type === 'Expense'"
            [class.cf-muted]="tx.isCancelled"
            >{{ signed(tx) | vndMoney: true }}</b
          >
          @if (tx.isCancelled) {
            <div class="cf-alert cf-alert--danger" role="status">
              @if (full()) {
                Đã hủy lúc {{ time(tx.cancelledAt) }} bởi {{ tx.cancelledByName }}. Lý do: {{ tx.cancelReason }}
              } @else {
                Khoản này đã bị hủy ngày {{ date(tx.cancelledOn) }} và không còn tính vào số dư.
              }
            </div>
          }
          @if (full() && tx.isPublic === false) {
            <div class="cf-alert cf-alert--warning">
              Không công khai: phụ huynh không thấy nội dung, ngày, danh mục, ghi chú. Số tiền vẫn nằm trong số dư và dòng
              “Khoản khác” nên có thể suy ra – không phải bảo mật tuyệt đối.
            </div>
          }
          <dl>
            <dt>Danh mục</dt>
            <dd>{{ tx.categoryName }}</dd>
            <dt>Ngày</dt>
            <dd>{{ date(tx.occurredOn) }}</dd>
            @if (full()) {
              <dt>Ghi chú</dt>
              <dd>{{ tx.note || '—' }}</dd>
              <dt>Công khai</dt>
              <dd>{{ tx.isPublic ? 'Có' : 'Không' }}</dd>
              <dt>Người ghi</dt>
              <dd>{{ tx.createdByName }} · {{ time(tx.createdAt) }}</dd>
              <dt>Sửa gần nhất</dt>
              <dd>{{ tx.updatedByName ? tx.updatedByName + ' · ' + time(tx.updatedAt) : '—' }}</dd>
            }
          </dl>
          @if (canManage() && !tx.isCancelled) {
            <div class="actions">
              <button kendoButton type="button" (click)="edit.emit(tx)">Sửa</button>
              <button kendoButton type="button" class="danger" (click)="cancel.emit(tx)">Hủy giao dịch</button>
            </div>
          }
          @if (full()) {
            <section class="history" aria-labelledby="tx-history">
              <b id="tx-history">Lịch sử thay đổi</b>
              @switch (history().status) {
                @case ('loading') {
                  <span class="cf-muted small">Đang tải lịch sử…</span>
                }
                @case ('error') {
                  <span class="cf-muted small">Không tải được lịch sử thay đổi.</span>
                }
                @default {
                  @for (line of lines(); track $index) {
                    <div class="line">
                      <span class="bullet" aria-hidden="true"></span>
                      <span>
                        <b>{{ line.what }}</b> · <span class="cf-muted">{{ line.when }} · {{ line.who }}</span><br />
                        <span class="cf-muted small">{{ line.detail }}</span>
                      </span>
                    </div>
                  }
                }
              }
            </section>
          } @else {
            <span class="cf-muted note">
              Phụ huynh và học sinh không xem được ghi chú nội bộ, người ghi, lý do hủy và lịch sử thay đổi.
            </span>
          }
        }
      }
      @case ('not-found') {
        <div class="cf-muted empty">Không tìm thấy giao dịch (có thể khoản không công khai hoặc không thuộc quỹ).</div>
      }
      @default {
        <app-state [kind]="kind()!" />
      }
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .head {
      display: flex;
      align-items: flex-start;
      gap: 8px;
    }
    .head-text {
      flex: 1;
      min-width: 0;
    }
    h2 {
      margin: 0;
      font-size: 18px;
      line-height: 26px;
      overflow-wrap: anywhere;
    }
    .close {
      min-height: 36px;
      padding: 0 10px;
    }
    .amount {
      font-size: 26px;
      line-height: 32px;
      text-align: left;
    }
    dl {
      display: grid;
      grid-template-columns: 120px minmax(0, 1fr);
      gap: 8px 12px;
      margin: 0;
    }
    dt {
      color: var(--cf-text-muted);
    }
    dd {
      margin: 0;
      overflow-wrap: anywhere;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .danger {
      border-color: var(--cf-expense);
      color: var(--cf-danger-fg);
    }
    .history {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding-top: 12px;
      border-top: 1px solid var(--cf-subtle);
    }
    .line {
      display: flex;
      gap: 10px;
    }
    .bullet {
      flex: none;
      width: 8px;
      height: 8px;
      margin-top: 6px;
      border-radius: 50%;
      background: var(--cf-primary);
    }
    .small {
      font-size: 13px;
    }
    .note {
      font-size: 12px;
    }
    .empty {
      padding: 24px 8px;
      text-align: center;
    }
  `,
})
export class TransactionDetail {
  readonly state = input.required<LoadState<Transaction>>();
  readonly history = input<LoadState<TransactionHistoryItem[]>>({ status: 'ready', data: [] });
  readonly categories = input<readonly Category[]>([]);
  /** Bản đầy đủ (`Ledger.ViewFull`). */
  readonly full = input(false);
  /** Thủ quỹ (`Transaction.Manage`): nút Sửa / Hủy. */
  readonly canManage = input(false);

  readonly edit = output<Transaction>();
  readonly cancel = output<Transaction>();
  readonly closed = output<void>();

  protected readonly kind = computed(() => stateKind(this.state(), () => false));
  protected readonly tx = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data : null;
  });
  /** Mới nhất trước như Canvas; mỗi dòng: hành động · thời điểm · người, rồi giá trị trước / sau. */
  protected readonly lines = computed<HistoryLine[]>(() => {
    const state = this.history();
    if (state.status !== 'ready') {
      return [];
    }
    const names = new Map(this.categories().map((category) => [category.id, category.name]));
    return [...state.data].reverse().map((item) => ({
      what: item.action.endsWith('Create') ? 'Tạo' : item.action.endsWith('Cancel') ? 'Hủy' : 'Sửa',
      when: vnDateTime(item.occurredAt),
      who: item.actorName ?? '—',
      detail: describe(item, names),
    }));
  });

  protected date(value: string | null | undefined): string {
    return dmy(value);
  }

  protected time(value: string | null | undefined): string {
    return vnDateTime(value);
  }

  protected signed(tx: Transaction): number {
    return tx.type === 'Income' ? tx.amount : -tx.amount;
  }
}

const LABELS: Record<string, string> = {
  amount: 'Số tiền',
  occurredOn: 'Ngày',
  categoryId: 'Danh mục',
  description: 'Nội dung',
  note: 'Ghi chú',
  isPublic: 'Công khai',
};

function show(key: string, value: unknown, names: Map<string, string>): string {
  if (value === null || value === undefined || value === '') {
    return '—';
  }
  switch (key) {
    case 'amount':
      return `${MONEY.format(Number(value))} đ`;
    case 'occurredOn':
      return dmy(String(value));
    case 'categoryId':
      return names.get(String(value)) ?? '—';
    case 'isPublic':
      return value ? 'Có' : 'Không';
    default:
      return String(value);
  }
}

function describe(item: TransactionHistoryItem, names: Map<string, string>): string {
  const next = item.newValues ?? {};
  if (item.action.endsWith('Cancel')) {
    return `Lý do: ${String(next['reason'] ?? '')}`;
  }
  if (item.action.endsWith('Create')) {
    return `Số tiền ${show('amount', next['amount'], names)} · ${show('categoryId', next['categoryId'], names)}`;
  }
  const old = item.oldValues ?? {};
  return Object.keys(LABELS)
    .filter((key) => JSON.stringify(old[key] ?? null) !== JSON.stringify(next[key] ?? null))
    .map((key) => `${LABELS[key]}: ${show(key, old[key], names)} → ${show(key, next[key], names)}`)
    .join('; ');
}
