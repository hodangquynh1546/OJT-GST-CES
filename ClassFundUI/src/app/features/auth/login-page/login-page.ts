import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { KENDO_BUTTON } from '@progress/kendo-angular-buttons';
import { KENDO_LOADER } from '@progress/kendo-angular-indicators';
import { KENDO_FORMFIELD, KENDO_TEXTBOX } from '@progress/kendo-angular-inputs';
import { KENDO_LABEL } from '@progress/kendo-angular-label';

import { AuthService } from '../../../core/auth/auth.service';
import { ApiError } from '../../../shared/models/api-error';
import { GuestCard } from '../guest-card';

type LoginAlert = { kind: 'danger' | 'disabled' | 'warning'; text: string };

/** A-02 Đăng nhập: một form chung, một ô "Email hoặc tên đăng nhập", không chọn vai trò (D-34). */
@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink, GuestCard, KENDO_BUTTON, KENDO_FORMFIELD, KENDO_TEXTBOX, KENDO_LABEL, KENDO_LOADER],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: '../auth-form.scss',
  template: `
    <app-guest-card [width]="400">
      <div class="title">
        <h1>Đăng nhập</h1>
        <span class="cf-muted">Một màn hình cho mọi người dùng. Hệ thống tự nhận biết tài khoản và vai trò của bạn trong từng quỹ.</span>
      </div>

      @if (alert(); as a) {
        <div role="alert" class="cf-alert" [class.cf-alert--danger]="a.kind === 'danger'" [class.cf-alert--warning]="a.kind === 'warning'"
          [class.disabled]="a.kind === 'disabled'">
          @if (a.kind === 'disabled') {
            <b>Tài khoản của bạn đang bị khóa.</b><br />Vui lòng liên hệ giáo viên chủ nhiệm để được mở khóa.
          } @else {
            {{ a.text }}
          }
        </div>
      }

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <kendo-formfield showErrors="always">
          <kendo-label [for]="loginBox" text="Email hoặc tên đăng nhập" />
          <kendo-textbox #loginBox formControlName="login" [inputAttributes]="{ autocomplete: 'username', autocapitalize: 'none', spellcheck: 'false' }" />
          @if (submitted() && form.controls.login.invalid) {
            <kendo-formerror>Nhập email hoặc tên đăng nhập.</kendo-formerror>
          } @else {
            <kendo-formhint>Giáo viên, phụ huynh: email. Học sinh: tên đăng nhập do GVCN cấp (vd. khang.nguyen.204).</kendo-formhint>
          }
        </kendo-formfield>

        <kendo-formfield showErrors="always">
          <kendo-label [for]="passwordBox" text="Mật khẩu" />
          <kendo-textbox #passwordBox type="password" formControlName="password" [inputAttributes]="{ autocomplete: 'current-password' }" />
          @if (submitted() && form.controls.password.invalid) {
            <kendo-formerror>Nhập mật khẩu.</kendo-formerror>
          }
        </kendo-formfield>

        <button kendoButton themeColor="primary" type="submit" class="submit" [disabled]="busy()">
          @if (busy()) {
            <kendo-loader size="small" type="pulsing" themeColor="light" />
            Đang đăng nhập…
          } @else {
            Đăng nhập
          }
        </button>
      </form>

      <span class="cf-muted small">Quên mật khẩu? Học sinh: nhờ giáo viên chủ nhiệm cấp lại mật khẩu tạm. Giáo viên, phụ huynh: liên hệ quản trị hệ thống của trường.</span>

      <div class="footer">
        <span class="links">
          <span class="cf-muted">Giáo viên chưa có tài khoản?</span>
          <a routerLink="/register-teacher"><b>Đăng ký tài khoản giáo viên</b></a>
        </span>
        <span class="cf-muted small">Học sinh và phụ huynh không tự đăng ký: tài khoản do giáo viên chủ nhiệm cấp.</span>
      </div>
    </app-guest-card>
  `,
  styles: `
    .disabled {
      background: var(--cf-subtle);
      color: var(--cf-text);
    }
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly form = inject(NonNullableFormBuilder).group({
    login: ['', [Validators.required, Validators.pattern(/\S/)]],
    password: ['', Validators.required],
  });
  readonly submitted = signal(false);
  readonly busy = signal(false);
  readonly alert = signal<LoginAlert | null>(null);

  async submit(): Promise<void> {
    this.submitted.set(true);
    this.form.markAllAsTouched();
    if (this.form.invalid || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.alert.set(null);
    try {
      const { login, password } = this.form.getRawValue();
      await this.auth.login(login.trim(), password);
      await this.router.navigateByUrl(this.auth.homeUrl());
    } catch (error) {
      this.alert.set(toAlert(error));
      // Xóa mật khẩu đã nhập; lỗi "Nhập mật khẩu." chỉ hiện lại khi người dùng bấm Đăng nhập lần nữa.
      this.form.controls.password.reset('');
      this.submitted.set(false);
    } finally {
      this.busy.set(false);
    }
  }
}

function toAlert(error: unknown): LoginAlert {
  const apiError = error instanceof ApiError ? error : null;
  switch (apiError?.code) {
    case 'auth.account_disabled':
      return { kind: 'disabled', text: apiError.message };
    case 'auth.rate_limited':
      return { kind: 'warning', text: apiError.message };
    default:
      return { kind: 'danger', text: apiError?.message ?? 'Đã có lỗi xảy ra. Vui lòng thử lại.' };
  }
}
