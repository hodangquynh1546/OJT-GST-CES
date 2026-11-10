import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { KENDO_BUTTON } from '@progress/kendo-angular-buttons';

import { AuthService } from '../../../core/auth/auth.service';
import { AppNotifications } from '../../../core/services/notification.service';
import { ChangePasswordForm } from '../change-password-form/change-password-form';
import { GuestCard } from '../guest-card';

/**
 * A-03 Đặt mật khẩu mới – bắt buộc khi còn mật khẩu tạm (authGuard đưa mọi route về đây).
 * Người đã có mật khẩu riêng vào `/change-password` thì đổi như A-04 rồi về trang chính.
 */
@Component({
  selector: 'app-change-password-page',
  imports: [GuestCard, ChangePasswordForm, KENDO_BUTTON],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: '../auth-form.scss',
  template: `
    <app-guest-card style="--auth-control-height: 40px; --guest-card-padding: 20px; --guest-card-radius: 8px">
      <div class="title">
        <h1>Đặt mật khẩu mới</h1>
      </div>
      @if (mustChange()) {
        <div role="status" class="cf-alert cf-alert--warning">
          Bạn đang dùng mật khẩu tạm do giáo viên chủ nhiệm cấp. Hãy đặt mật khẩu của riêng bạn để tiếp tục.
        </div>
      }
      <app-change-password-form [mode]="mustChange() ? 'forced' : 'normal'" (changed)="changed()" />
      <button kendoButton type="button" class="submit" (click)="logout()">Đăng xuất</button>
    </app-guest-card>
  `,
})
export class ChangePasswordPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(AppNotifications);

  readonly mustChange = () => this.auth.user()?.mustChangePassword ?? false;

  async changed(): Promise<void> {
    this.notifications.success('Đã đặt mật khẩu mới.');
    await this.router.navigateByUrl(this.auth.homeUrl());
  }

  async logout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigateByUrl('/login');
  }
}
