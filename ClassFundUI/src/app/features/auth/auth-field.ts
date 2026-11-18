import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { TextBoxDirective } from '@progress/kendo-angular-inputs';
import { map, switchMap } from 'rxjs';

import { serverError } from '../../shared/forms/server-errors';

const DEFAULT_MESSAGES: Readonly<Record<string, string>> = {
  required: 'Vui lòng nhập thông tin này.',
  email: 'Email không hợp lệ.',
  passwordPolicy: 'Mật khẩu cần 8–128 ký tự, có cả chữ và số.',
  mismatch: 'Mật khẩu nhập lại chưa khớp.',
  unchanged: 'Mật khẩu mới phải khác mật khẩu hiện tại.',
  maxlength: 'Nội dung quá dài.',
};

/**
 * Một ô nhập có nhãn, gợi ý và lỗi (design-system.md: lỗi hiện sau khi chạm vào ô / bấm gửi). Nội dung chèn vào
 * (ví dụ danh sách yêu cầu mật khẩu) hiện ngay dưới ô. Lỗi từ server (`{ server }`) ưu tiên hơn lỗi kiểm tra tại chỗ.
 */
@Component({
  selector: 'app-auth-field',
  imports: [ReactiveFormsModule, TextBoxDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="label-row">
      <label class="label" [for]="fieldId()">{{ label() }}</label>
      @if (required()) {
        <span class="required" aria-hidden="true">*</span>
      }
    </div>
    <input
      kendoTextBox
      [id]="fieldId()"
      [type]="type()"
      [formControl]="control()"
      [attr.autocomplete]="autocomplete()"
      [attr.aria-invalid]="invalid() ? 'true' : null"
      [attr.aria-required]="required() ? 'true' : null"
      [attr.aria-describedby]="describedBy()"
      [attr.maxlength]="maxLength()"
      [readOnly]="readonly()"
    />
    <ng-content />
    @if (message(); as text) {
      <p class="error" role="alert" [id]="fieldId() + '-error'">{{ text }}</p>
    } @else if (hint(); as text) {
      <p class="hint" [id]="fieldId() + '-hint'">{{ text }}</p>
    }
  `,
  styles: `
    :host {
      display: block;
      --kendo-border-radius-md: var(--cf-radius-lg);
    }
    .label-row {
      display: flex;
      gap: 4px;
      margin-bottom: 6px;
      font-weight: 600;
    }
    .required {
      color: var(--cf-expense);
    }
    input {
      width: 100%;
      box-sizing: border-box;
    }
    input[readonly] {
      background: var(--cf-subtle);
      color: var(--cf-text);
    }
    .error,
    .hint {
      margin: 6px 0 0;
      font-size: 13px;
    }
    .hint {
      color: var(--cf-text-muted);
    }
    .error {
      color: var(--cf-danger-fg);
    }
  `,
})
export class AuthField {
  readonly control = input.required<FormControl<string>>();
  readonly fieldId = input.required<string>();
  readonly label = input.required<string>();
  readonly type = input<'text' | 'email' | 'password'>('text');
  readonly autocomplete = input<string>();
  readonly hint = input<string>();
  readonly readonly = input(false);
  readonly required = input(false);
  readonly maxLength = input<number>();
  /** Thông báo riêng theo khóa lỗi, ghi đè thông báo mặc định (chuỗi rỗng = không hiện thông báo cho khóa đó). */
  readonly messages = input<Readonly<Record<string, string>>>({});

  /** Đổi mỗi khi control đổi giá trị / trạng thái / touched (kể cả khi form cha gọi markAllAsTouched). */
  private readonly changes = toSignal(
    toObservable(this.control).pipe(
      switchMap((control) => control.events),
      map(() => ({})),
    ),
    { initialValue: {} },
  );

  /** Ô đang không hợp lệ và đã chạm / form đã gửi (kể cả khi không có câu lỗi riêng, ví dụ chỉ có danh sách yêu cầu). */
  protected readonly invalid = computed(() => {
    this.changes();
    const control = this.control();
    return control.invalid && control.touched;
  });

  /** Lỗi hiện khi ô đã chạm / form đã gửi (markAsTouched) và đang không hợp lệ. */
  protected readonly message = computed<string | null>(() => {
    this.changes();
    const control = this.control();
    if (!control.invalid || !control.touched) {
      return null;
    }
    const server = serverError(control);
    if (server) {
      return server;
    }
    const key = Object.keys(control.errors ?? {}).find((name) => name !== 'server');
    if (!key) {
      return null;
    }
    const text = this.messages()[key] ?? DEFAULT_MESSAGES[key] ?? 'Giá trị không hợp lệ.';
    return text === '' ? null : text;
  });

  protected readonly describedBy = computed<string | null>(() => {
    if (this.message()) {
      return this.fieldId() + '-error';
    }
    return this.hint() ? this.fieldId() + '-hint' : null;
  });
}
