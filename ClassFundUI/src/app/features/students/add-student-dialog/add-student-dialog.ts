import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { LoaderComponent } from '@progress/kendo-angular-indicators';
import { ErrorComponent, FormFieldComponent, HintComponent, TextBoxComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { finalize } from 'rxjs';

import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { StudentsApi } from '../students.api';
import { PARENT_NAME_MAX_LENGTH, STUDENT_NAME_MAX_LENGTH, StudentCreated } from '../students.models';
import { EMAIL_ERROR, STUDENT_NAME_ERROR, optionalEmailValidator, studentNameValidator } from '../students.validators';

/** Kết quả thêm học sinh để trang hiện thông báo / hộp thoại mật khẩu tạm của phụ huynh. */
export interface AddedStudent {
  name: string;
  hadParentEmail: boolean;
  created: StudentCreated;
}

/**
 * Dialog "Thêm học sinh" (Canvas G-02, FR-24): Họ tên*, Email phụ huynh, Họ tên phụ huynh. Học sinh được thêm chưa có
 * tài khoản; có email phụ huynh thì liên kết luôn trong cùng thao tác (tạo tài khoản phụ huynh nếu email chưa có).
 */
@Component({
  selector: 'app-add-student-dialog',
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
    <kendo-dialog title="Thêm học sinh" [width]="520" (close)="cancel()">
      <form id="add-student-form" class="form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        @if (summary(); as message) {
          <div class="cf-alert cf-alert--danger" role="alert">{{ message }}</div>
        }
        <kendo-formfield showHints="always" showErrors="always">
          <kendo-label [for]="name" text="Họ tên học sinh *" />
          <kendo-textbox
            #name
            formControlName="fullName"
            placeholder="Ví dụ: Phạm Đức Huy"
            [maxlength]="nameMax"
            [readonly]="saving()"
          />
          @if (message(form.controls.fullName, nameError); as text) {
            <kendo-formerror>{{ text }}</kendo-formerror>
          }
        </kendo-formfield>

        <kendo-formfield showHints="always" showErrors="always">
          <kendo-label [for]="email" text="Email phụ huynh (tùy chọn)" />
          <kendo-textbox
            #email
            formControlName="parentEmail"
            inputmode="email"
            placeholder="ten@example.com"
            [readonly]="saving()"
          />
          @if (message(form.controls.parentEmail, emailError); as text) {
            <kendo-formerror>{{ text }}</kendo-formerror>
          } @else {
            <kendo-formhint>Có email → liên kết luôn. Bỏ trống → liên kết sau.</kendo-formhint>
          }
        </kendo-formfield>

        <kendo-formfield>
          <kendo-label [for]="parentName" text="Họ tên phụ huynh (tùy chọn)" />
          <kendo-textbox #parentName formControlName="parentFullName" [maxlength]="parentNameMax" [readonly]="saving()" />
        </kendo-formfield>

        <span class="cf-muted note">
          Học sinh được thêm <b>chưa có tài khoản</b>. Cấp tài khoản ở bước sau (Chi tiết học sinh).
        </span>
      </form>
      <kendo-dialog-actions layout="end">
        <button kendoButton type="button" [disabled]="saving()" (click)="cancel()">Hủy bỏ</button>
        <button
          kendoButton
          themeColor="primary"
          type="submit"
          form="add-student-form"
          [disabled]="saving()"
          [attr.aria-busy]="saving()"
        >
          @if (saving()) {
            <kendo-loader size="small" type="infinite-spinner" themeColor="light" />
            Đang lưu…
          } @else {
            Lưu học sinh
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
      font-size: 13px;
    }
    kendo-loader {
      margin-right: 6px;
    }
  `,
})
export class AddStudentDialog {
  private readonly api = inject(StudentsApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly fundId = input.required<string>();
  readonly added = output<AddedStudent>();
  readonly closed = output<void>();

  protected readonly nameMax = STUDENT_NAME_MAX_LENGTH;
  protected readonly parentNameMax = PARENT_NAME_MAX_LENGTH;
  protected readonly nameError = STUDENT_NAME_ERROR;
  protected readonly emailError = EMAIL_ERROR;
  protected readonly form = new FormGroup({
    fullName: new FormControl('', { nonNullable: true, validators: studentNameValidator }),
    parentEmail: new FormControl('', { nonNullable: true, validators: optionalEmailValidator }),
    parentFullName: new FormControl('', { nonNullable: true }),
  });
  protected readonly saving = signal(false);
  protected readonly summary = signal<string | null>(null);
  /** Lỗi chỉ hiện sau lần bấm "Lưu học sinh" đầu tiên (design-system.md mục 6). */
  protected readonly submitted = signal(false);

  /** Thông báo lỗi của một ô: lỗi từ server, hoặc lỗi kiểm tra sau khi đã bấm lưu. */
  protected message(control: AbstractControl, fallback: string): string | null {
    return serverError(control) ?? (this.submitted() && control.invalid ? fallback : null);
  }

  protected submit(): void {
    if (this.saving()) {
      return;
    }
    this.submitted.set(true);
    this.summary.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.summary.set('Chưa lưu được. Kiểm tra các ô được đánh dấu.');
      return;
    }

    const { fullName, parentEmail, parentFullName } = this.form.getRawValue();
    const email = parentEmail.trim();
    const parentName = parentFullName.trim();
    this.saving.set(true);
    this.api
      .create(this.fundId(), {
        fullName: fullName.trim(),
        parentEmail: email || undefined,
        parentFullName: parentName || undefined,
      })
      .pipe(
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (created) => this.added.emit({ name: fullName.trim(), hadParentEmail: email !== '', created }),
        error: (error: unknown) =>
          this.summary.set(applyServerErrors(this.form, error, { 'parent.email_in_use': 'parentEmail' })),
      });
  }

  protected cancel(): void {
    if (!this.saving()) {
      this.closed.emit();
    }
  }
}
