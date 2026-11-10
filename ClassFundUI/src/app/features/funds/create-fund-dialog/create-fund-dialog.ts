import { ChangeDetectionStrategy, Component, DestroyRef, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { LoaderComponent } from '@progress/kendo-angular-indicators';
import { ErrorComponent, FormFieldComponent, HintComponent, TextBoxComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { finalize } from 'rxjs';

import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { FUND_NAME_ERROR, fundNameValidator } from '../fund-name.validator';
import { FundsApi } from '../funds.api';
import { FUND_NAME_MAX_LENGTH } from '../funds.models';

export interface CreatedFund {
  id: string;
  name: string;
}

/**
 * Dialog "Tạo quỹ mới" 480 px (Canvas G-01, FR-23): một ô Tên quỹ, ghi chú danh mục mặc định.
 * Lỗi hiện sau khi bấm "Tạo quỹ" (design-system.md mục 6); khi gửi: tắt nút + spinner.
 */
@Component({
  selector: 'app-create-fund-dialog',
  imports: [
    ReactiveFormsModule,
    ButtonComponent,
    DialogComponent,
    DialogActionsComponent,
    ErrorComponent,
    FormFieldComponent,
    HintComponent,
    LabelComponent,
    LoaderComponent,
    TextBoxComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <kendo-dialog title="Tạo quỹ mới" [width]="480" (close)="cancel()">
      <form id="create-fund-form" class="form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        @if (summary(); as message) {
          <div class="cf-alert cf-alert--danger" role="alert">{{ message }}</div>
        }
        <kendo-formfield showHints="always" showErrors="always">
          <kendo-label [for]="name" text="Tên quỹ *" />
          <kendo-textbox
            #name
            formControlName="name"
            placeholder="Ví dụ: Quỹ lớp 6B · 2026–2027"
            [maxlength]="maxLength"
            [readonly]="saving()"
            aria-describedby="create-fund-name-help"
          />
          @if (invalid()) {
            <kendo-formerror id="create-fund-name-help">{{ nameError() }}</kendo-formerror>
          } @else {
            <kendo-formhint id="create-fund-name-help">Hiển thị cho học sinh và phụ huynh của quỹ.</kendo-formhint>
          }
        </kendo-formfield>
        <div class="note" role="note">
          Bạn là GVCN của quỹ này. Quỹ có sẵn danh mục:
          <i>Đóng quỹ, Thu khác, Văn phòng phẩm, Sinh hoạt, liên hoan, Khen thưởng, Chi khác</i>. Chưa ai ghi thu chi
          được cho tới khi bạn phân công một học sinh làm thủ quỹ.
        </div>
      </form>
      <kendo-dialog-actions layout="end">
        <button kendoButton type="button" [disabled]="saving()" (click)="cancel()">Hủy bỏ</button>
        <button
          kendoButton
          themeColor="primary"
          type="submit"
          form="create-fund-form"
          [disabled]="saving()"
          [attr.aria-busy]="saving()"
        >
          @if (saving()) {
            <kendo-loader size="small" type="infinite-spinner" themeColor="light" />
            Đang tạo…
          } @else {
            Tạo quỹ
          }
        </button>
      </kendo-dialog-actions>
    </kendo-dialog>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    kendo-formfield {
      margin: 0;
    }
    .note {
      padding: 10px 12px;
      border-radius: var(--cf-radius-md);
      background: var(--cf-subtle);
      font-size: 13px;
    }
    kendo-loader {
      margin-right: 6px;
    }
  `,
})
export class CreateFundDialog {
  private readonly api = inject(FundsApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly created = output<CreatedFund>();
  readonly closed = output<void>();

  protected readonly maxLength = FUND_NAME_MAX_LENGTH;
  protected readonly form = new FormGroup(
    { name: new FormControl('', { nonNullable: true, validators: fundNameValidator }) },
    { updateOn: 'submit' },
  );
  protected readonly submitted = signal(false);
  protected readonly saving = signal(false);
  protected readonly summary = signal<string | null>(null);
  /** Lỗi chỉ hiện sau lần bấm "Tạo quỹ" đầu tiên. */
  protected readonly invalid = signal(false);
  protected readonly nameError = signal(FUND_NAME_ERROR);

  protected submit(): void {
    if (this.saving()) {
      return;
    }
    this.submitted.set(true);
    this.summary.set(null);
    const control = this.form.controls.name;
    if (control.invalid) {
      this.showError(FUND_NAME_ERROR);
      return;
    }

    const name = control.value.trim();
    this.invalid.set(false);
    this.saving.set(true);
    this.api
      .create(name)
      .pipe(
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (id) => this.created.emit({ id, name }),
        error: (error: unknown) => {
          const message = applyServerErrors(this.form, error);
          const fieldMessage = serverError(control);
          if (fieldMessage) {
            this.showError(fieldMessage);
          }
          this.summary.set(message);
        },
      });
  }

  protected cancel(): void {
    if (!this.saving()) {
      this.closed.emit();
    }
  }

  private showError(message: string): void {
    this.nameError.set(message);
    this.form.controls.name.markAsTouched();
    this.invalid.set(true);
  }
}
