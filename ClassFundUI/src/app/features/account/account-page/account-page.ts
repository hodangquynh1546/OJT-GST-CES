import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { KENDO_BUTTON } from '@progress/kendo-angular-buttons';
import { KENDO_FORMFIELD, KENDO_TEXTBOX } from '@progress/kendo-angular-inputs';
import { KENDO_LABEL } from '@progress/kendo-angular-label';

import { AuthService } from '../../../core/auth/auth.service';
import { AppNotifications } from '../../../core/services/notification.service';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { ChangePasswordForm } from '../../auth/change-password-form/change-password-form';
import { describeAccount } from '../account-info';

/** A-04 Tài khoản: xem thông tin, sửa họ tên, đổi mật khẩu (phiên khác bị đăng xuất), đăng xuất. */
@Component({
  selector: 'app-account-page',
  imports: [ReactiveFormsModule, PageHeader, ChangePasswordForm, KENDO_BUTTON, KENDO_FORMFIELD, KENDO_TEXTBOX, KENDO_LABEL],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-page cf-page--narrow">
      <app-page-header title="Tài khoản" />
      @if (user(); as me) {
        <div class="grid">
          <section class="cf-card card" aria-labelledby="acc-info">
            <h2 id="acc-info">Thông tin</h2>
            <dl>
              <dt>Họ tên</dt>
              <dd>{{ me.fullName }}</dd>
              <dt>{{ info()?.loginLabel }}</dt>
              <dd>{{ info()?.login }}</dd>
              <dt>Loại tài khoản</dt>
              <dd>{{ info()?.kindLabel }}</dd>
              <dt>{{ info()?.fundsLabel }}</dt>
              <dd>{{ info()?.funds }}</dd>
            </dl>

            @if (editing()) {
              <form [formGroup]="nameForm" (ngSubmit)="saveName()" novalidate>
                <kendo-formfield showErrors="always">
                  <kendo-label [for]="nameBox" text="Họ tên mới" />
                  <kendo-textbox #nameBox formControlName="fullName" [maxlength]="200" />
                  @if (nameForm.controls.fullName.invalid && (nameTried() || nameForm.controls.fullName.hasError('server'))) {
                    <kendo-formerror>{{ serverError(nameForm.controls.fullName) ?? 'Nhập họ tên.' }}</kendo-formerror>
                  }
                </kendo-formfield>
                <div class="actions">
                  <button kendoButton themeColor="primary" type="submit" [disabled]="savingName()">Lưu họ tên</button>
                  <button kendoButton type="button" (click)="editing.set(false)">Hủy</button>
                </div>
              </form>
            }

            @if (nameSaved()) {
              <div role="status" class="cf-alert saved">Đã cập nhật họ tên.</div>
            }
            <span class="cf-muted note">{{ info()?.note }}</span>
            <div class="actions">
              @if (!editing()) {
                <button kendoButton type="button" (click)="editName()">Sửa họ tên</button>
              }
              <button kendoButton type="button" (click)="logout()">Đăng xuất</button>
            </div>
          </section>

          <section class="cf-card card" aria-labelledby="acc-pw">
            <h2 id="acc-pw">Đổi mật khẩu</h2>
            <span class="cf-muted note">Đổi mật khẩu sẽ đăng xuất tài khoản này trên mọi thiết bị khác.</span>
            <app-change-password-form mode="normal" />
          </section>
        </div>
      }
    </div>
  `,
  styles: `
    .grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 16px;
      align-items: start;
    }
    .card {
      display: flex;
      flex-direction: column;
      gap: 14px;
      padding: 20px;
    }
    h2 {
      margin: 0;
      font-size: 17px;
    }
    dl {
      display: grid;
      grid-template-columns: 120px minmax(0, 1fr);
      gap: 10px 12px;
      margin: 0;
    }
    dt {
      color: var(--cf-text-muted);
    }
    dd {
      margin: 0;
      font-weight: 600;
    }
    form {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    kendo-formfield {
      margin: 0;
    }
    .note {
      font-size: 13px;
    }
    .saved {
      background: var(--cf-success-bg);
      color: var(--cf-success-fg);
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
  `,
})
export class AccountPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(AppNotifications);

  readonly user = this.auth.user;
  readonly info = computed(() => {
    const me = this.user();
    return me ? describeAccount(me) : null;
  });
  readonly nameForm = inject(NonNullableFormBuilder).group({
    fullName: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
  });
  readonly editing = signal(false);
  readonly nameTried = signal(false);
  readonly savingName = signal(false);
  readonly nameSaved = signal(false);
  readonly serverError = serverError;

  editName(): void {
    this.nameForm.reset({ fullName: this.user()?.fullName ?? '' });
    this.nameTried.set(false);
    this.nameSaved.set(false);
    this.editing.set(true);
  }

  async saveName(): Promise<void> {
    this.nameTried.set(true);
    this.nameForm.markAllAsTouched();
    if (this.nameForm.invalid || this.savingName()) {
      return;
    }
    this.savingName.set(true);
    try {
      await this.auth.updateProfile(this.nameForm.getRawValue().fullName.trim());
      this.editing.set(false);
      this.nameSaved.set(true);
    } catch (error) {
      const message = applyServerErrors(this.nameForm, error);
      if (message) {
        this.notifications.error(message);
      }
      this.nameSaved.set(false);
    } finally {
      this.savingName.set(false);
    }
  }

  async logout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigateByUrl('/login');
  }
}
