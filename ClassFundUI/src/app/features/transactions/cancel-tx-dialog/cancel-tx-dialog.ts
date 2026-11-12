import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent, DialogTitleBarComponent } from '@progress/kendo-angular-dialog';
import { LoaderComponent } from '@progress/kendo-angular-indicators';
import { TextAreaComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { finalize } from 'rxjs';

import { ApiError } from '../../../shared/models/api-error';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { dmy } from '../ledger-dates';
import { TransactionsApi } from '../transactions.api';
import { CANCEL_REASON_MAX, Transaction } from '../transactions.models';

/**
 * T-01 "Hủy giao dịch?" 480 px – Canvas `TxDialog` chế độ hủy (FR-05; BR-06): lý do bắt buộc ≤ 500 ký tự,
 * khoản đã hủy vẫn nằm trong lịch sử, không tính vào số dư. Không có chức năng xóa.
 */
@Component({
  selector: 'app-cancel-tx-dialog',
  imports: [FormsModule, ButtonComponent, DialogActionsComponent, DialogComponent, DialogTitleBarComponent, LabelComponent, LoaderComponent, TextAreaComponent, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <kendo-dialog [width]="480" [maxWidth]="'calc(100vw - 32px)'" (close)="close()">
      <kendo-dialog-titlebar>
        <span class="title"><span class="dot" [class.dot--income]="transaction().type === 'Income'" aria-hidden="true"></span>Hủy giao dịch?</span>
      </kendo-dialog-titlebar>
      <div class="body">
        <div class="summary">
          <b>{{ transaction().description }}</b>
          <span class="cf-muted">{{ meta() }}</span>
          <b [class.cf-income]="transaction().type === 'Income'" [class.cf-expense]="transaction().type === 'Expense'">
            {{ signedAmount() | vndMoney: true }}
          </b>
        </div>
        <div class="cf-alert cf-alert--warning" role="note">
          Khoản đã hủy vẫn nằm trong lịch sử, không tính vào số dư và báo cáo, <b>không thể hoàn tác hay sửa</b>. Nếu khoản đang
          công khai, phụ huynh chỉ thấy nhãn “Đã hủy” và ngày hủy.
        </div>
        @if (serverMessage(); as message) {
          <div class="cf-alert cf-alert--danger" role="alert">{{ message }}</div>
        }
        <div class="field">
          <kendo-label [for]="reasonBox" text="Lý do hủy *" />
          <kendo-textarea
            #reasonBox
            [(ngModel)]="reason"
            [rows]="3"
            [maxlength]="maxLength"
            placeholder="Ví dụ: Ghi trùng với khoản ngày 07/11"
            [readonly]="saving()"
            [class.invalid]="invalid()"
            [attr.aria-invalid]="invalid()"
            aria-describedby="cancel-reason-help"
          />
          <span id="cancel-reason-help" class="help" [class.help--error]="invalid()">
            {{
              invalid()
                ? 'Nhập lý do hủy (bắt buộc, tối đa 500 ký tự).'
                : 'Bắt buộc, tối đa 500 ký tự. Thủ quỹ và GVCN xem được; phụ huynh không thấy lý do.'
            }}
          </span>
        </div>
      </div>
      <kendo-dialog-actions layout="end">
        <button kendoButton type="button" [disabled]="saving()" (click)="close()">Không hủy</button>
        <button kendoButton themeColor="error" type="button" [disabled]="saving()" [attr.aria-busy]="saving()" (click)="confirm()">
          @if (saving()) {
            <kendo-loader size="small" type="infinite-spinner" themeColor="light" />
            Đang hủy…
          } @else {
            Hủy giao dịch
          }
        </button>
      </kendo-dialog-actions>
    </kendo-dialog>
  `,
  styles: `
    .title {
      display: flex;
      align-items: center;
      gap: 10px;
      font-size: 18px;
      font-weight: 700;
      line-height: 26px;
    }
    .dot {
      flex: none;
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: var(--cf-expense);
    }
    .dot--income {
      background: var(--cf-income);
    }
    .body {
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .summary {
      display: flex;
      flex-direction: column;
      gap: 2px;
      padding: 10px 12px;
      border-radius: var(--cf-radius-md);
      background: var(--cf-background);
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .invalid {
      border-color: var(--cf-expense);
    }
    .help {
      color: var(--cf-text-muted);
      font-size: 12px;
    }
    .help--error {
      color: var(--cf-danger-fg);
    }
    kendo-loader {
      margin-right: 6px;
    }
  `,
})
export class CancelTxDialog {
  private readonly api = inject(TransactionsApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly fundId = input.required<string>();
  readonly transaction = input.required<Transaction>();

  readonly cancelled = output<Transaction>();
  readonly closed = output<void>();

  protected readonly maxLength = CANCEL_REASON_MAX;
  protected reason = '';
  protected readonly tried = signal(false);
  protected readonly saving = signal(false);
  protected readonly serverMessage = signal<string | null>(null);
  private readonly reasonValid = signal(false);

  protected readonly invalid = computed(() => this.tried() && !this.reasonValid());
  protected readonly meta = computed(() => `${dmy(this.transaction().occurredOn)} · ${this.transaction().categoryName ?? ''}`);
  protected readonly signedAmount = computed(() =>
    this.transaction().type === 'Income' ? this.transaction().amount : -this.transaction().amount,
  );

  protected confirm(): void {
    if (this.saving()) {
      return;
    }
    const reason = this.reason.trim();
    this.tried.set(true);
    this.reasonValid.set(reason.length > 0 && reason.length <= CANCEL_REASON_MAX);
    this.serverMessage.set(null);
    if (!this.reasonValid()) {
      return;
    }

    this.saving.set(true);
    this.api
      .cancel(this.fundId(), this.transaction().id, reason)
      .pipe(
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (saved) => this.cancelled.emit(saved),
        error: (error: unknown) => {
          const message = error instanceof ApiError ? (error.fieldErrors['reason']?.[0] ?? error.message) : 'Đã có lỗi xảy ra. Vui lòng thử lại.';
          this.serverMessage.set(message);
        },
      });
  }

  protected close(): void {
    if (!this.saving()) {
      this.closed.emit();
    }
  }
}
