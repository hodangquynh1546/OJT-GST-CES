import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { LoaderComponent } from '@progress/kendo-angular-indicators';
import { ErrorComponent, FormFieldComponent, HintComponent, TextBoxComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { finalize, map } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { AppNotifications } from '../../../core/services/notification.service';
import { ApiError } from '../../../shared/models/api-error';
import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { VnDatePipe } from '../../../shared/pipes/vn-date.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { FUND_NAME_ERROR, fundNameValidator } from '../fund-name.validator';
import { FundsApi } from '../funds.api';
import { FUND_NAME_MAX_LENGTH, FundSummary } from '../funds.models';

/**
 * G-04 "Thông tin quỹ" – Canvas board `FundSettingsTeacher`; FR-11. Thông tin lấy từ mục của quỹ đang mở trong
 * `GET /funds` (screen-specifications.md); đổi tên bằng `PUT /funds/{fundId}` (Fund.Manage – guard ở app.routes.ts).
 */
@Component({
  selector: 'app-fund-settings',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonComponent,
    ErrorComponent,
    FormFieldComponent,
    HintComponent,
    LabelComponent,
    LoaderComponent,
    TextBoxComponent,
    AppState,
    PageHeader,
    VnDatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <app-page-header title="Thông tin quỹ" subtitle="Chỉ GVCN đổi được tên quỹ. Thủ quỹ không sửa thông tin quỹ." />

      @if (fund(); as fund) {
        <form class="cf-card card" aria-label="Thông tin quỹ" [formGroup]="form" (ngSubmit)="save()" novalidate>
          @if (summary(); as message) {
            <div class="cf-alert cf-alert--danger" role="alert">{{ message }}</div>
          }
          <kendo-formfield showHints="always" showErrors="always">
            <kendo-label [for]="name" text="Tên quỹ *" />
            <kendo-textbox
              #name
              formControlName="name"
              [maxlength]="maxLength"
              [readonly]="saving()"
              aria-describedby="fund-name-help"
            />
            @if (invalid()) {
              <kendo-formerror id="fund-name-help">{{ nameError() }}</kendo-formerror>
            } @else {
              <kendo-formhint id="fund-name-help">
                Hiển thị cho mọi thành viên, kể cả phụ huynh. Tối đa {{ maxLength }} ký tự.
              </kendo-formhint>
            }
          </kendo-formfield>

          <dl class="facts">
            <dt class="cf-muted">Ngày tạo</dt>
            <dd>{{ fund.createdAt | vnDate }}</dd>
            <dt class="cf-muted">GVCN</dt>
            <dd>{{ teacherName() }} (bạn)</dd>
            <dt class="cf-muted">Thủ quỹ</dt>
            <dd>{{ fund.treasurerNames.length > 0 ? fund.treasurerNames.join(', ') : '—' }}</dd>
            <dt class="cf-muted">Học sinh</dt>
            <dd>
              {{ fund.studentCount ?? 0 }} học sinh –
              <a [routerLink]="['/funds', fund.id, 'students']">Học sinh &amp; phụ huynh</a> ·
              <a [routerLink]="['/funds', fund.id, 'treasurers']">Phân công thủ quỹ</a>
            </dd>
            <dt class="cf-muted">Danh mục</dt>
            <dd>Do thủ quỹ quản lý (GVCN chỉ xem)</dd>
          </dl>

          <div class="actions">
            <button kendoButton themeColor="primary" type="submit" [disabled]="saving()" [attr.aria-busy]="saving()">
              @if (saving()) {
                <kendo-loader size="small" type="infinite-spinner" themeColor="light" />
                Đang lưu…
              } @else {
                Lưu
              }
            </button>
            <button kendoButton type="button" [disabled]="saving()" (click)="undo()">Hoàn tác</button>
          </div>
        </form>
      } @else {
        @switch (kind()) {
          @case ('error') {
            <app-state class="cf-card" kind="error" actionLabel="Thử lại" (action)="load()" />
          }
          @case ('loading') {
            <app-state kind="loading" />
          }
          @default {
            <app-state [kind]="kind() === 'forbidden' ? 'forbidden' : 'not-found'" />
          }
        }
      }
    </section>
  `,
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      max-width: 1100px;
    }
    app-page-header {
      margin-bottom: 16px;
    }
    .card {
      display: flex;
      flex-direction: column;
      gap: 10px;
      max-width: 640px;
    }
    kendo-formfield {
      margin: 0;
    }
    .facts {
      display: grid;
      grid-template-columns: 140px minmax(0, 1fr);
      gap: 8px 12px;
      margin: 0;
    }
    dd {
      margin: 0;
    }
    .actions {
      display: flex;
      gap: 8px;
    }
    kendo-loader {
      margin-right: 6px;
    }
  `,
})
export class FundSettings {
  private readonly api = inject(FundsApi);
  private readonly auth = inject(AuthService);
  private readonly notifications = inject(AppNotifications);
  private readonly destroyRef = inject(DestroyRef);

  /** `:fundId` trên URL (component input binding, kế thừa tham số route cha). */
  readonly fundId = input.required<string>();

  protected readonly maxLength = FUND_NAME_MAX_LENGTH;
  protected readonly state = signal<LoadState<FundSummary | null>>(LOADING);
  protected readonly saving = signal(false);
  protected readonly invalid = signal(false);
  protected readonly nameError = signal(FUND_NAME_ERROR);
  protected readonly summary = signal<string | null>(null);
  protected readonly form = new FormGroup(
    { name: new FormControl('', { nonNullable: true, validators: fundNameValidator }) },
    { updateOn: 'submit' },
  );

  protected readonly kind = computed(() => stateKind(this.state(), (fund) => fund === null));
  protected readonly fund = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data : null;
  });
  protected readonly teacherName = computed(() => this.auth.user()?.fullName ?? '');

  constructor() {
    effect(() => {
      this.fundId();
      untracked(() => this.load());
    });
  }

  protected load(): void {
    const fundId = this.fundId();
    toLoadState(this.api.list().pipe(map((funds) => funds.find((fund) => fund.id === fundId) ?? null)))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((state) => {
        this.state.set(state);
        if (state.status === 'ready' && state.data) {
          this.resetForm(state.data.name);
        }
      });
  }

  protected save(): void {
    const fund = this.fund();
    if (!fund || this.saving()) {
      return;
    }
    this.summary.set(null);
    const control = this.form.controls.name;
    if (control.invalid) {
      this.showError(FUND_NAME_ERROR);
      return;
    }

    const name = control.value.trim();
    this.invalid.set(false);
    this.saving.set(true);
    this.api
      .rename(fund.id, name)
      .pipe(
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: async () => {
          this.state.set({ status: 'ready', data: { ...fund, name } });
          this.resetForm(name);
          this.notifications.success('Đã đổi tên quỹ.');
          // Header (tên quỹ) và "Đổi quỹ" lấy từ /auth/me → đọc lại để hiện tên mới.
          await this.auth.restoreSession();
        },
        error: (error: unknown) => {
          if (error instanceof ApiError && (error.status === 403 || error.status === 404)) {
            this.state.set({ status: 'error', error });
            return;
          }
          const message = applyServerErrors(this.form, error);
          const fieldMessage = serverError(control);
          if (fieldMessage) {
            this.showError(fieldMessage);
          }
          this.summary.set(message);
        },
      });
  }

  /** Trả ô tên về tên đang lưu, bỏ lỗi. */
  protected undo(): void {
    const fund = this.fund();
    if (fund) {
      this.resetForm(fund.name);
    }
  }

  private resetForm(name: string): void {
    this.form.reset({ name });
    this.invalid.set(false);
    this.summary.set(null);
  }

  private showError(message: string): void {
    this.nameError.set(message);
    this.form.controls.name.markAsTouched();
    this.invalid.set(true);
  }
}
