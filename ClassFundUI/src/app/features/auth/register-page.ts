import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';

import { AuthService } from '../../core/auth/auth.service';
import { applyServerErrors } from '../../shared/forms/server-errors';
import { AuthCard } from './auth-card';
import { AuthField } from './auth-field';
import { AUTH_FIELD_FOR } from './auth.errors';
import {
  FULL_NAME_MAX_LENGTH,
  emailFormatValidator,
  passwordPolicyValidator,
  passwordsMatchValidator,
} from './auth-validators';
import { PasswordRules } from './password-rules';

/** A-02 Đăng ký thủ quỹ (FR-01): tự đăng ký xong được đăng nhập luôn và có quyền tạo quỹ. */
@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink, ButtonComponent, AuthCard, AuthField, PasswordRules],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-card
      title="Đăng ký thủ quỹ"
      subtitle="Tài khoản tự đăng ký được tạo quỹ và trở thành thủ quỹ chính của quỹ đó."
      [backLink]="{ url: '/login', label: 'Quay lại đăng nhập' }"
    >
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        @if (formError(); as message) {
          <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
        }
        <app-auth-field
          fieldId="register-name"
          label="Họ và tên"
          autocomplete="name"
          [required]="true"
          [maxLength]="nameMaxLength"
          [control]="form.controls.fullName"
        />
        <app-auth-field
          fieldId="register-email"
          label="Email"
          type="email"
          autocomplete="username"
          [required]="true"
          [control]="form.controls.email"
        />
        <app-auth-field
          fieldId="register-password"
          label="Mật khẩu"
          type="password"
          autocomplete="new-password"
          [required]="true"
          [messages]="{ passwordPolicy: '' }"
          [control]="form.controls.password"
        >
          <app-password-rules [control]="form.controls.password" />
        </app-auth-field>
        <app-auth-field
          fieldId="register-confirm"
          label="Nhập lại mật khẩu"
          type="password"
          autocomplete="new-password"
          [required]="true"
          [control]="form.controls.confirmPassword"
        />
        <div class="consent">
          <label class="consent-label">
            <input type="checkbox" formControlName="accepted" />
            <span>Tôi dùng hệ thống để quản lý quỹ lớp và chịu trách nhiệm về số liệu nhập vào.</span>
          </label>
          @if (form.controls.accepted.touched && form.controls.accepted.invalid) {
            <p class="consent-error" role="alert">Vui lòng xác nhận để tiếp tục.</p>
          }
        </div>
        <button kendoButton type="submit" themeColor="primary" class="submit" [disabled]="submitting()">
          {{ submitting() ? 'Đang tạo tài khoản…' : 'Tạo tài khoản' }}
        </button>
      </form>
      <p class="footer">Đã có tài khoản? <a routerLink="/login">Đăng nhập</a></p>
    </app-auth-card>
  `,
  styles: `
    form {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .consent-label {
      display: flex;
      align-items: flex-start;
      gap: 10px;
    }
    .consent-label input {
      flex: none;
      width: 16px;
      height: 16px;
      margin: 2px 0 0;
      accent-color: var(--cf-ink);
    }
    .consent-error {
      margin: 6px 0 0;
      color: var(--cf-danger-fg);
      font-size: 13px;
    }
    .submit {
      width: 100%;
    }
    .footer {
      margin: 20px 0 0;
      color: var(--cf-text-muted);
      text-align: center;
    }
    .footer a {
      color: var(--cf-text);
      font-weight: 600;
    }
  `,
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly nameMaxLength = FULL_NAME_MAX_LENGTH;

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      fullName: ['', [Validators.required, Validators.maxLength(FULL_NAME_MAX_LENGTH), noBlank]],
      email: ['', [Validators.required, emailFormatValidator]],
      password: ['', [Validators.required, passwordPolicyValidator]],
      confirmPassword: ['', [Validators.required]],
      accepted: [false, [Validators.requiredTrue]],
    },
    { validators: passwordsMatchValidator('password', 'confirmPassword') },
  );
  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.formError.set(null);
    try {
      const { fullName, email, password } = this.form.getRawValue();
      await this.auth.register({ fullName: fullName.trim(), email: email.trim(), password });
      await this.router.navigateByUrl(this.auth.homeUrl());
    } catch (error) {
      // Backend dùng khóa `password` cho ô mật khẩu khi đăng ký.
      this.formError.set(applyServerErrors(this.form, error, { ...AUTH_FIELD_FOR, 'auth.password_policy': 'password' }));
    } finally {
      this.submitting.set(false);
    }
  }
}

function noBlank(control: { value: string }): { required: true } | null {
  return control.value.trim().length === 0 ? { required: true } : null;
}
