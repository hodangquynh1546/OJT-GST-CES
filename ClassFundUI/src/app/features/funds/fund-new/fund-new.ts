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
import { TopBar } from '../../account/top-bar';
import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { FundsApi } from '../funds.api';
import '../funds.errors';
import { FUND_NAME_MAX, createFundForm, fundBody } from '../fund-form';
import { DEFAULT_CATEGORIES, currentSchoolYear, displaySchoolYear, schoolYearOptions } from '../funds.models';

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
    ReactiveFormsModule,
    RouterLink,
    TextBoxComponent,
    TopBar,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-top-bar variant="funds" />
    <main class="page">
      <div class="head-t">
        <a routerLink="/funds" class="back">← Quỹ của tôi</a>
        <h1>Tạo quỹ</h1>
        <p class="cf-muted">Bạn sẽ là thủ quỹ chính. Danh mục thu chi mặc định được tạo sẵn.</p>
      </div>

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

          <kendo-formfield class="cf-required">
            <kendo-label [for]="nameInput" text="Tên quỹ" />
            <kendo-textbox #nameInput formControlName="name" [maxlength]="nameMax" placeholder="VD: Lớp 10A1" />
            <kendo-formhint>VD: Lớp 10A1, CLB Tiếng Anh</kendo-formhint>
            <kendo-formerror>{{ serverError(form.controls.name) ?? 'Vui lòng nhập tên quỹ (tối đa 100 ký tự).' }}</kendo-formerror>
          </kendo-formfield>

          <kendo-formfield class="cf-required">
            <kendo-label [for]="yearInput" text="Năm học" />
            <kendo-dropdownlist
              #yearInput
              formControlName="schoolYear"
              [data]="years"
              textField="text"
              valueField="value"
              [valuePrimitive]="true"
            />
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
            <button kendoButton fillMode="outline" type="button" (click)="cancel()">Hủy</button>
            <button kendoButton themeColor="primary" type="submit" [disabled]="saving()">
              {{ saving() ? 'Đang tạo…' : 'Tạo quỹ' }}
            </button>
          </div>
        </form>

        <div class="cf-alert">
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
    /* Theo canvas F-02 (FundNew.dc.html): trang 640 px giữa màn, khoảng cách 24 px, thẻ đệm 20 px. */
    .page {
      display: flex;
      flex-direction: column;
      gap: 24px;
      box-sizing: border-box;
      max-width: 640px;
      margin: 0 auto;
      padding: 24px;
    }
    .head-t {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    h1 {
      font-weight: 600;
      letter-spacing: -0.02em;
    }
    p {
      margin: 0;
    }
    .back {
      color: var(--cf-text-muted);
      font-size: 13px;
      text-decoration: none;
    }
    .form {
      display: flex;
      flex-direction: column;
      gap: 16px;
      padding: 20px;
    }
    kendo-formfield {
      margin: 0;
    }
    .defaults {
      display: flex;
      flex-direction: column;
      gap: 8px;
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
  `,
})
export class FundNew {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(FundsApi);
  private readonly router = inject(Router);
  private readonly notifications = inject(AppNotifications);

  protected readonly nameMax = FUND_NAME_MAX;
  /** Giá trị gửi API "2026-2027", hiển thị "2026–2027" như canvas. */
  protected readonly years = schoolYearOptions().map((value) => ({ value, text: displaySchoolYear(value) }));
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
