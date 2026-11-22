import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DatePickerComponent } from '@progress/kendo-angular-dateinputs';
import { DialogActionsComponent, DialogComponent, DialogTitleBarComponent } from '@progress/kendo-angular-dialog';
import { DropDownListComponent } from '@progress/kendo-angular-dropdowns';
import { ErrorComponent, FormFieldComponent, HintComponent, TextAreaComponent, TextBoxComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { catchError, firstValueFrom, of, switchMap } from 'rxjs';

import { AppNotifications } from '../../../core/services/notification.service';
import { ScopeBadge } from '../../../shared/components/scope-badge/scope-badge';
import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { fromApiDate, toApiDate, todayApiDate } from '../../../shared/utils/api-date';
import { Attachment } from '../../attachments/attachments.models';
import { FileUpload } from '../../attachments/file-upload/file-upload';
import { CategoriesApi } from '../../categories/categories.api';
import { Category } from '../../categories/categories.models';
import { LedgerApi } from '../ledger.api';
import '../ledger.errors';
import {
  EVIDENCE_REQUIRED_ABOVE,
  EXPENSE_TITLE_MAX,
  LEDGER_TEXT_MAX,
  LedgerEntryDetail,
  MAX_AMOUNT,
  formatMoneyInput,
  parseMoney,
  spendable,
} from '../ledger.models';

type Kind = 'Income' | 'Expense';

/**
 * L-02 "Ghi giao dịch" (quỹ chung, có Thu / Chi) và "Ghi chi sự kiện" ở E-03 (chỉ Chi) – theo canvas LedgerBody / Event.
 * Thu không nhập tay (BR-39): chọn "Thu" chỉ dẫn sang tạo khoản thu. Chi: mức tối đa = số dư phạm vi (BR-38),
 * tự yêu cầu chứng từ khi &gt; 500.000 đ (BR-06).
 */
@Component({
  selector: 'app-expense-dialog',
  imports: [
    ButtonComponent,
    DatePickerComponent,
    DialogActionsComponent,
    DialogComponent,
    DialogTitleBarComponent,
    DropDownListComponent,
    ErrorComponent,
    FileUpload,
    FormFieldComponent,
    HintComponent,
    LabelComponent,
    ReactiveFormsModule,
    RouterLink,
    ScopeBadge,
    TextAreaComponent,
    TextBoxComponent,
    VndMoneyPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <kendo-dialog [width]="560" [maxWidth]="'calc(100vw - 32px)'" (close)="closed.emit()">
      <kendo-dialog-titlebar>
        <div class="title">
          <div class="title-row">
            <span>{{ eventId() ? 'Ghi chi sự kiện' : 'Ghi giao dịch' }}</span>
            <app-scope-badge [scope]="eventId() ? 'Event' : 'General'" [eventName]="eventName()" />
          </div>
          @if (!eventId()) {
            <span class="sub">Thu chi của sự kiện ghi trong trang chi tiết sự kiện.</span>
          }
        </div>
      </kendo-dialog-titlebar>

      <form class="form" [formGroup]="form" (ngSubmit)="save()" novalidate>
        @if (allowIncome()) {
          <div class="field">
            <span class="k-label" id="kind-label">Loại</span>
            <div class="tabs seg" role="radiogroup" aria-labelledby="kind-label">
              @for (option of kinds; track option.value) {
                <button
                  type="button"
                  role="radio"
                  class="tab"
                  [class.on]="kind() === option.value"
                  [attr.aria-checked]="kind() === option.value"
                  (click)="kind.set(option.value)"
                >
                  {{ option.label }}
                </button>
              }
            </div>
          </div>
        }

        @if (kind() === 'Income') {
          <div class="stack">
            <p class="cf-muted small">Tiền thu vào quỹ qua khoản thu: mỗi người nộp → thủ quỹ xác nhận → tự ghi vào sổ. Không ghi thu bằng tay.</p>
            <div class="cf-alert">
              Tạo khoản thu chung (tên, mục đích, số tiền mỗi người, hạn đóng) ở màn <strong>Khoản thu</strong>. Mọi thành viên, gồm cả thủ quỹ,
              nhận thông báo “[Quỹ chung] Khoản thu mới”.
            </div>
          </div>
        } @else {
          <kendo-formfield class="field cf-required">
            <kendo-label [for]="titleInput" text="Tên khoản chi" />
            <kendo-textbox #titleInput formControlName="title" [maxlength]="titleMax" [placeholder]="eventId() ? 'VD: Nước uống cho cả đoàn' : 'VD: Thuê loa sinh hoạt lớp'" />
            <kendo-formerror>{{ serverError(form.controls.title) ?? 'Vui lòng nhập tên khoản chi.' }}</kendo-formerror>
          </kendo-formfield>

          <kendo-formfield class="field">
            <kendo-label [for]="purposeInput" text="Mục đích" />
            <kendo-textbox #purposeInput formControlName="purpose" [maxlength]="textMax" />
          </kendo-formfield>

          <div class="grid2">
            <kendo-formfield class="field cf-required">
              <kendo-label [for]="amountInput" text="Số tiền (đ)" />
              <kendo-textbox
                #amountInput
                class="money"
                formControlName="amount"
                inputmode="numeric"
                [inputAttributes]="{ inputmode: 'numeric', autocomplete: 'off' }"
                (valueChange)="formatAmount($event)"
              />
              <kendo-formhint>Tối đa {{ maxSpend() | vndMoney }} (số dư {{ eventId() ? 'sự kiện' : 'quỹ chung' }})</kendo-formhint>
              <kendo-formerror>{{ serverError(form.controls.amount) ?? 'Vui lòng nhập số tiền lớn hơn 0.' }}</kendo-formerror>
            </kendo-formfield>
            <kendo-formfield class="field cf-required">
              <kendo-label [for]="dateInput" text="Ngày chi" />
              <kendo-datepicker #dateInput formControlName="date" format="dd/MM/yyyy" [max]="today" />
              <kendo-formerror>{{ serverError(form.controls.date) ?? 'Chọn ngày chi (không ở tương lai).' }}</kendo-formerror>
            </kendo-formfield>
          </div>

          @if (over()) {
            <div class="cf-alert cf-alert--danger" role="alert">
              Vượt số dư {{ eventId() ? 'sự kiện' : 'quỹ chung' }} ({{ maxSpend() | vndMoney }}). Tổng chi không được lớn hơn tổng thu – tạo khoản thu trước.
            </div>
          }

          <kendo-formfield class="field cf-required">
            <kendo-label [for]="categoryInput" text="Danh mục" />
            <kendo-dropdownlist
              #categoryInput
              formControlName="categoryId"
              [data]="categories()"
              textField="name"
              valueField="id"
              [valuePrimitive]="true"
              [defaultItem]="noCategory"
            />
            <kendo-formerror>{{ serverError(form.controls.categoryId) ?? 'Vui lòng chọn danh mục.' }}</kendo-formerror>
          </kendo-formfield>

          <div class="field">
            <span class="k-label">Chứng từ @if (needProof()) {<span class="cf-req">* bắt buộc</span>}</span>
            @if (needProof()) {
              <div class="cf-alert cf-alert--warning" role="status">Khoản chi trên 500.000 đ bắt buộc có chứng từ.</div>
            }
            <app-file-upload [fundId]="fundId()" kind="Receipt" label="Tải ảnh hóa đơn / PDF" [required]="needProof()" [(attachments)]="files" />
            @if (proofMissing()) {
              <span class="k-form-error" role="alert">Hãy đính kèm ít nhất một chứng từ.</span>
            }
          </div>

          <kendo-formfield class="field">
            <kendo-label [for]="noteInput" text="Ghi chú" />
            <kendo-textarea #noteInput formControlName="note" [rows]="2" resizable="vertical" [maxlength]="textMax" />
          </kendo-formfield>
        }
      </form>

      <kendo-dialog-actions layout="end">
        <button kendoButton type="button" (click)="closed.emit()">Hủy</button>
        @if (kind() === 'Income') {
          <a class="cf-primary-link" [routerLink]="['/funds', fundId(), 'charges', 'new']" (click)="closed.emit()">Tạo khoản thu</a>
        } @else {
          <button kendoButton themeColor="primary" type="button" [disabled]="busy() || over()" (click)="save()">
            {{ busy() ? 'Đang lưu…' : 'Lưu khoản chi' }}
          </button>
        }
      </kendo-dialog-actions>
    </kendo-dialog>
  `,
  // Theo canvas (.dlg, .field, .tabs.seg, .alert, .money): màu qua token --cf-*.
  styles: `
    .title {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .title-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .sub {
      color: var(--cf-text-muted);
      font-size: 13px;
      font-weight: 400;
    }
    .form,
    .stack {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin: 0;
    }
    .grid2 {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 12px;
    }
    .small {
      margin: 0;
      font-size: 13px;
    }
    .money ::ng-deep input {
      text-align: right;
      font-variant-numeric: tabular-nums;
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
      justify-content: center;
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
  `,
})
export class ExpenseDialog {
  private readonly api = inject(LedgerApi);
  private readonly categoriesApi = inject(CategoriesApi);
  private readonly notifications = inject(AppNotifications);

  readonly fundId = input.required<string>();
  /** null = quỹ chung. */
  readonly eventId = input<string | null>(null);
  readonly eventName = input<string | null>(null);
  /** Số dư hiện có của phạm vi = mức chi tối đa (BR-38). */
  readonly balance = input.required<number>();
  /** L-02 ở quỹ chung có chọn Thu / Chi; E-03 chỉ ghi chi. */
  readonly allowIncome = input(false);

  readonly closed = output<void>();
  readonly saved = output<LedgerEntryDetail>();

  protected readonly kinds: readonly { value: Kind; label: string }[] = [
    { value: 'Income', label: 'Thu' },
    { value: 'Expense', label: 'Chi' },
  ];
  protected readonly titleMax = EXPENSE_TITLE_MAX;
  protected readonly textMax = LEDGER_TEXT_MAX;
  protected readonly noCategory: Partial<Category> = { id: undefined, name: 'Chọn danh mục' };
  protected readonly today = fromApiDate(todayApiDate())!;
  protected readonly serverError = serverError;

  protected readonly kind = signal<Kind>('Expense');
  protected readonly busy = signal(false);
  protected readonly submitted = signal(false);
  protected readonly files = signal<Attachment[]>([]);

  protected readonly form = new FormGroup({
    title: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(EXPENSE_TITLE_MAX)] }),
    purpose: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(LEDGER_TEXT_MAX)] }),
    amount: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    date: new FormControl<Date | null>(this.today, { validators: [Validators.required] }),
    categoryId: new FormControl<string | null>(null, { validators: [Validators.required] }),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(LEDGER_TEXT_MAX)] }),
  });

  protected readonly categories = toSignal(
    toObservable(this.fundId).pipe(
      switchMap((fundId) => this.categoriesApi.list(fundId, { type: 'Expense', activeOnly: true }).pipe(catchError(() => of<Category[]>([])))),
    ),
    { initialValue: [] as Category[] },
  );

  private readonly amountValue = toSignal(this.form.controls.amount.valueChanges, { initialValue: '' });
  protected readonly amount = computed(() => parseMoney(this.amountValue()) ?? 0);
  protected readonly maxSpend = computed(() => spendable(this.balance()));
  protected readonly over = computed(() => this.amount() > this.maxSpend());
  protected readonly needProof = computed(() => this.amount() > EVIDENCE_REQUIRED_ABOVE && !this.over());
  protected readonly proofMissing = computed(() => this.submitted() && this.needProof() && this.files().length === 0);

  /** Giữ ô tiền dạng "650.000" khi gõ (canvas `.money`). */
  protected formatAmount(value: string): void {
    const formatted = formatMoneyInput(parseMoney(value));
    if (formatted !== value) {
      this.form.controls.amount.setValue(formatted);
    }
  }

  protected async save(): Promise<void> {
    this.submitted.set(true);
    const amount = parseMoney(this.form.controls.amount.value);
    if (amount === null || amount <= 0 || amount > MAX_AMOUNT) {
      this.form.controls.amount.setErrors({ required: true });
    }
    if (this.form.invalid || this.over() || this.proofMissing()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.busy.set(true);
    try {
      const detail = await firstValueFrom(
        this.api.createExpense(this.fundId(), {
          eventId: this.eventId(),
          categoryId: value.categoryId!,
          title: value.title.trim(),
          purpose: value.purpose.trim() || null,
          amount: amount!,
          transactionDate: toApiDate(value.date!),
          note: value.note.trim() || null,
          attachmentIds: this.files().map((file) => file.id),
        }),
      );
      this.notifications.success(`Đã ghi khoản chi vào sổ thu chi ${this.eventId() ? 'của sự kiện' : 'chung'}. Nhật ký hoạt động đã lưu thao tác.`);
      this.saved.emit(detail);
    } catch (error) {
      const message = applyServerErrors(this.form, error, {
        'category.unusable': 'categoryId',
        'ledger.future_date': 'date',
        'money.invalid': 'amount',
      });
      if (message) {
        this.notifications.error(message);
      }
    } finally {
      this.busy.set(false);
    }
  }
}

