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
    ReactiveFormsModule,
    TextBoxComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="head-t">
        <h1>Danh mục thu chi</h1>
        <p class="muted">Thêm, đổi tên, ngừng dùng. Danh mục đã có giao dịch không xóa được, chỉ ngừng dùng.</p>
      </div>

      <form class="card add" [formGroup]="addForm" (ngSubmit)="add()" novalidate>
        <kendo-formfield class="field name-field">
          <kendo-label class="label" [for]="nameInput" text="Tên danh mục" />
          <kendo-textbox #nameInput class="input" formControlName="name" [maxlength]="nameMax" placeholder="VD: Văn nghệ" />
          <kendo-formerror>{{ serverError(addForm.controls.name) ?? 'Vui lòng nhập tên danh mục.' }}</kendo-formerror>
        </kendo-formfield>
        <div class="field type-field">
          <span class="label" id="type-label">Loại</span>
          <div class="tabs" role="radiogroup" aria-labelledby="type-label">
            @for (type of types; track type) {
              <button
                type="button"
                role="radio"
                class="tab"
                [class.on]="addForm.controls.type.value === type"
                [attr.aria-checked]="addForm.controls.type.value === type"
                (click)="addForm.controls.type.setValue(type)"
              >
                {{ typeLabel[type] }}
              </button>
            }
          </div>
        </div>
        <button kendoButton class="btn btn-p" type="submit" [disabled]="busy()">Thêm danh mục</button>
      </form>

      @let current = state();
      @if (kind(); as stateKindValue) {
        <app-state [kind]="stateKindValue" />
      } @else if (current.status === 'ready') {
        <div class="grid2">
          @for (type of types; track type) {
            <section class="card" [attr.aria-label]="'Danh mục ' + typeLabel[type].toLowerCase()">
              <header class="card-h">
                <h2>Danh mục {{ typeLabel[type].toLowerCase() }}</h2>
                <span class="badge" [class.b-ok]="type === 'Income'" [class.b-bad]="type === 'Expense'">{{ typeLabel[type] }}</span>
              </header>
              @for (category of byType()[type]; track category.id) {
                <div class="row" [attr.data-category]="category.name">
                  @if (editingId() === category.id) {
                    <kendo-textbox
                      class="input rename"
                      [formControl]="renameControl"
                      [maxlength]="nameMax"
                      [inputAttributes]="{ 'aria-label': 'Tên mới cho ' + category.name }"
                      (keydown.enter)="$event.preventDefault(); saveRename(category)"
                      (keydown.escape)="cancelRename()"
                    />
                    <button kendoButton class="btn btn-p btn-s" type="button" [disabled]="busy()" (click)="saveRename(category)">Lưu</button>
                    <button kendoButton class="btn btn-g btn-s" type="button" (click)="cancelRename()">Hủy</button>
                  } @else {
                    <div class="row-main">
                      <span class="name" [class.strike]="!category.isActive">{{ category.name }}</span>
                      <span class="muted xs">{{ meta(category) }}</span>
                    </div>
                    <span class="badge" [class.b-ok]="category.isActive" [class.b-gen]="!category.isActive">
                      {{ category.isActive ? 'Đang dùng' : 'Ngừng dùng' }}
                    </span>
                    <button kendoButton class="btn btn-o btn-s" type="button" [disabled]="busy()" (click)="startRename(category)">Đổi tên</button>
                    <button kendoButton class="btn btn-g btn-s" type="button" [disabled]="busy()" (click)="toggle(category)">
                      {{ category.isActive ? 'Ngừng dùng' : 'Dùng lại' }}
                    </button>
                  }
                </div>
              } @empty {
                <p class="muted empty">Chưa có danh mục {{ typeLabel[type].toLowerCase() }}.</p>
              }
            </section>
          }
        </div>
      }
    </div>
  `,
  // Theo đúng canvas C-01 (Categories.dc.html + cf.css): kích thước, bo góc, khoảng cách lấy từ canvas; màu dùng token --cf-*.
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 24px;
      max-width: var(--cf-content-max);
    }
    .head-t {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    h1 {
      margin: 0;
      font-size: 24px;
      font-weight: 600;
      line-height: 32px;
      letter-spacing: -0.02em;
    }
    h2 {
      margin: 0;
      font-size: 16px;
      font-weight: 600;
      line-height: 24px;
    }
    p {
      margin: 0;
    }
    .muted {
      color: var(--cf-text-muted);
    }
    .xs {
      font-size: 12px;
    }
    .card {
      border: 1px solid var(--cf-border);
      border-radius: 12px;
      background: var(--cf-surface);
      box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
    }
    .add {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: 12px;
      padding: 20px;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin: 0;
    }
    .name-field {
      flex: 2 1 240px;
    }
    .type-field {
      flex: 1 1 180px;
    }
    .label {
      font-weight: 500;
    }
    /* Nhãn của kendo-label nằm trong component con: chỉnh cỡ chữ / lề cho khớp canvas, chỉ trong màn này. */
    .add ::ng-deep .k-label {
      margin: 0;
      font-size: 14px;
      font-weight: 500;
    }
    kendo-textbox.input {
      min-height: 0;
      height: 36px;
      border-color: var(--cf-border);
      border-radius: var(--cf-radius-lg);
    }
    .tabs {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 4px;
      padding: 4px;
      border-radius: var(--cf-radius-lg);
      background: var(--cf-subtle);
    }
    .tab {
      display: inline-flex;
      align-items: center;
      height: 28px;
      padding: 0 12px;
      border: 0;
      border-radius: var(--cf-radius-md);
      background: transparent;
      color: var(--cf-text-muted);
      font: inherit;
      font-size: 13px;
      font-weight: 500;
      cursor: pointer;
    }
    .tab.on {
      background: var(--cf-surface);
      color: var(--cf-ink);
      box-shadow: 0 1px 2px rgba(0, 0, 0, 0.08);
    }
    /* Nút theo canvas (.btn, .btn-p, .btn-o, .btn-g, .btn-s) – vẫn là kendoButton. */
    .btn.k-button {
      min-height: 0;
      height: 36px;
      padding: 0 14px;
      border: 1px solid transparent;
      border-radius: var(--cf-radius-lg);
      font-size: 14px;
      font-weight: 500;
      white-space: nowrap;
      box-shadow: none;
    }
    .btn.btn-s.k-button {
      height: 32px;
      padding: 0 10px;
      font-size: 13px;
    }
    .btn-p.k-button {
      background: var(--cf-ink);
      color: var(--cf-surface);
    }
    .btn-p.k-button:hover {
      background: var(--cf-ink-hover);
    }
    .btn-p.k-button:disabled {
      border-color: var(--cf-ink-soft);
      background: var(--cf-ink-soft);
    }
    .btn-o.k-button {
      border-color: var(--cf-border);
      background: var(--cf-surface);
      color: var(--cf-ink);
    }
    .btn-o.k-button:hover,
    .btn-g.k-button:hover {
      background: var(--cf-subtle);
    }
    .btn-g.k-button {
      background: transparent;
      color: var(--cf-ink);
    }
    .grid2 {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      align-items: start;
      gap: 16px;
    }
    .card-h {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 16px;
    }
    .badge {
      display: inline-flex;
      align-items: center;
      height: 22px;
      padding: 0 8px;
      border: 1px solid var(--cf-border);
      border-radius: 999px;
      background: var(--cf-surface);
      color: var(--cf-ink);
      font-size: 12px;
      font-weight: 500;
      white-space: nowrap;
    }
    .b-ok {
      border-color: color-mix(in srgb, var(--cf-success-fg) 25%, transparent);
      background: var(--cf-success-bg);
      color: var(--cf-success-fg);
    }
    .b-bad {
      border-color: color-mix(in srgb, var(--cf-danger-fg) 22%, transparent);
      background: var(--cf-danger-bg);
      color: var(--cf-danger-fg);
    }
    .b-gen {
      border-color: transparent;
      background: var(--cf-subtle);
      color: var(--cf-text);
    }
    .row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 16px;
      padding: 14px 16px;
      border-bottom: 1px solid var(--cf-subtle);
    }
    .row:last-child {
      border-bottom: 0;
    }
    .row-main {
      display: flex;
      flex: 1 1 280px;
      flex-direction: column;
      gap: 4px;
      min-width: 0;
    }
    .name {
      font-weight: 500;
    }
    .strike {
      color: var(--cf-text-muted);
      text-decoration: line-through;
    }
    .rename {
      flex: 1 1 220px;
    }
    .empty {
      padding: 16px;
    }
  `,
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
