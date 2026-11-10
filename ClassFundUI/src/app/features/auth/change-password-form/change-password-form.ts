import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { KENDO_BUTTON } from '@progress/kendo-angular-buttons';
import { KENDO_LOADER } from '@progress/kendo-angular-indicators';
import { KENDO_FORMFIELD, KENDO_TEXTBOX } from '@progress/kendo-angular-inputs';
import { KENDO_LABEL } from '@progress/kendo-angular-label';

import { AuthService } from '../../../core/auth/auth.service';
import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { PASSWORD_PATTERN } from '../auth.errors';

const FIELD_FOR = {
  'auth.wrong_password': 'currentPassword',
  'auth.password_policy': 'newPassword',
  'auth.password_reused': 'newPassword',
} as const;

function sameAsNew(control: AbstractControl): ValidationErrors | null {
  const value = control.parent?.get('newPassword')?.value;
  return control.value && control.value !== value ? { mismatch: true } : null;
}

function differentFromCurrent(control: AbstractControl): ValidationErrors | null {
  const current = control.parent?.get('currentPassword')?.value;
  return control.value && control.value === current ? { reused: true } : null;
}

/**
 * Form đổi mật khẩu dùng chung: A-03 (`mode="forced"` – mật khẩu tạm) và A-04 (`mode="normal"`).
 * Thành công: phiên này nhận cookie mới, các phiên khác bị đăng xuất → phát `changed`.
 */
@Component({
  selector: 'app-change-password-form',
  imports: [ReactiveFormsModule, KENDO_BUTTON, KENDO_FORMFIELD, KENDO_TEXTBOX, KENDO_LABEL, KENDO_LOADER],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: '../auth-form.scss',
  template: `
    @if (summary(); as text) {
      <div role="alert" class="cf-alert cf-alert--danger">{{ text }}</div>
    }
    <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
      <kendo-formfield showErrors="always">
        <kendo-label [for]="currentBox" [text]="forced() ? 'Mật khẩu tạm' : 'Mật khẩu hiện tại'" />
        <kendo-textbox #currentBox type="password" formControlName="currentPassword" [inputAttributes]="{ autocomplete: 'current-password' }" />
        @if (show('currentPassword')) {
          <kendo-formerror>{{ serverError(form.controls.currentPassword) ?? (forced() ? 'Nhập mật khẩu tạm.' : 'Nhập mật khẩu hiện tại.') }}</kendo-formerror>
        }
      </kendo-formfield>

      <kendo-formfield showErrors="always">
        <kendo-label [for]="newBox" text="Mật khẩu mới" />
        <kendo-textbox #newBox type="password" formControlName="newPassword" [inputAttributes]="{ autocomplete: 'new-password' }" />
        @if (show('newPassword')) {
          <kendo-formerror>{{ newPasswordError() }}</kendo-formerror>
        } @else {
          <kendo-formhint>{{ forced() ? '8–128 ký tự, có chữ và số, khác mật khẩu tạm.' : '8–128 ký tự, có cả chữ và số.' }}</kendo-formhint>
        }
      </kendo-formfield>

      <kendo-formfield showErrors="always">
        <kendo-label [for]="confirmBox" text="Nhập lại mật khẩu mới" />
        <kendo-textbox #confirmBox type="password" formControlName="confirmPassword" [inputAttributes]="{ autocomplete: 'new-password' }" />
        @if (show('confirmPassword')) {
          <kendo-formerror>{{ form.controls.confirmPassword.hasError('required') ? 'Nhập lại mật khẩu mới.' : 'Mật khẩu nhập lại không khớp.' }}</kendo-formerror>
        }
      </kendo-formfield>

      @if (done()) {
        <div role="status" class="cf-alert">Đã đổi mật khẩu. Các thiết bị khác đã bị đăng xuất.</div>
      }

      <button kendoButton themeColor="primary" type="submit" [class.submit]="forced()" [disabled]="busy()">
        @if (busy()) {
          <kendo-loader size="small" type="pulsing" themeColor="light" />
          Đang lưu…
        } @else {
          {{ forced() ? 'Lưu và tiếp tục' : 'Đổi mật khẩu' }}
        }
      </button>
    </form>
  `,
  styles: `
    :host {
      --auth-control-height: 40px;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    button:not(.submit) {
      align-self: flex-start;
    }
  `,
})
export class ChangePasswordForm {
  private readonly auth = inject(AuthService);

  readonly mode = input<'forced' | 'normal'>('normal');
  readonly changed = output<void>();

  readonly forced = computed(() => this.mode() === 'forced');
  readonly form = inject(NonNullableFormBuilder).group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.pattern(PASSWORD_PATTERN), differentFromCurrent]],
    confirmPassword: ['', [Validators.required, sameAsNew]],
  });
  readonly submitted = signal(false);
  readonly busy = signal(false);
  readonly done = signal(false);
  readonly summary = signal<string | null>(null);
  readonly serverError = serverError;

  constructor() {
    const controls = this.form.controls;
    controls.currentPassword.valueChanges.subscribe(() => controls.newPassword.updateValueAndValidity({ emitEvent: false }));
    controls.newPassword.valueChanges.subscribe(() => controls.confirmPassword.updateValueAndValidity({ emitEvent: false }));
    this.form.valueChanges.subscribe(() => this.done.set(false));
  }

  show(field: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[field];
    return control.invalid && (this.submitted() || control.hasError('server'));
  }

  newPasswordError(): string {
    const control = this.form.controls.newPassword;
    return (
      serverError(control) ??
      (control.hasError('required')
        ? 'Nhập mật khẩu mới.'
        : control.hasError('reused')
          ? 'Mật khẩu mới phải khác mật khẩu hiện tại.'
          : 'Mật khẩu mới cần 8–128 ký tự, có cả chữ và số.')
    );
  }

  async submit(): Promise<void> {
    this.submitted.set(true);
    this.form.markAllAsTouched();
    this.summary.set(null);
    if (this.form.invalid) {
      this.summary.set('Chưa đổi được. Kiểm tra các ô được đánh dấu.');
      return;
    }
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      const { currentPassword, newPassword } = this.form.getRawValue();
      await this.auth.changePassword(currentPassword, newPassword);
      this.form.reset();
      this.submitted.set(false);
      this.done.set(true);
      this.changed.emit();
    } catch (error) {
      this.summary.set(applyServerErrors(this.form, error, FIELD_FOR) ?? 'Chưa đổi được. Kiểm tra các ô được đánh dấu.');
    } finally {
      this.busy.set(false);
    }
  }
}
