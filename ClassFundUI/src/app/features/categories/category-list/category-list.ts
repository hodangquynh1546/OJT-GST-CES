import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { ErrorComponent, FormFieldComponent, TextBoxComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { BehaviorSubject, combineLatest, firstValueFrom, map, switchMap } from 'rxjs';

import { ConfirmDialog } from '../../../core/services/confirm-dialog.service';
import { AppNotifications } from '../../../core/services/notification.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { ApiError } from '../../../shared/models/api-error';
import { LOADING, stateKind, toLoadState } from '../../../shared/state/load-state';
import { CategoriesApi } from '../categories.api';
import '../categories.errors';
import {
  CATEGORY_NAME_MAX,
  Category,
  LEDGER_TYPE_LABEL,
  LedgerType,
  categoryMeta,
  normalizeCategoryName,
} from '../categories.models';

const notBlank = (control: AbstractControl<string | null>): ValidationErrors | null =>
  (control.value ?? '').trim() ? null : { required: true };

/** C-01 · Danh mục thu chi (FR-14): thêm, đổi tên, ngừng dùng / dùng lại. Không xóa. */
@Component({
  selector: 'app-category-list',
  imports: [
    AppState,
    ButtonComponent,
    ErrorComponent,
    FormFieldComponent,
    LabelComponent,
    PageHeader,
    ReactiveFormsModule,
    TextBoxComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-page">
      <app-page-header
        title="Danh mục thu chi"
        subtitle="Thêm, đổi tên, ngừng dùng. Danh mục đã có giao dịch không xóa được, chỉ ngừng dùng."
      />

      <form class="cf-card add" [formGroup]="addForm" (ngSubmit)="add()" novalidate>
        <kendo-formfield class="name-field">
          <kendo-label [for]="nameInput" text="Tên danh mục" />
          <kendo-textbox #nameInput formControlName="name" [maxlength]="nameMax" placeholder="VD: Văn nghệ" />
          <kendo-formerror>{{ serverError(addForm.controls.name) ?? 'Vui lòng nhập tên danh mục.' }}</kendo-formerror>
        </kendo-formfield>
        <div class="type">
          <span class="k-label" id="type-label">Loại</span>
          <div class="segmented" role="radiogroup" aria-labelledby="type-label">
            @for (type of types; track type) {
              <button
                kendoButton
                type="button"
                role="radio"
                [attr.aria-checked]="addForm.controls.type.value === type"
                [themeColor]="addForm.controls.type.value === type ? 'primary' : 'base'"
                (click)="addForm.controls.type.setValue(type)"
              >
                {{ typeLabel[type] }}
              </button>
            }
          </div>
        </div>
        <button kendoButton themeColor="primary" type="submit" [disabled]="busy()">Thêm danh mục</button>
      </form>

      @let current = state();
      @if (kind(); as stateKindValue) {
        <app-state [kind]="stateKindValue" />
      } @else if (current.status === 'ready') {
        <div class="columns">
          @for (type of types; track type) {
            <section class="cf-card list" [attr.aria-label]="'Danh mục ' + typeLabel[type].toLowerCase()">
              <header>
                <h2>Danh mục {{ typeLabel[type].toLowerCase() }}</h2>
                <span class="cf-tag" [class.cf-tag--success]="type === 'Income'" [class.cf-tag--danger]="type === 'Expense'">{{ typeLabel[type] }}</span>
              </header>
              @for (category of byType()[type]; track category.id) {
                <div class="row" [attr.data-category]="category.name">
                  @if (editingId() === category.id) {
                    <kendo-textbox
                      class="rename"
                      [formControl]="renameControl"
                      [maxlength]="nameMax"
                      [inputAttributes]="{ 'aria-label': 'Tên mới cho ' + category.name }"
                      (keydown.enter)="$event.preventDefault(); saveRename(category)"
                      (keydown.escape)="cancelRename()"
                    />
                    <button kendoButton size="small" themeColor="primary" type="button" [disabled]="busy()" (click)="saveRename(category)">Lưu</button>
                    <button kendoButton size="small" fillMode="flat" type="button" (click)="cancelRename()">Hủy</button>
                  } @else {
                    <div class="main">
                      <span class="name" [class.inactive]="!category.isActive">{{ category.name }}</span>
                      <span class="cf-muted xs">{{ meta(category) }}</span>
                    </div>
                    <span class="cf-tag" [class.cf-tag--success]="category.isActive">{{ category.isActive ? 'Đang dùng' : 'Ngừng dùng' }}</span>
                    <button kendoButton size="small" type="button" [disabled]="busy()" (click)="startRename(category)">Đổi tên</button>
                    <button kendoButton size="small" fillMode="flat" type="button" [disabled]="busy()" (click)="toggle(category)">
                      {{ category.isActive ? 'Ngừng dùng' : 'Dùng lại' }}
                    </button>
                  }
                </div>
              } @empty {
                <p class="cf-muted empty">Chưa có danh mục {{ typeLabel[type].toLowerCase() }}.</p>
              }
            </section>
          }
        </div>
      }
    </div>
  `,
  styles: `
    .add {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: 12px;
      margin-bottom: 16px;
      padding: 16px;
    }
    .add > button {
      margin-top: 24px;
    }
    .name-field {
      flex: 2 1 240px;
      margin: 0;
    }
    .type {
      display: flex;
      flex: 1 1 180px;
      flex-direction: column;
      gap: 4px;
    }
    .segmented {
      display: flex;
    }
    .segmented button {
      flex: 1;
    }
    .segmented button:first-child {
      border-top-right-radius: 0;
      border-bottom-right-radius: 0;
    }
    .segmented button:last-child {
      border-top-left-radius: 0;
      border-bottom-left-radius: 0;
    }
    .columns {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(360px, 1fr));
      gap: 16px;
    }
    .list header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 16px;
      border-bottom: 1px solid var(--cf-border);
    }
    h2 {
      margin: 0;
      font-size: 16px;
    }
    .row {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 16px;
      border-bottom: 1px solid var(--cf-subtle);
    }
    .row:last-child {
      border-bottom: none;
    }
    .main {
      display: flex;
      flex: 1;
      flex-direction: column;
      min-width: 0;
    }
    .name {
      font-weight: 500;
    }
    .inactive {
      color: var(--cf-text-muted);
      text-decoration: line-through;
    }
    .rename {
      flex: 1;
    }
    .xs {
      font-size: 12px;
    }
    .empty {
      margin: 0;
      padding: 16px;
    }  `,
})
export class CategoryList {
  private readonly api = inject(CategoriesApi);
  private readonly route = inject(ActivatedRoute);
  private readonly notifications = inject(AppNotifications);
  private readonly confirmDialog = inject(ConfirmDialog);
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  protected readonly types: readonly LedgerType[] = ['Income', 'Expense'];
  protected readonly typeLabel = LEDGER_TYPE_LABEL;
  protected readonly nameMax = CATEGORY_NAME_MAX;
  protected readonly meta = categoryMeta;
  protected readonly serverError = serverError;

  protected readonly addForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [notBlank, Validators.maxLength(CATEGORY_NAME_MAX)] }),
    type: new FormControl<LedgerType>('Expense', { nonNullable: true }),
  });
  protected readonly renameControl = new FormControl('', { nonNullable: true });
  protected readonly editingId = signal<string | null>(null);
  protected readonly busy = signal(false);

  private readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), {
    initialValue: '',
  });

  protected readonly state = toSignal(
    combineLatest([this.route.paramMap, this.reload$]).pipe(
      switchMap(([params]) => toLoadState(this.api.list(params.get('fundId') ?? ''))),
    ),
    { initialValue: LOADING },
  );

  protected readonly kind = computed(() => stateKind<Category[]>(this.state(), () => false));

  protected readonly byType = computed<Record<LedgerType, Category[]>>(() => {
    const current = this.state();
    const items = current.status === 'ready' ? current.data : [];
    return {
      Income: items.filter((item) => item.type === 'Income'),
      Expense: items.filter((item) => item.type === 'Expense'),
    };
  });

  protected async add(): Promise<void> {
    if (this.addForm.invalid) {
      this.addForm.markAllAsTouched();
      return;
    }
    const { name, type } = this.addForm.getRawValue();
    await this.run(async () => {
      try {
        const created = await firstValueFrom(this.api.create(this.fundId(), { name: normalizeCategoryName(name), type }));
        this.addForm.reset({ name: '', type });
        this.notifications.success(`Đã thêm danh mục ${created.name}.`);
        this.reload$.next();
      } catch (error) {
        const message = applyServerErrors(this.addForm, error, { 'category.duplicate_name': 'name' });
        if (message) {
          this.notifications.error(message);
        }
      }
    });
  }

  protected startRename(category: Category): void {
    this.renameControl.setValue(category.name);
    this.editingId.set(category.id);
  }

  protected cancelRename(): void {
    this.editingId.set(null);
  }

  protected async saveRename(category: Category): Promise<void> {
    const name = normalizeCategoryName(this.renameControl.value);
    if (!name || name === category.name) {
      this.cancelRename();
      return;
    }
    await this.update(category, { name, isActive: category.isActive }, 'Đã đổi tên danh mục.');
  }

  /** Ngừng dùng cần xác nhận; dùng lại thì làm ngay. */
  protected async toggle(category: Category): Promise<void> {
    if (!category.isActive) {
      await this.update(category, { name: category.name, isActive: true }, 'Đã dùng lại danh mục.');
      return;
    }
    const confirmed = await firstValueFrom(
      this.confirmDialog.confirm({
        title: 'Ngừng dùng danh mục?',
        message: `"${category.name}" sẽ không còn trong ô chọn khi ghi giao dịch mới. Giao dịch cũ giữ nguyên; bạn có thể dùng lại sau.`,
        confirmLabel: 'Ngừng dùng',
      }),
    );
    if (confirmed) {
      await this.update(category, { name: category.name, isActive: false }, 'Đã ngừng dùng danh mục.');
    }
  }

  private async update(category: Category, body: { name: string; isActive: boolean }, success: string): Promise<void> {
    await this.run(async () => {
      try {
        await firstValueFrom(this.api.update(this.fundId(), category.id, body));
        this.editingId.set(null);
        this.notifications.success(success);
        this.reload$.next();
      } catch (error) {
        this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
      }
    });
  }

  private async run(work: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await work();
    } finally {
      this.busy.set(false);
    }
  }
}
