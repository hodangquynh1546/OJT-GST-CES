import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DatePickerComponent } from '@progress/kendo-angular-dateinputs';
import { DialogActionsComponent, DialogComponent, DialogTitleBarComponent } from '@progress/kendo-angular-dialog';
import { DropDownListComponent } from '@progress/kendo-angular-dropdowns';
import { LoaderComponent } from '@progress/kendo-angular-indicators';
import {
  CheckBoxDirective,
  ErrorComponent,
  FormFieldComponent,
  HintComponent,
  NumericTextBoxComponent,
  TextAreaComponent,
  TextBoxComponent,
} from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { finalize, startWith } from 'rxjs';

import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { fromDate, toDate, vietnamToday } from '../ledger-dates';
import { TransactionsApi } from '../transactions.api';
import { FIELD_OF_CODE } from '../transactions.errors';
import { Category } from '../../categories/categories.models';
import { DESCRIPTION_MAX, NOTE_MAX, Transaction, TransactionType } from '../transactions.models';

const trimmedRequired = (control: AbstractControl<string>): ValidationErrors | null =>
  control.value?.trim() ? null : { required: true };

/**
 * T-01 "Thêm / Sửa khoản thu chi" 560 px – Canvas `TxDialog` (FR-02 → FR-04; BR-01, BR-04, BR-05, BR-07, BR-12, BR-14).
 * Mỗi lần mở có một `idempotencyKey`: bấm Lưu nhiều lần (kể cả khi mạng chập chờn) không tạo khoản trùng.
 */
@Component({
  selector: 'app-tx-dialog',
  imports: [
    ReactiveFormsModule,
    ButtonComponent,
    CheckBoxDirective,
    DatePickerComponent,
    DialogActionsComponent,
    DialogComponent,
    DialogTitleBarComponent,
    DropDownListComponent,
    ErrorComponent,
    FormFieldComponent,
    HintComponent,
    LabelComponent,
    LoaderComponent,
    NumericTextBoxComponent,
    TextAreaComponent,
    TextBoxComponent,
    VndMoneyPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <kendo-dialog [width]="560" [maxWidth]="'calc(100vw - 32px)'" (close)="cancel()">
      <kendo-dialog-titlebar>
        <span class="title"><span class="dot" [class.dot--income]="type() === 'Income'" aria-hidden="true"></span>{{ title() }}</span>
      </kendo-dialog-titlebar>

      <form id="tx-form" class="body" [formGroup]="form" (ngSubmit)="save()" novalidate>
        @if (errorCount() > 0) {
          <div class="cf-alert cf-alert--danger box" role="alert">
            <b>Chưa lưu được.</b><span>Kiểm tra {{ errorCount() }} ô được đánh dấu.</span>
          </div>
        }
        @if (summary(); as message) {
          <div class="cf-alert cf-alert--danger" role="alert">{{ message }}</div>
        }
        @if (transaction()) {
          <div class="box note-box">Không đổi được loại thu ↔ chi. Nếu ghi nhầm loại, hãy hủy khoản này và ghi lại.</div>
        }

        <kendo-formfield showErrors="always">
          <kendo-label [for]="description" text="Nội dung *" />
          <kendo-textbox #description formControlName="description" [maxlength]="descriptionMax" placeholder="Ví dụ: Mua giấy A4" [readonly]="saving()" />
          @if (shown('description'); as message) {
            <kendo-formerror>{{ message }}</kendo-formerror>
          }
        </kendo-formfield>

        <div class="pair">
          <kendo-formfield showErrors="always">
            <kendo-label [for]="amount" text="Số tiền (đ) *" />
            <kendo-numerictextbox
              #amount
              class="amount"
              formControlName="amount"
              format="n0"
              [decimals]="0"
              [min]="0"
              [spinners]="false"
              [autoCorrect]="true"
              placeholder="0"
              [readonly]="saving()"
            />
            @if (shown('amount'); as message) {
              <kendo-formerror>{{ message }}</kendo-formerror>
            }
          </kendo-formfield>
          <kendo-formfield showErrors="always">
            <kendo-label [for]="occurredOn" text="Ngày *" />
            <kendo-datepicker #occurredOn formControlName="occurredOn" format="dd/MM/yyyy" [max]="maxDate" [readonly]="saving()" />
            @if (shown('occurredOn'); as message) {
              <kendo-formerror>{{ message }}</kendo-formerror>
            }
          </kendo-formfield>
        </div>

        <kendo-formfield showHints="always" showErrors="always">
          <kendo-label [for]="category" text="Danh mục *" />
          <kendo-dropdownlist
            #category
            formControlName="categoryId"
            [data]="categoryChoices()"
            textField="name"
            valueField="id"
            [valuePrimitive]="true"
            [defaultItem]="{ id: null, name: 'Chọn danh mục' }"
            [readonly]="saving()"
          />
          @if (shown('categoryId'); as message) {
            <kendo-formerror>{{ message }}</kendo-formerror>
          } @else {
            <kendo-formhint>Danh mục đã ẩn không có trong danh sách; khoản cũ vẫn giữ danh mục của nó.</kendo-formhint>
          }
        </kendo-formfield>

        <kendo-formfield showHints="always" showErrors="always">
          <kendo-label [for]="note" text="Ghi chú nội bộ" [optional]="false" />
          <kendo-textarea #note formControlName="note" [rows]="3" [maxlength]="noteMax" placeholder="Không bắt buộc" [readonly]="saving()" />
          @if (shown('note'); as message) {
            <kendo-formerror>{{ message }}</kendo-formerror>
          } @else {
            <kendo-formhint>Chỉ thủ quỹ và GVCN thấy. Phụ huynh không bao giờ thấy ghi chú.</kendo-formhint>
          }
        </kendo-formfield>

        <div class="public">
          <input id="tx-public" type="checkbox" kendoCheckBox formControlName="isPublic" />
          <label for="tx-public">
            <b>Công khai cho học sinh và phụ huynh</b>
            <span class="cf-muted small">
              {{
                isPublic()
                  ? 'Đang bật: phụ huynh thấy nội dung, ngày, danh mục và số tiền.'
                  : 'Đang tắt: phụ huynh không thấy nội dung; số tiền vẫn nằm trong số dư và dòng “Khoản khác”.'
              }}
            </span>
          </label>
        </div>
        @if (!isPublic()) {
          <div class="cf-alert cf-alert--warning box" role="status">
            <b>Khoản này sẽ không công khai chi tiết</b>
            <span>
              Nội dung, ngày, danh mục và ghi chú được ẩn với phụ huynh. Số tiền <b>không được bảo mật tuyệt đối</b>: vẫn tính
              vào số dư và tổng thu chi mà phụ huynh thấy, nằm trong dòng “Khoản khác”, nên có thể suy ra – nhất là khi đây là
              khoản không công khai duy nhất cùng loại trong tháng.
            </span>
          </div>
        } @else if (transaction() && transaction()!.isPublic === false) {
          <div class="cf-alert box" role="status">
            <b>Khoản này sẽ được công khai</b>
            <span>Sau khi lưu, phụ huynh thấy nội dung, ngày, danh mục và số tiền. Ghi chú nội bộ vẫn chỉ thủ quỹ và GVCN thấy.</span>
          </div>
        }
        @if (negativeAfter(); as after) {
          <div class="cf-alert cf-alert--warning" role="status">
            Sau khoản chi này, số dư quỹ sẽ là <b>{{ after | vndMoney }}</b>. Bạn vẫn có thể lưu (khoản chi lớn hơn số dư được phép).
          </div>
        }
      </form>

      <kendo-dialog-actions layout="end">
        <span class="cf-muted foot">
          {{ saving() ? 'Đang gửi – nút bị khóa để không lưu trùng.' : 'Nhấn Lưu nhiều lần cũng không tạo khoản trùng.' }}
        </span>
        <button kendoButton type="button" [disabled]="saving()" (click)="cancel()">Hủy bỏ</button>
        <button kendoButton themeColor="primary" type="submit" form="tx-form" [disabled]="saving()" [attr.aria-busy]="saving()">
          @if (saving()) {
            <kendo-loader size="small" type="infinite-spinner" themeColor="light" />
            Đang lưu…
          } @else {
            Lưu
          }
        </button>
      </kendo-dialog-actions>
    </kendo-dialog>
  `,
  styles: `
    .title {
      display: flex;
      align-items: center;
      gap: 10px;
      font-size: 18px;
      font-weight: 700;
      line-height: 26px;
    }
    .dot {
      flex: none;
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: var(--cf-expense);
    }
    .dot--income {
      background: var(--cf-income);
    }
    .body {
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    kendo-formfield {
      margin: 0;
    }
    .pair {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 12px;
    }
    .amount ::ng-deep input {
      text-align: right;
      font-variant-numeric: tabular-nums;
    }
    .box {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .note-box {
      padding: 10px 12px;
      border-radius: var(--cf-radius-md);
      background: var(--cf-subtle);
    }
    .public {
      display: flex;
      align-items: flex-start;
      gap: 10px;
      padding: 12px;
      border-radius: var(--cf-radius-md);
      background: var(--cf-background);
    }
    .public label {
      display: flex;
      flex-direction: column;
      gap: 2px;
      cursor: pointer;
    }
    .small {
      font-size: 13px;
    }
    .foot {
      flex: 1;
      min-width: 180px;
      font-size: 12px;
    }
    kendo-loader {
      margin-right: 6px;
    }
  `,
})
export class TxDialog implements OnInit {
  private readonly api = inject(TransactionsApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly fundId = input.required<string>();
  /** Loại khi thêm; khi sửa lấy theo khoản. */
  readonly addType = input<TransactionType>('Expense');
  /** Khoản đang sửa (bản đầy đủ); null = thêm mới. */
  readonly transaction = input<Transaction | null>(null);
  /** Mọi danh mục của quỹ (kể cả đã ẩn – để giữ danh mục hiện tại khi sửa). */
  readonly categories = input<readonly Category[]>([]);
  /** Số dư hiện tại (null = chưa biết, không cảnh báo trước). */
  readonly balance = input<number | null>(null);

  readonly saved = output<Transaction>();
  readonly closed = output<void>();

  protected readonly descriptionMax = DESCRIPTION_MAX;
  protected readonly noteMax = NOTE_MAX;
  protected readonly maxDate = toDate(vietnamToday());
  protected readonly saving = signal(false);
  protected readonly tried = signal(false);
  protected readonly summary = signal<string | null>(null);
  /** Khóa chống lưu trùng – sinh một lần cho mỗi lần mở dialog. */
  private readonly idempotencyKey = crypto.randomUUID();

  protected readonly form = new FormGroup({
    description: new FormControl('', { nonNullable: true, validators: [trimmedRequired, (c) => (c.value.trim().length > DESCRIPTION_MAX ? { maxlength: true } : null)] }),
    amount: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    occurredOn: new FormControl<Date | null>(toDate(vietnamToday()), [
      Validators.required,
      (c) => (c.value && fromDate(c.value) > vietnamToday() ? { future: true } : null),
    ]),
    categoryId: new FormControl<string | null>(null, Validators.required),
    note: new FormControl('', { nonNullable: true, validators: (c) => (c.value.trim().length > NOTE_MAX ? { maxlength: true } : null) }),
    isPublic: new FormControl(true, { nonNullable: true }),
  });

  private readonly value = toSignal(this.form.valueChanges.pipe(startWith(this.form.getRawValue())), {
    initialValue: this.form.getRawValue(),
  });
  /** Đổi trạng thái lỗi (kể cả lỗi server) cũng phải tính lại số ô lỗi. */
  private readonly status = toSignal(this.form.statusChanges, { initialValue: this.form.status });

  protected readonly type = computed<TransactionType>(() => this.transaction()?.type ?? this.addType());
  protected readonly title = computed(() => {
    const tx = this.transaction();
    if (!tx) {
      return this.type() === 'Income' ? 'Thêm khoản thu' : 'Thêm khoản chi';
    }
    if (tx.isPublic === false) {
      return 'Sửa khoản không công khai';
    }
    return tx.type === 'Income' ? 'Sửa khoản thu' : 'Sửa khoản chi';
  });
  protected readonly isPublic = computed(() => this.value().isPublic !== false);
  protected readonly categoryChoices = computed(() => {
    const current = this.transaction()?.categoryId;
    return this.categories().filter((category) => category.type === this.type() && (!category.isHidden || category.id === current));
  });
  protected readonly errorCount = computed(() => {
    this.status();
    this.value();
    if (!this.tried() || this.saving()) {
      return 0;
    }
    return Object.values(this.form.controls).filter((control) => control.invalid).length;
  });
  /** BR-04: chỉ cảnh báo trước khi lưu; server vẫn trả `isNegativeBalance` sau khi lưu. */
  protected readonly negativeAfter = computed(() => {
    const balance = this.balance();
    const amount = this.value().amount ?? 0;
    if (this.type() !== 'Expense' || balance === null || amount <= 0) {
      return null;
    }
    const tx = this.transaction();
    const before = tx && !tx.isCancelled ? balance + tx.amount : balance;
    const after = before - amount;
    return after < 0 ? after : null;
  });

  ngOnInit(): void {
    const tx = this.transaction();
    if (tx) {
      this.form.reset({
        description: tx.description,
        amount: tx.amount,
        occurredOn: toDate(tx.occurredOn),
        categoryId: tx.categoryId,
        note: tx.note ?? '',
        isPublic: tx.isPublic !== false,
      });
    }
  }

  /** Thông báo lỗi của ô – chỉ hiện sau khi bấm Lưu (design-system.md mục 6). */
  protected shown(name: keyof TxDialog['form']['controls']): string | null {
    this.status();
    this.value();
    const control = this.form.controls[name];
    if (!this.tried() || control.valid) {
      return null;
    }
    const server = serverError(control);
    if (server) {
      return server;
    }
    switch (name) {
      case 'description':
        return `Nhập nội dung (tối đa ${DESCRIPTION_MAX} ký tự).`;
      case 'amount':
        return 'Số tiền phải là số nguyên lớn hơn 0.';
      case 'occurredOn':
        return 'Ngày không được để trống hoặc ở tương lai.';
      case 'categoryId':
        return 'Chọn danh mục.';
      case 'note':
        return `Ghi chú tối đa ${NOTE_MAX} ký tự.`;
      default:
        return null;
    }
  }

  protected save(): void {
    if (this.saving()) {
      return;
    }
    this.tried.set(true);
    this.summary.set(null);
    this.form.updateValueAndValidity();
    // Kendo tô viền đỏ + aria-invalid cho ô lỗi khi control đã "touched".
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }

    const raw = this.form.getRawValue();
    const values = {
      amount: raw.amount!,
      occurredOn: fromDate(raw.occurredOn!),
      categoryId: raw.categoryId!,
      description: raw.description.trim(),
      note: raw.note.trim() || null,
      isPublic: raw.isPublic,
    };
    const tx = this.transaction();
    const request = tx
      ? this.api.update(this.fundId(), tx.id, values)
      : this.api.create(this.fundId(), this.type(), values, this.idempotencyKey);

    this.saving.set(true);
    request
      .pipe(
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (saved) => this.saved.emit(saved),
        error: (error: unknown) => this.summary.set(applyServerErrors(this.form, error, FIELD_OF_CODE)),
      });
  }

  protected cancel(): void {
    if (!this.saving()) {
      this.closed.emit();
    }
  }
}
