import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DropDownListComponent } from '@progress/kendo-angular-dropdowns';
import { FormFieldComponent, ErrorComponent, HintComponent, TextBoxComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { AppNotifications } from '../../../core/services/notification.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { TopBar } from '../../account/top-bar';
import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { FundsApi } from '../funds.api';
import '../funds.errors';
import { FUND_NAME_MAX, createFundForm, fundBody } from '../fund-form';
import { DEFAULT_CATEGORIES, currentSchoolYear, schoolYearOptions } from '../funds.models';

/** F-02 · Tạo quỹ (FR-05): người tạo là thủ quỹ chính, danh mục mặc định tạo sẵn. */
@Component({
  selector: 'app-fund-new',
  imports: [
    AppState,
    ButtonComponent,
    DropDownListComponent,
    ErrorComponent,
    FormFieldComponent,
    HintComponent,
    LabelComponent,
    PageHeader,
    ReactiveFormsModule,
    RouterLink,
    TextBoxComponent,
    TopBar,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-top-bar variant="funds" />
    <main class="page">
      <a routerLink="/funds" class="back cf-muted">← Quỹ của tôi</a>
      <app-page-header title="Tạo quỹ" subtitle="Bạn sẽ là thủ quỹ chính. Danh mục thu chi mặc định được tạo sẵn." />

      @if (!auth.canCreateFund()) {
        <app-state
          kind="forbidden"
          title="Tài khoản của bạn không tạo quỹ được"
          message="Chỉ tài khoản tự đăng ký làm thủ quỹ mới tạo được quỹ. Học sinh được thủ quỹ thêm vào quỹ bằng email."
        />
      } @else {
        <form class="cf-card form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          @if (formError(); as message) {
            <div class="cf-alert cf-alert--danger" role="alert">{{ message }}</div>
          }

          <kendo-formfield>
            <kendo-label [for]="nameInput" text="Tên quỹ *" />
            <kendo-textbox #nameInput formControlName="name" [maxlength]="nameMax" placeholder="VD: Lớp 10A1" />
            <kendo-formhint>VD: Lớp 10A1, CLB Tiếng Anh</kendo-formhint>
            <kendo-formerror>{{ serverError(form.controls.name) ?? 'Vui lòng nhập tên quỹ (tối đa 100 ký tự).' }}</kendo-formerror>
          </kendo-formfield>

          <kendo-formfield>
            <kendo-label [for]="yearInput" text="Năm học *" />
            <kendo-dropdownlist #yearInput formControlName="schoolYear" [data]="years" />
            <kendo-formerror>{{ serverError(form.controls.schoolYear) ?? 'Năm học có dạng 2026-2027.' }}</kendo-formerror>
          </kendo-formfield>

          <div class="defaults">
            <span class="k-label">Danh mục mặc định</span>
            <div class="tags">
              @for (category of defaults; track category.name) {
                <span class="cf-tag" [class.cf-tag--success]="category.type === 'Income'" [class.cf-tag--danger]="category.type === 'Expense'">
                  {{ category.name }}
                </span>
              }
            </div>
            <span class="k-form-hint">Sửa được sau ở mục Danh mục.</span>
          </div>

          <div class="actions">
            <button kendoButton type="button" (click)="cancel()">Hủy</button>
            <button kendoButton themeColor="primary" type="submit" [disabled]="saving()">
              {{ saving() ? 'Đang tạo…' : 'Tạo quỹ' }}
            </button>
          </div>
        </form>

        <div class="cf-alert next">
          Bước tiếp theo: thêm học sinh bằng email ở mục <b>&nbsp;Học sinh</b>, rồi tạo khoản thu chung đầu tiên.
        </div>
      }
    </main>
  `,
  styles: `
    :host {
      display: block;
      min-height: 100dvh;
      background: var(--cf-background);
    }
    .page {
      box-sizing: border-box;
      max-width: 640px;
      margin: 0 auto;
      padding: 32px 24px 48px;
    }
    .back {
      display: inline-block;
      margin-bottom: 8px;
      font-size: 13px;
      text-decoration: none;
    }
    .form {
      display: flex;
      flex-direction: column;
      gap: 16px;
      padding: 24px;
    }
    kendo-formfield {
      margin: 0;
    }
    .defaults {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .tags {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
    }
    .next {
      margin-top: 16px;
    }
  `,
})
export class FundNew {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(FundsApi);
  private readonly router = inject(Router);
  private readonly notifications = inject(AppNotifications);

  protected readonly nameMax = FUND_NAME_MAX;
  protected readonly years = schoolYearOptions();
  protected readonly defaults = DEFAULT_CATEGORIES;
  protected readonly serverError = serverError;
  protected readonly form = createFundForm({ name: '', schoolYear: currentSchoolYear() });
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected cancel(): void {
    void this.router.navigateByUrl('/funds');
  }

  protected async submit(): Promise<void> {
    this.formError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    try {
      const fund = await firstValueFrom(this.api.create(fundBody(this.form)));
      // Cập nhật memberships để menu / guard nhận quỹ mới.
      await this.auth.restoreSession();
      this.notifications.success(`Đã tạo quỹ ${fund.name}.`);
      await this.router.navigate(['/funds', fund.id]);
    } catch (error) {
      this.formError.set(applyServerErrors(this.form, error));
    } finally {
      this.saving.set(false);
    }
  }
}
