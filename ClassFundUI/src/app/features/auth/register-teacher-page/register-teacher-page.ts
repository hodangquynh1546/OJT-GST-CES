import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { KENDO_BUTTON } from '@progress/kendo-angular-buttons';
import { KENDO_LOADER } from '@progress/kendo-angular-indicators';
import { KENDO_FORMFIELD, KENDO_TEXTBOX } from '@progress/kendo-angular-inputs';
import { KENDO_LABEL } from '@progress/kendo-angular-label';

import { AuthService } from '../../../core/auth/auth.service';
import { AppNotifications } from '../../../core/services/notification.service';
import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { PASSWORD_PATTERN } from '../auth.errors';
import { GuestCard } from '../guest-card';

/** Mã lỗi nghiệp vụ → ô nhận lỗi. */
const FIELD_FOR = { 'user.email_taken': 'email', 'auth.password_policy': 'password' } as const;

function sameAsPassword(control: AbstractControl): ValidationErrors | null {
  const password = control.parent?.get('password')?.value;
  return control.value && control.value !== password ? { mismatch: true } : null;
}

/** A-01 Đăng ký giáo viên: tài khoản luôn là Giáo viên; thành công → đăng nhập luôn → G-01 trống. */
@Component({
  selector: 'app-register-teacher-page',
  imports: [ReactiveFormsModule, RouterLink, GuestCard, KENDO_BUTTON, KENDO_FORMFIELD, KENDO_TEXTBOX, KENDO_LABEL, KENDO_LOADER],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: '../auth-form.scss',
  template: `
    <app-guest-card>
      <div class="title">
        <h1>Đăng ký tài khoản giáo viên</h1>
        <span class="cf-muted">Dành cho giáo viên chủ nhiệm. Sau khi đăng ký, bạn tạo quỹ lớp, thêm học sinh và phân công thủ quỹ.</span>
      </div>

      @if (summary(); as text) {
        <div role="alert" class="cf-alert cf-alert--danger">{{ text }}</div>
      }

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <kendo-formfield showErrors="always">
          <kendo-label [for]="nameBox" text="Họ tên *" />
          <kendo-textbox #nameBox formControlName="fullName" [maxlength]="200" placeholder="Ví dụ: Trần Văn Hùng" [inputAttributes]="{ autocomplete: 'name' }" />
          @if (show('fullName')) {
            <kendo-formerror>{{ serverError(form.controls.fullName) ?? 'Nhập họ tên.' }}</kendo-formerror>
          }
        </kendo-formfield>

        <kendo-formfield showErrors="always">
          <kendo-label [for]="emailBox" text="Email *" />
          <kendo-textbox #emailBox formControlName="email" [maxlength]="254" placeholder="ten@truong.edu.vn" [inputAttributes]="{ autocomplete: 'email', inputmode: 'email', autocapitalize: 'none' }" />
          @if (show('email')) {
            <kendo-formerror>{{ serverError(form.controls.email) ?? (form.controls.email.hasError('email') ? 'Nhập email hợp lệ, ví dụ ten@truong.edu.vn' : 'Nhập email.') }}</kendo-formerror>
          } @else {
            <kendo-formhint>Dùng để đăng nhập. Không phân biệt chữ hoa / thường.</kendo-formhint>
          }
        </kendo-formfield>

        <kendo-formfield showErrors="always">
          <kendo-label [for]="passwordBox" text="Mật khẩu *" />
          <kendo-textbox #passwordBox type="password" formControlName="password" [inputAttributes]="{ autocomplete: 'new-password' }" />
          @if (show('password')) {
            <kendo-formerror>{{ serverError(form.controls.password) ?? (form.controls.password.hasError('required') ? 'Nhập mật khẩu.' : 'Mật khẩu cần 8–128 ký tự, có cả chữ và số.') }}</kendo-formerror>
          } @else {
            <kendo-formhint>8–128 ký tự, có cả chữ và số.</kendo-formhint>
          }
        </kendo-formfield>

        <kendo-formfield showErrors="always">
          <kendo-label [for]="confirmBox" text="Xác nhận mật khẩu *" />
          <kendo-textbox #confirmBox type="password" formControlName="confirmPassword" [inputAttributes]="{ autocomplete: 'new-password' }" />
          @if (show('confirmPassword')) {
            <kendo-formerror>{{ serverError(form.controls.confirmPassword) ?? (form.controls.confirmPassword.hasError('required') ? 'Nhập lại mật khẩu.' : 'Mật khẩu xác nhận không khớp.') }}</kendo-formerror>
          }
        </kendo-formfield>

        <div role="note" class="note">
          Tài khoản đăng ký ở đây luôn là <b>Giáo viên</b>. Không chọn được vai trò khác. Giáo viên không ghi thu chi – việc đó do học sinh được phân công thủ quỹ.
        </div>

        <button kendoButton themeColor="primary" type="submit" class="submit" [disabled]="busy()">
          @if (busy()) {
            <kendo-loader size="small" type="pulsing" themeColor="light" />
            Đang tạo tài khoản…
          } @else {
            Đăng ký
          }
        </button>
      </form>

      <span class="links">
        <span class="cf-muted">Đã có tài khoản?</span>
        <a routerLink="/login"><b>Đăng nhập</b></a>
      </span>
    </app-guest-card>
  `,
})
export class RegisterTeacherPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(AppNotifications);

  readonly form = inject(NonNullableFormBuilder).group({
    fullName: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: ['', [Validators.required, Validators.pattern(PASSWORD_PATTERN)]],
    confirmPassword: ['', [Validators.required, sameAsPassword]],
  });
  readonly submitted = signal(false);
  readonly busy = signal(false);
  readonly summary = signal<string | null>(null);
  readonly serverError = serverError;

  constructor() {
    this.form.controls.password.valueChanges.subscribe(() => this.form.controls.confirmPassword.updateValueAndValidity());
  }

  show(field: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[field];
    return control.invalid && (this.submitted() || control.hasError('server'));
  }

  async submit(): Promise<void> {
    this.submitted.set(true);
    this.form.markAllAsTouched();
    this.summary.set(null);
    if (this.form.invalid || this.busy()) {
      this.summary.set(this.form.invalid ? 'Chưa đăng ký được. Kiểm tra các ô được đánh dấu.' : null);
      return;
    }
    this.busy.set(true);
    try {
      const value = this.form.getRawValue();
      await this.auth.registerTeacher({ ...value, fullName: value.fullName.trim(), email: value.email.trim() });
      this.notifications.success('Đã tạo tài khoản giáo viên. Bước tiếp theo: tạo quỹ lớp đầu tiên.');
      await this.router.navigateByUrl(this.auth.homeUrl());
    } catch (error) {
      this.summary.set(applyServerErrors(this.form, error, FIELD_FOR) ?? 'Chưa đăng ký được. Kiểm tra các ô được đánh dấu.');
    } finally {
      this.busy.set(false);
    }
  }
}
