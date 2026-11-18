import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';

import { AuthService } from '../../core/auth/auth.service';
import { AppNotifications } from '../../core/services/notification.service';
import { applyServerErrors } from '../../shared/forms/server-errors';
import { AuthCard } from './auth-card';
import { AuthField } from './auth-field';
import { AUTH_FIELD_FOR } from './auth.errors';
import { differsFromValidator, passwordPolicyValidator, passwordsMatchValidator } from './auth-validators';
import { PasswordRules } from './password-rules';

/**
 * A-03 Đặt mật khẩu mới (FR-03). Khi đang dùng mật khẩu tạm đây là trang duy nhất dùng được
 * (guard + backend `password.change_required`); đổi xong các phiên khác bị đăng xuất (BR-29).
 * Đổi mật khẩu tự nguyện nằm ở A-04.
 */
@Component({
  selector: 'app-change-password-page',
  imports: [ReactiveFormsModule, RouterLink, ButtonComponent, AuthCard, AuthField, PasswordRules],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-card [title]="forced() ? 'Đặt mật khẩu mới' : 'Đổi mật khẩu'" [subtitle]="subtitle()">
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        @if (forced()) {
          <p class="banner" role="status">Trước khi đổi, bạn chỉ có thể đổi mật khẩu hoặc đăng xuất.</p>
        }
        @if (formError(); as message) {
          <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
        }
        <app-auth-field
          fieldId="change-current"
          [label]="forced() ? 'Mật khẩu tạm' : 'Mật khẩu hiện tại'"
          type="password"
          autocomplete="current-password"
          [control]="form.controls.currentPassword"
        />
        <app-auth-field
          fieldId="change-new"
          label="Mật khẩu mới"
          type="password"
          autocomplete="new-password"
          [messages]="{ passwordPolicy: '' }"
          [control]="form.controls.newPassword"
        >
          <app-password-rules mode="summary" [control]="form.controls.newPassword" />
        </app-auth-field>
        <app-auth-field
          fieldId="change-confirm"
          label="Nhập lại mật khẩu mới"
          type="password"
          autocomplete="new-password"
          [control]="form.controls.confirmPassword"
        />
        <button kendoButton type="submit" themeColor="primary" class="submit" [disabled]="submitting()">
          {{ submitting() ? 'Đang lưu…' : forced() ? 'Lưu và vào lớp' : 'Đổi mật khẩu' }}
        </button>
      </form>
      <p class="footer">
        @if (forced()) {
          <button type="button" class="link" (click)="logout()">Đăng xuất</button>
        } @else {
          <a routerLink="/account">Hủy</a>
        }
      </p>
    </app-auth-card>
  `,
  styles: `
    form {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .banner {
      margin: 0;
      padding: 12px;
      border: 1px solid var(--cf-warning-bg);
      border-radius: var(--cf-radius-lg);
      background: var(--cf-warning-bg);
      color: var(--cf-warning-fg);
    }
    .submit {
      width: 100%;
    }
    .footer {
      margin: 16px 0 0;
      text-align: center;
    }
    .link,
    .footer a {
      border: 0;
      background: none;
      color: var(--cf-text);
      font: inherit;
      font-weight: 600;
      cursor: pointer;
      text-decoration: none;
    }
  `,
})
export class ChangePasswordPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(AppNotifications);

  protected readonly forced = computed(() => this.auth.user()?.mustChangePassword ?? false);
  protected readonly subtitle = computed(() => {
    const name = this.auth.user()?.fullName;
    return this.forced()
      ? `Xin chào ${name ?? ''}. Bạn đang dùng mật khẩu tạm do thủ quỹ cấp – cần đổi trước khi dùng hệ thống.`
      : 'Đổi xong, các thiết bị khác sẽ bị đăng xuất.';
  });

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, passwordPolicyValidator]],
      confirmPassword: ['', [Validators.required]],
    },
    {
      validators: [
        passwordsMatchValidator('newPassword', 'confirmPassword'),
        differsFromValidator('currentPassword', 'newPassword'),
      ],
    },
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
      const { currentPassword, newPassword } = this.form.getRawValue();
      await this.auth.changePassword(currentPassword, newPassword);
      this.notifications.success('Đã đổi mật khẩu.');
      await this.router.navigateByUrl(this.auth.homeUrl());
    } catch (error) {
      this.formError.set(applyServerErrors(this.form, error, AUTH_FIELD_FOR));
    } finally {
      this.submitting.set(false);
    }
  }

  protected async logout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigateByUrl('/login');
  }
}
