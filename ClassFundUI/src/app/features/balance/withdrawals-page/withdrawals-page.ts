import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { CellTemplateDirective, ColumnComponent, GridComponent, NoRecordsTemplateDirective } from '@progress/kendo-angular-grid';
import { ErrorComponent, FormFieldComponent, TextAreaComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { BehaviorSubject, combineLatest, firstValueFrom, map, switchMap } from 'rxjs';

import { AppNotifications } from '../../../core/services/notification.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { ApiError } from '../../../shared/models/api-error';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { BalanceApi } from '../balance.api';
import '../balance.errors';
import { REJECT_REASON_MAX, WITHDRAWAL_STATUS_BADGE, WithdrawalList, WithdrawalRequest, WithdrawalStatus, shortTime } from '../balance.models';

/**
 * B-02 · Yêu cầu rút tiền (FR-41) theo canvas Withdrawals: tab Đang chờ / Đã trả / Từ chối, "Đã trả tiền" (trừ số dư, ghi lịch sử),
 * "Từ chối" (lý do bắt buộc, số dư mở lại). Thủ quỹ xử lý yêu cầu của chính mình được ghi "Tự ghi nhận" trong nhật ký.
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
    ErrorComponent,
    FormFieldComponent,
    GridComponent,
    LabelComponent,
    NoRecordsTemplateDirective,
    ReactiveFormsModule,
    TextAreaComponent,
    VndMoneyPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="head-t">
        <h1>Yêu cầu rút tiền</h1>
        <p class="cf-muted">Thành viên rút toàn bộ số dư cá nhân. Liên hệ trả tiền rồi bấm “Đã trả tiền” – hệ thống trừ số dư và ghi lịch sử (FR-41).</p>
      </div>

      <section class="cf-card list" aria-label="Danh sách yêu cầu rút">
        <div class="card-h">
          <div class="tabs" role="tablist" aria-label="Trạng thái">
            @for (option of tabs; track option.value) {
              <button
                type="button"
                role="tab"
                class="tab"
                [class.on]="status() === option.value"
                [attr.aria-selected]="status() === option.value"
                (click)="status.set(option.value)"
              >
                {{ option.label }}
              </button>
            }
          </div>
        </div>
        @if (kind(); as stateKindValue) {
          <app-state [kind]="stateKindValue" />
        } @else {
          <kendo-grid [data]="visible()" [loading]="state().status === 'loading'">
            <kendo-grid-column title="Thành viên">
              <ng-template kendoGridCellTemplate let-row>
                <div class="strong">{{ row.fullName }}@if (row.isMine) { <span class="cf-muted">(bạn)</span>}</div>
                <div class="cf-muted xs">{{ row.email }}</div>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Gửi lúc" [width]="130">
              <ng-template kendoGridCellTemplate let-row><span class="cf-muted">{{ time(row.requestedAt) }}</span></ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Số tiền" [width]="140" headerClass="num-head">
              <ng-template kendoGridCellTemplate let-row><span class="cf-money block strong-600">{{ row.amount | vndMoney }}</span></ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Trạng thái" [width]="120">
              <ng-template kendoGridCellTemplate let-row>
                @let badge = badgeOf(row.status);
                <span class="cf-tag" [class]="'cf-tag--' + badge.tone">{{ badge.text }}</span>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Ghi chú">
              <ng-template kendoGridCellTemplate let-row><span class="cf-muted small">{{ note(row) }}</span></ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Thao tác" [width]="210" headerClass="num-head">
              <ng-template kendoGridCellTemplate let-row>
                @if (row.status === 'Requested') {
                  <div class="row-actions">
                    <button kendoButton fillMode="outline" themeColor="error" size="small" type="button" [disabled]="busy()" (click)="askReject(row)">
                      Từ chối
                    </button>
                    <button kendoButton themeColor="primary" size="small" type="button" [disabled]="busy()" (click)="pay(row)">Đã trả tiền</button>
                  </div>
                }
              </ng-template>
            </kendo-grid-column>
            <ng-template kendoGridNoRecordsTemplate>
              <app-state kind="empty" title="Không có yêu cầu nào." message="" />
            </ng-template>
          </kendo-grid>
        }
      </section>
    </div>

    @if (rejecting(); as row) {
      <kendo-dialog title="Từ chối yêu cầu rút" [width]="460" [maxWidth]="'calc(100vw - 32px)'" (close)="rejecting.set(null)">
        <div class="stack">
          <p class="cf-muted">Số dư của {{ row.fullName }} được mở lại. Lý do được gửi trong thông báo.</p>
          <kendo-formfield class="field cf-required">
            <kendo-label [for]="reasonInput" text="Lý do" />
            <kendo-textarea #reasonInput [formControl]="reason" [rows]="2" resizable="vertical" [maxlength]="reasonMax" />
            <kendo-formerror>Vui lòng nhập lý do từ chối.</kendo-formerror>
          </kendo-formfield>
        </div>
        <kendo-dialog-actions layout="end">
          <button kendoButton type="button" (click)="rejecting.set(null)">Hủy</button>
          <button kendoButton themeColor="error" type="button" [disabled]="busy()" (click)="reject(row)">Từ chối</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }
  `,
  // Theo canvas B-02 (Withdrawals.dc.html + cf.css); màu dùng token --cf-*.
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
      margin: 0;
    }
    h1 {
      font-weight: 600;
      letter-spacing: -0.02em;
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
    .list {
      padding: 0;
      overflow: hidden;
    }
    .card-h {
      padding: 16px;
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
    .row-actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
    }
    :host ::ng-deep .num-head .k-cell-inner {
      justify-content: flex-end;
    }
  `,
})
export class WithdrawalsPage {
  private readonly api = inject(BalanceApi);
  private readonly route = inject(ActivatedRoute);
  private readonly notifications = inject(AppNotifications);
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  protected readonly tabs: readonly { value: WithdrawalStatus; label: string }[] = [
    { value: 'Requested', label: 'Đang chờ' },
    { value: 'Paid', label: 'Đã trả' },
    { value: 'Rejected', label: 'Từ chối' },
  ];
  protected readonly time = shortTime;
  protected readonly reasonMax = REJECT_REASON_MAX;
  protected readonly status = signal<WithdrawalStatus>('Requested');
  protected readonly busy = signal(false);
  protected readonly rejecting = signal<WithdrawalRequest | null>(null);
  protected readonly reason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(REJECT_REASON_MAX)] });

  private readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), { initialValue: '' });

  protected readonly state = toSignal(
    combineLatest([this.route.paramMap, this.reload$]).pipe(switchMap(([params]) => toLoadState(this.api.list(params.get('fundId') ?? '')))),
    { initialValue: LOADING as LoadState<WithdrawalList> },
  );
  protected readonly kind = computed(() => {
    const current = this.state();
    return current.status === 'error' ? stateKind(current) : null;
  });
  private readonly data = computed(() => {
    const current = this.state();
    return current.status === 'ready' ? current.data : null;
  });
  protected readonly visible = computed(() => (this.data()?.items ?? []).filter((item) => item.status === this.status()));

  protected count(status: WithdrawalStatus): number {
    const data = this.data();
    return !data ? 0 : status === 'Requested' ? data.requestedCount : status === 'Paid' ? data.paidCount : data.rejectedCount;
  }

  protected badgeOf(status: WithdrawalStatus): { text: string; tone: string } {
    return WITHDRAWAL_STATUS_BADGE[status];
  }

  /** Cột Ghi chú như canvas: "Toàn bộ số dư", "Trả tiền 10/11 – Trần Quang Huy", "Từ chối: …". */
  protected note(row: WithdrawalRequest): string {
    switch (row.status) {
      case 'Paid':
        return `Đã trả ${shortTime(row.handledAt)}${row.handledByName ? ` – ${row.handledByName}` : ''}`;
      case 'Rejected':
        return `Từ chối: ${row.rejectionReason ?? ''}`;
      default:
        return 'Toàn bộ số dư';
    }
  }

  protected async pay(row: WithdrawalRequest): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.markPaid(this.fundId(), row.id));
      this.notifications.success(`Đã xác nhận trả ${new Intl.NumberFormat('vi-VN').format(row.amount)} đ cho ${row.fullName}.`);
    });
  }

  protected askReject(row: WithdrawalRequest): void {
    this.reason.reset('');
    this.rejecting.set(row);
  }

  protected async reject(row: WithdrawalRequest): Promise<void> {
    const reason = this.reason.value.trim();
    if (!reason) {
      this.reason.setErrors({ required: true });
      this.reason.markAsTouched();
      return;
    }
    await this.run(async () => {
      await firstValueFrom(this.api.reject(this.fundId(), row.id, reason));
      this.rejecting.set(null);
      this.notifications.success('Đã từ chối và gửi lý do cho thành viên.');
    });
  }

  private async run(work: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await work();
    } catch (error) {
      this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      this.busy.set(false);
      this.reload$.next();
    }
  }
}
