import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';

import { AuthService } from '../../core/auth/auth.service';
import { applyServerErrors } from '../../shared/forms/server-errors';
import { AuthField } from './auth-field';
import { AUTH_FIELD_FOR } from './auth.errors';
import { emailFormatValidator } from './auth-validators';

/** A-01 Đăng nhập (FR-02): bố cục chia đôi theo canvas "ClassFund UI Design". */
@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink, ButtonComponent, AuthField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="split">
      <aside class="brand-panel" aria-hidden="true">
        <div class="brand">
          <span class="logo">
            <svg width="20" height="20" viewBox="0 0 24 24">
              <path d="M4 7h16v12H4zM4 7l2-3h12l2 3M9 12h6" fill="none" stroke="currentColor" stroke-width="2"
                stroke-linecap="round" stroke-linejoin="round" />
            </svg>
          </span>
          <b>ClassFund</b>
        </div>
        <div class="pitch">
          <p class="headline">Quỹ lớp minh bạch, tách bạch quỹ chung và quỹ sự kiện.</p>
          <p class="lead">
            Thu tiền, xác nhận thanh toán, ghi thu chi có chứng từ và quyết toán từng sự kiện – mọi thao tác đều có nhật ký.
          </p>
        </div>
        <p class="audience">Dành cho thủ quỹ và học sinh trong lớp.</p>
      </aside>

      <main class="form-panel">
        <section class="form" aria-labelledby="login-title">
          <h1 id="login-title">Đăng nhập</h1>
          <p class="subtitle">Nhập email và mật khẩu để vào quỹ của bạn</p>

          <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
            <app-auth-field
              fieldId="login-email"
              label="Email"
              type="email"
              autocomplete="username"
              [control]="form.controls.email"
            />
            <div class="password">
              <span class="forgot">Quên mật khẩu? Liên hệ thủ quỹ</span>
              <app-auth-field
                fieldId="login-password"
                label="Mật khẩu"
                type="password"
                autocomplete="current-password"
                [control]="form.controls.password"
              />
            </div>
            @if (formError(); as message) {
              <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
            }
            <button kendoButton type="submit" themeColor="primary" class="submit" [disabled]="submitting()">
              {{ submitting() ? 'Đang đăng nhập…' : 'Đăng nhập' }}
            </button>
          </form>

          <div class="divider" role="separator"><span>CHƯA CÓ TÀI KHOẢN</span></div>
          <a routerLink="/register" class="cf-link-button register">Đăng ký làm thủ quỹ</a>
          <p class="note">Học sinh được thủ quỹ thêm vào lớp và nhận mật khẩu tạm – đổi mật khẩu ở lần đăng nhập đầu.</p>
        </section>
      </main>
    </div>
  `,
  styles: `
    .split {
      display: flex;
      min-height: 100dvh;
    }
    .brand-panel {
      display: flex;
      flex: 1 1 50%;
      flex-direction: column;
      justify-content: space-between;
      box-sizing: border-box;
      padding: 40px;
      background: var(--cf-ink);
      color: var(--cf-surface);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 10px;
      font-size: 18px;
    }
    .logo {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      border-radius: 8px;
      background: var(--cf-surface);
      color: var(--cf-ink);
    }
    .pitch {
      max-width: 520px;
    }
    .headline {
      margin: 0 0 16px;
      font-size: 32px;
      font-weight: 700;
      line-height: 36px;
    }
    .lead,
    .audience {
      margin: 0;
      color: var(--cf-ink-soft);
      font-size: 15px;
      line-height: 24px;
    }
    .audience {
      font-size: 13px;
    }
    .form-panel {
      display: flex;
      flex: 1 1 50%;
      align-items: center;
      justify-content: center;
      box-sizing: border-box;
      padding: 32px;
      background: var(--cf-surface);
    }
    .form {
      width: 100%;
      max-width: 380px;
    }
    h1 {
      text-align: center;
      font-size: 24px;
    }
    .subtitle {
      margin: 8px 0 24px;
      color: var(--cf-text-muted);
      text-align: center;
    }
    form {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .password {
      position: relative;
    }
    .forgot {
      position: absolute;
      top: 0;
      right: 0;
      color: var(--cf-text-muted);
      font-size: 13px;
    }
    .submit {
      width: 100%;
    }
    .divider {
      display: flex;
      align-items: center;
      gap: 12px;
      margin: 24px 0;
      color: var(--cf-text-muted);
      font-size: 12px;
      letter-spacing: 0.04em;
    }
    .divider::before,
    .divider::after {
      flex: 1;
      height: 1px;
      background: var(--cf-border);
      content: '';
    }
    .register {
      display: flex;
      width: 100%;
    }
    .note {
      margin: 24px 0 0;
      color: var(--cf-text-muted);
      font-size: 13px;
      text-align: center;
    }
    @media (max-width: 900px) {
      .brand-panel {
        display: none;
      }
    }
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, emailFormatValidator]],
    password: ['', [Validators.required]],
  });
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
      const { email, password } = this.form.getRawValue();
      await this.auth.login(email.trim(), password);
      await this.router.navigateByUrl(this.auth.homeUrl());
    } catch (error) {
      this.formError.set(applyServerErrors(this.form, error, AUTH_FIELD_FOR));
    } finally {
      this.submitting.set(false);
    }
  }
}
