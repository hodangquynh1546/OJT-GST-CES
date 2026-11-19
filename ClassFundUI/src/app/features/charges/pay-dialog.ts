import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { firstValueFrom } from 'rxjs';

import { ApiError } from '../../shared/models/api-error';
import { VndMoneyPipe } from '../../shared/pipes/vnd-money.pipe';
import { FileUpload } from '../attachments/file-upload/file-upload';
import { Attachment } from '../attachments/attachments.models';
import { ChargesApi } from './charges.api';
import './charges.errors';
import { SubmitPaymentRequest } from './charges.models';

/** Khoản đang được nộp (đủ để hiện tiêu đề và số tiền). */
export interface PayTarget {
  assignmentId: string;
  title: string;
  amountDue: number;
}

/**
 * Hộp thoại nộp tiền (FR-20): chọn tiền mặt / chuyển khoản; chuyển khoản bắt buộc có ảnh minh chứng (BR-11).
 * Thủ quỹ nộp cho chính mình thì được tự ghi nhận ngay (BR-12) – hộp thoại báo kết quả qua `paid`.
 */
@Component({
  selector: 'app-pay-dialog',
  imports: [FormsModule, ButtonComponent, DialogComponent, DialogActionsComponent, FileUpload, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <kendo-dialog [title]="'Nộp tiền: ' + target().title" [width]="520" (close)="closed.emit()">
      <div class="body">
        <p class="amount">
          Số tiền phải đóng <b>{{ target().amountDue | vndMoney }}</b>
        </p>

        <fieldset class="methods">
          <legend>Hình thức đóng</legend>
          <label class="method">
            <input type="radio" name="method" value="Cash" [ngModel]="method()" (ngModelChange)="method.set('Cash')" />
            <span><b>Tiền mặt</b><span class="cf-muted">Đưa tiền trực tiếp cho thủ quỹ</span></span>
          </label>
          <label class="method">
            <input type="radio" name="method" value="BankTransfer" [ngModel]="method()" (ngModelChange)="method.set('BankTransfer')" />
            <span><b>Chuyển khoản</b><span class="cf-muted">Cần ảnh minh chứng chuyển khoản</span></span>
          </label>
        </fieldset>

        @if (method() === 'BankTransfer') {
          <div class="field">
            <label for="transfer-code">Mã giao dịch (nếu có)</label>
            <input id="transfer-code" class="text" maxlength="100" [ngModel]="transferCode()" (ngModelChange)="transferCode.set($event)" />
          </div>
          <app-file-upload
            [fundId]="fundId()"
            kind="PaymentProof"
            label="Tải ảnh minh chứng"
            [multiple]="false"
            [maxFiles]="1"
            [required]="true"
            [(attachments)]="proofs"
          />
        } @else {
          <p class="cf-muted">Thủ quỹ sẽ xác nhận sau khi nhận tiền.</p>
        }

        @if (error(); as message) {
          <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
        }
      </div>
      <kendo-dialog-actions>
        <button kendoButton type="button" fillMode="outline" [disabled]="busy()" (click)="closed.emit()">Hủy</button>
        <button kendoButton type="button" themeColor="primary" [disabled]="!canSubmit() || busy()" (click)="submit()">
          {{ busy() ? 'Đang gửi…' : 'Xác nhận đã nộp' }}
        </button>
      </kendo-dialog-actions>
    </kendo-dialog>
  `,
  styles: `
    .body {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .amount {
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
      align-items: flex-start;
      gap: 10px;
      padding: 10px 12px;
      border: 1px solid var(--cf-border);
      border-radius: var(--cf-radius-lg);
      cursor: pointer;
    }
    .method span {
      display: flex;
      flex-direction: column;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .text {
      box-sizing: border-box;
      min-height: 40px;
      padding: 0 12px;
      border: 1px solid var(--cf-border-strong);
      border-radius: var(--cf-radius-lg);
      font: inherit;
    }
  `,
})
export class PayDialog {
  private readonly api = inject(ChargesApi);

  readonly fundId = input.required<string>();
  readonly target = input.required<PayTarget>();
  readonly paid = output<{ selfDeclared: boolean }>();
  readonly closed = output<void>();

  protected readonly method = signal<SubmitPaymentRequest['method']>('Cash');
  protected readonly transferCode = signal('');
  protected readonly proofs = signal<Attachment[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly canSubmit = computed(() => this.method() === 'Cash' || this.proofs().length > 0);

  protected async submit(): Promise<void> {
    if (!this.canSubmit() || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      const bank = this.method() === 'BankTransfer';
      const payment = await firstValueFrom(
        this.api.submit(this.fundId(), this.target().assignmentId, {
          method: this.method(),
          transferCode: bank ? this.transferCode().trim() || null : null,
          attachmentId: bank ? (this.proofs()[0]?.id ?? null) : null,
        }),
      );
      this.paid.emit({ selfDeclared: payment.isSelfDeclared });
    } catch (error) {
      this.error.set(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      this.busy.set(false);
    }
  }
}
