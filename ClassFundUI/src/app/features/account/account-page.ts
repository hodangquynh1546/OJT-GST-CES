import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonComponent } from '@progress/kendo-angular-buttons';

import { AuthService } from '../../core/auth/auth.service';
import { AppNotifications } from '../../core/services/notification.service';
import { applyServerErrors } from '../../shared/forms/server-errors';
import { AuthField } from '../auth/auth-field';
import { AUTH_FIELD_FOR } from '../auth/auth.errors';
import {
  FULL_NAME_MAX_LENGTH,
  differsFromValidator,
  passwordPolicyValidator,
  passwordsMatchValidator,
} from '../auth/auth-validators';
import { PasswordRules } from '../auth/password-rules';
import { initialsOf } from '../../shared/utils/initials';
import { TopBar } from './top-bar';

/**
 * A-04 Tài khoản của tôi (FR-04): thông tin cá nhân dùng chung cho mọi quỹ + đổi mật khẩu.
 * Theo canvas, màn này không nằm trong khung có menu quỹ: chỉ có thanh trên (Quỹ của tôi, Đăng xuất).
 */
@Component({
  selector: 'app-account-page',
  imports: [ReactiveFormsModule, ButtonComponent, AuthField, PasswordRules, TopBar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-top-bar variant="account" />

    <main class="page">
      <h1>Tài khoản của tôi</h1>
      <p class="subtitle">Thông tin cá nhân dùng chung cho mọi quỹ bạn tham gia.</p>

      <section class="card" aria-labelledby="profile-title">
        <h2 id="profile-title">Thông tin cá nhân</h2>
        <div class="avatar-row">
          <span class="avatar" aria-hidden="true">{{ initials() }}</span>
          <div>
            <button type="button" kendoButton fillMode="outline" size="small" [disabled]="true" title="Tính năng đổi ảnh đại diện sẽ có sau">
              Đổi ảnh đại diện
            </button>
            <p class="caption">JPG/PNG, tối đa 2 MB</p>
          </div>
        </div>
        <form [formGroup]="profileForm" (ngSubmit)="saveProfile()" novalidate>
          @if (profileError(); as message) {
            <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
          }
          <app-auth-field
            fieldId="account-name"
            label="Họ và tên"
            autocomplete="name"
            [maxLength]="nameMaxLength"
            [control]="profileForm.controls.fullName"
          />
          <app-auth-field
            fieldId="account-email"
            label="Email"
            type="email"
            hint="Email dùng để đăng nhập, không đổi được."
            [readonly]="true"
            [control]="profileForm.controls.email"
          />
          <div class="actions">
            <button kendoButton type="submit" themeColor="primary" [disabled]="savingProfile() || profileForm.pristine">
              {{ savingProfile() ? 'Đang lưu…' : 'Lưu thay đổi' }}
            </button>
          </div>
        </form>
      </section>

      <section class="card" aria-labelledby="password-title">
        <h2 id="password-title">Đổi mật khẩu</h2>
        <form [formGroup]="passwordForm" (ngSubmit)="changePassword()" novalidate>
          @if (passwordError(); as message) {
            <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
          }
          <app-auth-field
            fieldId="account-current-password"
            label="Mật khẩu hiện tại"
            type="password"
            autocomplete="current-password"
            [control]="passwordForm.controls.currentPassword"
          />
          <div class="columns">
            <app-auth-field
              fieldId="account-new-password"
              label="Mật khẩu mới"
              type="password"
              autocomplete="new-password"
              [messages]="{ passwordPolicy: '' }"
              [control]="passwordForm.controls.newPassword"
            >
              <app-password-rules mode="summary" [control]="passwordForm.controls.newPassword" />
            </app-auth-field>
            <app-auth-field
              fieldId="account-confirm-password"
              label="Nhập lại"
              type="password"
              autocomplete="new-password"
              [control]="passwordForm.controls.confirmPassword"
            />
          </div>
          <div class="actions">
            <button kendoButton type="submit" fillMode="outline" [disabled]="savingPassword()">
              {{ savingPassword() ? 'Đang lưu…' : 'Đổi mật khẩu' }}
            </button>
          </div>
        </form>
      </section>
    </main>
  `,
  styles: `
    :host {
      display: block;
      min-height: 100dvh;
      background: var(--cf-background);
      --kendo-color-primary: var(--cf-ink);
      --kendo-color-primary-hover: var(--cf-ink-hover);
      --kendo-color-primary-active: var(--cf-ink-hover);
      --kendo-color-primary-emphasis: var(--cf-ink);
      --kendo-border-radius-md: var(--cf-radius-lg);
    }
    .page {
      box-sizing: border-box;
      width: 100%;
      max-width: 760px;
      margin: 0 auto;
      padding: 32px 24px 48px;
    }
    h1 {
      font-size: 24px;
    }
    .subtitle {
      margin: 4px 0 24px;
      color: var(--cf-text-muted);
    }
    .card {
      margin-bottom: 24px;
      padding: 24px;
      border: 1px solid var(--cf-border);
      border-radius: 12px;
      background: var(--cf-surface);
    }
    h2 {
      margin: 0 0 16px;
      font-size: 16px;
      line-height: 24px;
    }
    .avatar-row {
      display: flex;
      align-items: center;
      gap: 16px;
      margin-bottom: 16px;
    }
    .avatar {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 64px;
      height: 64px;
      border-radius: 32px;
      background: var(--cf-subtle);
      font-size: 20px;
      font-weight: 700;
    }
    .caption {
      margin: 6px 0 0;
      color: var(--cf-text-muted);
      font-size: 12px;
    }
    form {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .columns {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
      align-items: start;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
    }
  `,
})
export class AccountPage {
  private readonly auth = inject(AuthService);
  private readonly notifications = inject(AppNotifications);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly nameMaxLength = FULL_NAME_MAX_LENGTH;
  protected readonly initials = computed(() => initialsOf(this.auth.user()?.fullName));

  protected readonly profileForm = this.fb.group({
    fullName: ['', [Validators.required, Validators.maxLength(FULL_NAME_MAX_LENGTH), (c: { value: string }) => (c.value.trim() ? null : { required: true })]],
    email: [''],
  });
  protected readonly savingProfile = signal(false);
  protected readonly profileError = signal<string | null>(null);

  protected readonly passwordForm = this.fb.group(
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
  protected readonly savingPassword = signal(false);
  protected readonly passwordError = signal<string | null>(null);

  constructor() {
    effect(() => {
      const user = this.auth.user();
      if (user && this.profileForm.pristine) {
        this.profileForm.reset({ fullName: user.fullName, email: user.email });
      }
    });
  }

  protected async saveProfile(): Promise<void> {
    this.profileForm.markAllAsTouched();
    if (this.profileForm.invalid || this.savingProfile()) {
      return;
    }

    this.savingProfile.set(true);
    this.profileError.set(null);
    try {
      const user = await this.auth.updateProfile(this.profileForm.controls.fullName.value.trim());
      this.profileForm.reset({ fullName: user.fullName, email: user.email });
      this.notifications.success('Đã lưu thông tin cá nhân.');
    } catch (error) {
      this.profileError.set(applyServerErrors(this.profileForm, error, AUTH_FIELD_FOR));
    } finally {
      this.savingProfile.set(false);
    }
  }

  protected async changePassword(): Promise<void> {
    this.passwordForm.markAllAsTouched();
    if (this.passwordForm.invalid || this.savingPassword()) {
      return;
    }

    this.savingPassword.set(true);
    this.passwordError.set(null);
    try {
      const { currentPassword, newPassword } = this.passwordForm.getRawValue();
      await this.auth.changePassword(currentPassword, newPassword);
      this.passwordForm.reset();
      this.notifications.success('Đã đổi mật khẩu. Các thiết bị khác đã bị đăng xuất.');
    } catch (error) {
      this.passwordError.set(applyServerErrors(this.passwordForm, error, AUTH_FIELD_FOR));
    } finally {
      this.savingPassword.set(false);
    }
  }
}
