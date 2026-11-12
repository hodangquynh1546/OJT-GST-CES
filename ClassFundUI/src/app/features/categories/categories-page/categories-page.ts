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
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { TextBoxDirective } from '@progress/kendo-angular-inputs';
import { Observable, filter, finalize } from 'rxjs';

import { AppNotifications } from '../../../core/services/notification.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { ApiError } from '../../../shared/models/api-error';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { CategoriesApi } from '../categories.api';
import { CATEGORY_NAME_MAX_LENGTH, Category, CategoryType } from '../categories.models';
import { categoryNameError, categoryNameTaken } from '../categories.validation';

interface Group {
  type: CategoryType;
  title: string;
  items: Category[];
  count: string;
}

const GROUPS: readonly { type: CategoryType; title: string }[] = [
  { type: 'Income', title: 'Danh mục thu' },
  { type: 'Expense', title: 'Danh mục chi' },
];

/**
 * T-02 "Danh mục thu chi" – Canvas board `FundSettings` (thủ quỹ); FR-10, BR-08.
 * Ẩn thay cho xóa; tên không trùng trong cùng loại, kể cả danh mục đã ẩn. Chỉ thủ quỹ vào được (guard `Category.Manage`).
 */
@Component({
  selector: 'app-categories-page',
  imports: [ButtonComponent, TextBoxDirective, AppState, PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <app-page-header
        title="Danh mục thu chi"
        subtitle="Chỉ thủ quỹ quản lý danh mục. GVCN và phụ huynh chỉ xem."
      />

      @if (categories(); as all) {
        <span class="cf-muted">
          Danh mục giúp thống kê. Ẩn thay cho xóa: danh mục đã ẩn không chọn được cho khoản mới, khoản cũ vẫn giữ nguyên.
          Tên không trùng trong cùng loại (không phân biệt hoa thường).
        </span>
        @if (actionError(); as message) {
          <div class="cf-alert cf-alert--danger" role="alert">{{ message }}</div>
        }

        <div class="groups">
          @for (group of groups(); track group.type) {
            <section class="cf-card card" [attr.aria-label]="group.title">
              <div class="head">
                <span class="dot" [class.dot--income]="group.type === 'Income'" aria-hidden="true"></span>
                <h2>{{ group.title }}</h2>
                <span class="cf-muted small">{{ group.count }}</span>
              </div>

              <div>
                @for (item of group.items; track item.id) {
                  <div class="cat" [class.cat--hidden]="item.isHidden && editingId() !== item.id">
                    @if (editingId() === item.id) {
                      <div class="field">
                        <label class="sr-only" [for]="'cat-edit-' + item.id">Tên mới cho {{ item.name }}</label>
                        <input
                          kendoTextBox
                          [id]="'cat-edit-' + item.id"
                          [attr.maxlength]="maxLength"
                          [value]="editDraft()"
                          [attr.aria-invalid]="editError() ? 'true' : null"
                          (input)="editDraft.set($any($event.target).value)"
                          (keydown.enter)="saveEdit(item)"
                          (keydown.escape)="cancelEdit()"
                        />
                        @if (editError(); as message) {
                          <span class="err" role="alert">{{ message }}</span>
                        }
                      </div>
                      <button kendoButton themeColor="primary" type="button" [disabled]="busy()" (click)="saveEdit(item)">
                        Lưu
                      </button>
                      <button kendoButton type="button" [disabled]="busy()" (click)="cancelEdit()">Hủy</button>
                    } @else {
                      <span class="name">{{ item.name }}</span>
                      @if (item.isHidden) {
                        <span class="cf-tag">Đã ẩn</span>
                      }
                      <span class="cf-muted small">{{ item.transactionCount ?? 0 }} khoản</span>
                      <button
                        kendoButton
                        type="button"
                        [disabled]="busy()"
                        [attr.aria-label]="'Đổi tên ' + item.name"
                        (click)="startEdit(item)"
                      >
                        Đổi tên
                      </button>
                      <button
                        kendoButton
                        type="button"
                        [disabled]="busy()"
                        [attr.aria-label]="(item.isHidden ? 'Hiện lại ' : 'Ẩn ') + item.name"
                        (click)="toggleHidden(item)"
                      >
                        {{ item.isHidden ? 'Hiện lại' : 'Ẩn' }}
                      </button>
                    }
                  </div>
                } @empty {
                  <span class="cf-muted">Chưa có danh mục.</span>
                }
              </div>

              <div class="add">
                <div class="field">
                  <label class="sr-only" [for]="'cat-new-' + group.type">Tên {{ group.title.toLowerCase() }} mới</label>
                  <input
                    kendoTextBox
                    [id]="'cat-new-' + group.type"
                    placeholder="Tên danh mục mới"
                    [attr.maxlength]="maxLength"
                    [value]="newDrafts()[group.type]"
                    [attr.aria-invalid]="addErrors()[group.type] ? 'true' : null"
                    (input)="setNewDraft(group.type, $any($event.target).value)"
                    (keydown.enter)="add(group.type)"
                  />
                  @if (addErrors()[group.type]; as message) {
                    <span class="err" role="alert">{{ message }}</span>
                  }
                </div>
                <button kendoButton type="button" [disabled]="busy()" (click)="add(group.type)">+ Thêm</button>
              </div>
            </section>
          }
        </div>
        <span class="sr-only" aria-live="polite">{{ all.length }} danh mục</span>
      } @else {
        @switch (kind()) {
          @case ('error') {
            <app-state class="cf-card" kind="error" actionLabel="Thử lại" (action)="load()" />
          }
          @default {
            <app-state [kind]="kind()!" />
          }
        }
      }
    </section>
  `,
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 16px;
      max-width: 1052px;
    }
    app-page-header {
      margin-bottom: 0;
    }
    .groups {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(360px, 100%), 1fr));
      gap: 16px;
    }
    .card {
      display: flex;
      flex-direction: column;
      gap: 10px;
      min-width: 0;
    }
    .head {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    h2 {
      flex: 1;
      margin: 0;
      font-size: 16px;
    }
    .dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: var(--cf-expense);
    }
    .dot--income {
      background: var(--cf-income);
    }
    .small {
      font-size: 12px;
    }
    .cat {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: 8px;
      padding: 8px 0;
      border-bottom: 1px solid var(--cf-subtle);
    }
    .cat--hidden {
      opacity: 0.7;
    }
    .name {
      flex: 1 1 140px;
      min-width: 0;
      font-weight: 600;
      overflow-wrap: anywhere;
    }
    .cat > .cf-muted,
    .cat > .cf-tag {
      align-self: center;
    }
    .field {
      display: flex;
      flex: 1 1 200px;
      flex-direction: column;
      gap: 4px;
      min-width: 0;
    }
    .err {
      color: var(--cf-danger-fg);
      font-size: 12px;
    }
    .add {
      display: flex;
      align-items: flex-start;
      gap: 8px;
    }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
    }
  `,
})
export class CategoriesPage {
  private readonly api = inject(CategoriesApi);
  private readonly notifications = inject(AppNotifications);
  private readonly destroyRef = inject(DestroyRef);

  /** `:fundId` trên URL (component input binding, kế thừa tham số route cha). */
  readonly fundId = input.required<string>();

  protected readonly maxLength = CATEGORY_NAME_MAX_LENGTH;
  protected readonly state = signal<LoadState<Category[]>>(LOADING);
  protected readonly editingId = signal<string | null>(null);
  protected readonly editDraft = signal('');
  protected readonly editError = signal<string | null>(null);
  protected readonly newDrafts = signal<Record<CategoryType, string>>({ Income: '', Expense: '' });
  protected readonly addErrors = signal<Record<CategoryType, string | null>>({ Income: null, Expense: null });
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);

  protected readonly kind = computed(() => stateKind(this.state(), () => false));
  protected readonly categories = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data : null;
  });
  protected readonly groups = computed<Group[]>(() => {
    const all = this.categories() ?? [];
    return GROUPS.map(({ type, title }) => {
      const items = all.filter((category) => category.type === type);
      const hidden = items.filter((category) => category.isHidden).length;
      return { type, title, items, count: `${items.length - hidden} đang dùng · ${hidden} đã ẩn` };
    });
  });

  constructor() {
    effect(() => {
      this.fundId();
      untracked(() => this.load());
    });
  }

  /** `silent` giữ dữ liệu cũ trên màn hình trong lúc tải lại (sau thêm / đổi tên / ẩn). */
  protected load(silent = false): void {
    toLoadState(this.api.list(this.fundId(), { includeHidden: true }))
      .pipe(
        filter((state) => !silent || state.status !== 'loading'),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((state) => this.state.set(state));
  }

  protected setNewDraft(type: CategoryType, value: string): void {
    this.newDrafts.update((drafts) => ({ ...drafts, [type]: value }));
    this.addErrors.update((errors) => ({ ...errors, [type]: null }));
  }

  protected startEdit(category: Category): void {
    this.actionError.set(null);
    this.editingId.set(category.id);
    this.editDraft.set(category.name);
    this.editError.set(null);
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.editError.set(null);
  }

  protected add(type: CategoryType): void {
    if (this.busy()) {
      return;
    }
    this.actionError.set(null);
    const name = this.newDrafts()[type].trim();
    const error = categoryNameError(name, type, this.categories() ?? []);
    if (error) {
      this.addErrors.update((errors) => ({ ...errors, [type]: error }));
      return;
    }
    this.run(
      this.api.create(this.fundId(), { type, name }),
      `Đã thêm danh mục “${name}”.`,
      () => this.newDrafts.update((drafts) => ({ ...drafts, [type]: '' })),
      (message) => this.addErrors.update((errors) => ({ ...errors, [type]: message })),
      categoryNameTaken(type, name),
    );
  }

  protected saveEdit(category: Category): void {
    if (this.busy()) {
      return;
    }
    this.actionError.set(null);
    const name = this.editDraft().trim();
    const error = categoryNameError(name, category.type, this.categories() ?? [], category.id);
    if (error) {
      this.editError.set(error);
      return;
    }
    const isHidden = category.isHidden ?? false;
    if (name === category.name) {
      this.cancelEdit();
      return;
    }
    this.run(
      this.api.update(this.fundId(), category.id, { name, isHidden }),
      `Đã đổi tên danh mục thành “${name}”.`,
      () => this.cancelEdit(),
      (message) => this.editError.set(message),
      categoryNameTaken(category.type, name),
    );
  }

  protected toggleHidden(category: Category): void {
    if (this.busy()) {
      return;
    }
    this.actionError.set(null);
    const hide = !category.isHidden;
    this.run(
      this.api.update(this.fundId(), category.id, { name: category.name, isHidden: hide }),
      `${hide ? 'Đã ẩn' : 'Đã hiện lại'} “${category.name}”.`,
      () => undefined,
      (message) => this.actionError.set(message),
      '',
    );
  }

  /**
   * Gọi API ghi rồi tải lại. `category.name_taken` (người khác vừa thêm cùng tên) hiện ở ô nhập; 403 / 404 thay cả trang;
   * lỗi khác hiện ở hộp lỗi đầu trang.
   */
  private run(
    request: Observable<unknown>,
    success: string,
    onSuccess: () => void,
    onFieldError: (message: string) => void,
    nameTakenMessage: string,
  ): void {
    this.busy.set(true);
    request
      .pipe(
        finalize(() => this.busy.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => {
          onSuccess();
          this.notifications.success(success);
          this.load(true);
        },
        error: (error: unknown) => {
          if (error instanceof ApiError && (error.status === 403 || error.status === 404)) {
            this.state.set({ status: 'error', error });
            return;
          }
          if (error instanceof ApiError && error.code === 'category.name_taken' && nameTakenMessage) {
            onFieldError(nameTakenMessage);
            this.load(true);
            return;
          }
          onFieldError(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
        },
      });
  }
}
