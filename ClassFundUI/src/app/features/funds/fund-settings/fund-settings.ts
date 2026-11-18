import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { ErrorComponent, FormFieldComponent, TextBoxComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { firstValueFrom, map, switchMap, tap } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { AppNotifications } from '../../../core/services/notification.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { VnDatePipe } from '../../../shared/pipes/vn-date.pipe';
import { LOADING, stateKind, toLoadState } from '../../../shared/state/load-state';
import { FUND_NAME_MAX, createFundForm, fundBody } from '../fund-form';
import { FundsApi } from '../funds.api';
import '../funds.errors';
import { FUND_STATUS_LABEL, FundDetail, displaySchoolYear } from '../funds.models';

/** F-03 · Thông tin quỹ (FR-06, FR-07). Năm học chỉ xem theo bản thiết kế. */
@Component({
  selector: 'app-fund-settings',
  imports: [
    AppState,
    ButtonComponent,
    ErrorComponent,
    FormFieldComponent,
    LabelComponent,
    PageHeader,
    ReactiveFormsModule,
    TextBoxComponent,
    VnDatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-page settings">
      <app-page-header title="Thông tin quỹ" subtitle="Thông tin công khai mọi thành viên xem được." />

      @let current = state();
      @if (kind(); as stateKindValue) {
        <app-state [kind]="stateKindValue" />
      } @else if (current.status === 'ready') {
        @let fund = current.data;
        @if (archived()) {
          <div class="cf-alert cf-alert--warning" role="status">Quỹ đã tổng kết cuối năm – chỉ xem, không sửa được.</div>
        }
        <form class="cf-card section" [formGroup]="form" (ngSubmit)="save(fund.id)" novalidate>
          @if (formError(); as message) {
            <div class="cf-alert cf-alert--danger" role="alert">{{ message }}</div>
          }
          <kendo-formfield>
            <kendo-label [for]="nameInput" text="Tên quỹ" />
            <kendo-textbox #nameInput formControlName="name" [maxlength]="nameMax" />
            <kendo-formerror>{{ serverError(form.controls.name) ?? 'Vui lòng nhập tên quỹ (tối đa 100 ký tự).' }}</kendo-formerror>
          </kendo-formfield>
          <div class="row">
            <!-- Chỉ xem (không phải form control) nên không đặt trong kendo-formfield. -->
            <div class="field stretch">
              <kendo-label [for]="yearInput" text="Năm học" />
              <kendo-textbox #yearInput [value]="year(fund.schoolYear)" [readonly]="true" [disabled]="true" />
            </div>
            <div class="field">
              <span class="k-label">Trạng thái</span>
              <span class="cf-tag" [class.cf-tag--success]="fund.status === 'Active'">{{ statusLabel[fund.status] }}</span>
            </div>
          </div>
          <dl class="meta">
            <div><dt>Thủ quỹ chính</dt><dd>{{ fund.ownerName }}</dd></div>
            <div><dt>Thành viên</dt><dd>{{ fund.memberCount }}</dd></div>
            <div><dt>Ngày tạo</dt><dd>{{ fund.createdAt | vnDate }}</dd></div>
          </dl>
          @if (!archived()) {
            <div class="actions">
              <button kendoButton themeColor="primary" type="submit" [disabled]="saving() || form.pristine">
                {{ saving() ? 'Đang lưu…' : 'Lưu thay đổi' }}
              </button>
            </div>
          }
        </form>

        <section class="cf-card section">
          <h2>Tổng kết cuối năm</h2>
          <p class="cf-muted">
            Quyết toán quỹ chung một lần duy nhất khi mọi sự kiện đã quyết toán hoặc hủy. Sau đó quỹ chuyển "Đã tổng kết" – chỉ xem.
          </p>
          <div>
            <button kendoButton type="button" (click)="goYearEnd(fund.id)">Đi tới tổng kết cuối năm</button>
          </div>
        </section>
      }
    </div>
  `,
  styles: `
    .settings {
      max-width: 860px;
    }
    .section {
      display: flex;
      flex-direction: column;
      gap: 16px;
      margin-bottom: 16px;
      padding: 24px;
    }
    h2 {
      margin: 0;
      font-size: 16px;
    }
    p {
      margin: 0;
    }
    kendo-formfield {
      margin: 0;
    }
    .row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 16px;
    }
    .field {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 6px;
    }
    .field.stretch {
      align-items: stretch;
    }
    .meta {
      display: flex;
      flex-wrap: wrap;
      gap: 24px;
      margin: 0;
    }
    .meta dt {
      color: var(--cf-text-muted);
      font-size: 12px;
    }
    .meta dd {
      margin: 0;
      font-weight: 500;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
    }
    .cf-alert {
      margin-bottom: 16px;
    }
  `,
})
export class FundSettings {
  private readonly api = inject(FundsApi);
  private readonly auth = inject(AuthService);
  private readonly notifications = inject(AppNotifications);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly nameMax = FUND_NAME_MAX;
  protected readonly statusLabel = FUND_STATUS_LABEL;
  protected readonly year = displaySchoolYear;
  protected readonly serverError = serverError;
  protected readonly form = createFundForm({ name: '', schoolYear: '' });
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly state = toSignal(
    this.route.paramMap.pipe(
      // paramsInheritanceStrategy = 'always' nên route con đọc được :fundId.
      map((params) => params.get('fundId') ?? ''),
      switchMap((fundId) => toLoadState(this.api.get(fundId).pipe(tap((fund) => this.fill(fund))))),
    ),
    { initialValue: LOADING },
  );

  protected readonly kind = computed(() => stateKind<FundDetail>(this.state(), () => false));
  protected readonly archived = computed(() => {
    const current = this.state();
    return current.status === 'ready' && current.data.status === 'Archived';
  });

  protected goYearEnd(fundId: string): void {
    void this.router.navigate(['/funds', fundId, 'reports', 'year-end']);
  }

  protected async save(fundId: string): Promise<void> {
    this.formError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    try {
      const fund = await firstValueFrom(this.api.update(fundId, fundBody(this.form)));
      this.fill(fund);
      // Tên quỹ hiện ở header / menu lấy từ phiên.
      await this.auth.restoreSession();
      this.notifications.success('Đã lưu thông tin quỹ.');
    } catch (error) {
      this.formError.set(applyServerErrors(this.form, error));
    } finally {
      this.saving.set(false);
    }
  }

  private fill(fund: FundDetail): void {
    this.form.reset({ name: fund.name, schoolYear: fund.schoolYear });
    if (fund.status === 'Archived') {
      this.form.disable();
    }
  }
}
