import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { ErrorComponent, FormFieldComponent, HintComponent, TextBoxComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { Observable, finalize } from 'rxjs';

import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { ApiError } from '../../../shared/models/api-error';
import { VnDatePipe } from '../../../shared/pipes/vn-date.pipe';
import { CredentialNotice } from '../credential-dialog/credential-dialog';
import { StudentsApi } from '../students.api';
import {
  ACCOUNT_STATUS,
  Credential,
  PARENT_NAME_MAX_LENGTH,
  STUDENT_NAME_MAX_LENGTH,
  Student,
  USERNAME_MAX_LENGTH,
} from '../students.models';
import {
  EMAIL_ERROR,
  EMAIL_REQUIRED_ERROR,
  STUDENT_NAME_ERROR,
  USERNAME_ERROR,
  optionalUsernameValidator,
  requiredEmailValidator,
  studentNameValidator,
} from '../students.validators';

/** Một thao tác trong drawer đã xong: trang tải lại danh sách rồi hiện thông báo / hộp thoại mật khẩu tạm. */
export interface StudentMutation {
  message?: string;
  credential?: CredentialNotice;
}

type Step = 'reset' | 'unlink' | null;

/**
 * Drawer chi tiết học sinh 460 px (Canvas G-02): sửa họ tên, cấp tài khoản, cấp lại mật khẩu tạm (có xác nhận),
 * liên kết / gỡ liên kết phụ huynh (có xác nhận), link sang G-03. Mọi thao tác gọi API rồi báo `mutated`;
 * danh sách (nguồn dữ liệu duy nhất) do trang tải lại.
 */
@Component({
  selector: 'app-student-drawer',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonComponent,
    ErrorComponent,
    FormFieldComponent,
    HintComponent,
    LabelComponent,
    TextBoxComponent,
    VnDatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'onEscape()' },
  template: `
    <div class="backdrop" (click)="closed.emit()"></div>
    <aside #panel class="panel" role="dialog" aria-modal="true" aria-labelledby="student-drawer-title" tabindex="-1">
      <header class="head">
        <h2 id="student-drawer-title">{{ student().fullName }}</h2>
        <button kendoButton type="button" aria-label="Đóng" (click)="closed.emit()">✕</button>
      </header>

      <div class="content">
        @if (problem(); as message) {
          <div class="cf-alert cf-alert--danger" role="alert">{{ message }}</div>
        }

        <section class="cf-card section" aria-label="Thông tin học sinh">
          <div class="title-row">
            <h3>Thông tin học sinh</h3>
            @if (!renaming()) {
              <button kendoButton type="button" size="small" (click)="startRename()">Sửa họ tên</button>
            }
          </div>
          @if (renaming()) {
            <form [formGroup]="renameForm" (ngSubmit)="saveRename()" novalidate>
              <kendo-formfield showErrors="always">
                <kendo-label [for]="renameInput" text="Họ tên *" />
                <kendo-textbox
                  #renameInput
                  formControlName="fullName"
                  [maxlength]="nameMax"
                  [readonly]="busy()"
                />
                @if (message(renameForm.controls.fullName, nameError, renameTried()); as text) {
                  <kendo-formerror>{{ text }}</kendo-formerror>
                }
              </kendo-formfield>
              <div class="actions">
                <button kendoButton themeColor="primary" type="submit" [disabled]="busy()">Lưu</button>
                <button kendoButton type="button" [disabled]="busy()" (click)="renaming.set(false)">Hủy</button>
              </div>
            </form>
          }
          <dl class="dl">
            <dt class="cf-muted">Vai trò</dt>
            <dd>{{ student().isTreasurer ? 'Thủ quỹ (đang được phân công)' : 'Học sinh' }}</dd>
            <dt class="cf-muted">Thêm ngày</dt>
            <dd>{{ student().createdAt | vnDate }}</dd>
          </dl>
        </section>

        <section class="cf-card section" aria-label="Tài khoản đăng nhập">
          <h3>Tài khoản đăng nhập</h3>
          <dl class="dl">
            <dt class="cf-muted">Trạng thái</dt>
            <dd>
              <span class="cf-tag" [class]="statusTone()">{{ status().label }}</span>
            </dd>
            <dt class="cf-muted">Tên đăng nhập</dt>
            <dd data-testid="drawer-username">{{ student().username ?? '—' }}</dd>
          </dl>
          <span class="cf-muted hint">{{ status().hint }}</span>

          @if (student().accountStatus === 'None') {
            <form [formGroup]="accountForm" (ngSubmit)="issueAccount()" novalidate>
              <kendo-formfield showHints="always" showErrors="always">
                <kendo-label [for]="usernameInput" text="Tên đăng nhập" />
                <kendo-textbox
                  #usernameInput
                  formControlName="username"
                  autocapitalize="none"
                  [maxlength]="usernameMax"
                  [readonly]="busy()"
                />
                @if (message(accountForm.controls.username, usernameError, accountTried()); as text) {
                  <kendo-formerror>{{ text }}</kendo-formerror>
                } @else {
                  <kendo-formhint>Để trống: hệ thống gợi ý theo họ tên (ví dụ huy.pham.482).</kendo-formhint>
                }
              </kendo-formfield>
              <button kendoButton themeColor="primary" type="submit" class="self-start" [disabled]="busy()">
                Cấp tài khoản &amp; mật khẩu tạm
              </button>
            </form>
          }

          @if (canReset() && step() !== 'reset') {
            <button kendoButton type="button" class="self-start" [disabled]="busy()" (click)="step.set('reset')">
              Cấp lại mật khẩu tạm
            </button>
          }
          @if (step() === 'reset') {
            <div class="cf-alert cf-alert--warning" role="alert">
              <b>Cấp lại mật khẩu tạm cho {{ student().fullName }}?</b> Mật khẩu cũ hết hiệu lực và học sinh bị đăng
              xuất trên mọi thiết bị.
              @if (student().isTreasurer) {
                Học sinh vẫn là thủ quỹ nhưng không ghi thu chi được cho tới khi đổi mật khẩu mới.
              }
            </div>
            <div class="actions">
              <button kendoButton type="button" [disabled]="busy()" (click)="step.set(null)">Không</button>
              <button kendoButton themeColor="primary" type="button" [disabled]="busy()" (click)="resetPassword()">
                Cấp lại
              </button>
            </div>
          }
        </section>

        <section class="cf-card section" aria-label="Phụ huynh">
          <h3>Phụ huynh</h3>
          @if (student().parent; as parent) {
            <dl class="dl">
              <dt class="cf-muted">Họ tên</dt>
              <dd>{{ parent.fullName }}</dd>
              <dt class="cf-muted">Email</dt>
              <dd class="wrap">{{ parent.email }}</dd>
              <dt class="cf-muted">Tài khoản</dt>
              <dd>{{ parent.pendingActivation ? 'Chờ kích hoạt (chưa đổi mật khẩu tạm)' : 'Đang hoạt động' }}</dd>
            </dl>
            @if (step() !== 'unlink') {
              <button kendoButton type="button" class="self-start danger" [disabled]="busy()" (click)="step.set('unlink')">
                Gỡ liên kết
              </button>
            } @else {
              <div class="cf-alert cf-alert--danger" role="alert">
                <b>Gỡ liên kết phụ huynh?</b> Phụ huynh không xem được quỹ này nữa. Tài khoản phụ huynh vẫn còn (có thể
                đang liên kết học sinh khác). Lịch sử liên kết được giữ.
              </div>
              <div class="actions">
                <button kendoButton type="button" [disabled]="busy()" (click)="step.set(null)">Không</button>
                <button kendoButton type="button" class="danger" [disabled]="busy()" (click)="unlinkParent()">
                  Gỡ liên kết
                </button>
              </div>
            }
          } @else {
            <span class="cf-muted hint">
              Chưa liên kết. Mỗi học sinh có một email phụ huynh (v1). Email đã có tài khoản phụ huynh → chỉ liên kết;
              chưa có → tạo tài khoản với mật khẩu tạm.
            </span>
            <form [formGroup]="parentForm" (ngSubmit)="linkParent()" novalidate>
              <kendo-formfield showErrors="always">
                <kendo-label [for]="emailInput" text="Email phụ huynh *" />
                <kendo-textbox
                  #emailInput
                  formControlName="email"
                  inputmode="email"
                  placeholder="ten@example.com"
                  [readonly]="busy()"
                />
                @if (message(parentForm.controls.email, emailMessage(), parentTried()); as text) {
                  <kendo-formerror>{{ text }}</kendo-formerror>
                }
              </kendo-formfield>
              <kendo-formfield>
                <kendo-label [for]="parentNameInput" text="Họ tên phụ huynh (khi tạo tài khoản mới)" />
                <kendo-textbox
                  #parentNameInput
                  formControlName="fullName"
                  [maxlength]="parentNameMax"
                  [placeholder]="'Để trống: “Phụ huynh ' + student().fullName + '”'"
                  [readonly]="busy()"
                />
              </kendo-formfield>
              <button kendoButton themeColor="primary" type="submit" class="self-start" [disabled]="busy()">
                Liên kết phụ huynh
              </button>
            </form>
          }
        </section>

        <section class="cf-card section" aria-label="Thủ quỹ">
          <h3>Thủ quỹ</h3>
          <span class="small">{{ treasurerText() }}</span>
          <a kendoButton class="self-start" [routerLink]="['/funds', fundId(), 'treasurers']">
            Mở “Phân công thủ quỹ”
          </a>
        </section>
      </div>
    </aside>
  `,
  styles: `
    :host {
      position: fixed;
      inset: 0;
      z-index: 1000;
    }
    .backdrop {
      position: absolute;
      inset: 0;
      background: rgba(15, 23, 42, 0.35);
    }
    .panel {
      position: absolute;
      top: 0;
      right: 0;
      bottom: 0;
      display: flex;
      flex-direction: column;
      width: min(460px, 100%);
      background: var(--cf-surface);
      box-shadow: -8px 0 24px rgba(15, 23, 42, 0.2);
      outline: none;
    }
    .head {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 16px;
      border-bottom: 1px solid var(--cf-border);
    }
    h2 {
      flex: 1;
      margin: 0;
      font-size: 18px;
    }
    h3 {
      flex: 1;
      margin: 0;
      font-size: 15px;
    }
    .content {
      display: flex;
      flex-direction: column;
      gap: 16px;
      padding: 16px;
      overflow: auto;
    }
    .section {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .section form {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    kendo-formfield {
      margin: 0;
    }
    .title-row {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .dl {
      display: grid;
      grid-template-columns: 130px minmax(0, 1fr);
      gap: 8px 12px;
      margin: 0;
    }
    dd {
      margin: 0;
    }
    .wrap {
      overflow-wrap: anywhere;
    }
    .hint {
      font-size: 13px;
    }
    .small {
      font-size: 13px;
    }
    .actions {
      display: flex;
      gap: 8px;
    }
    .self-start {
      align-self: flex-start;
    }
    .danger {
      border-color: var(--cf-expense);
      color: var(--cf-danger-fg);
    }
  `,
})
export class StudentDrawer {
  private readonly api = inject(StudentsApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly fundId = input.required<string>();
  readonly student = input.required<Student>();
  /** Có hộp thoại khác đang mở phía trên (thêm học sinh, mật khẩu tạm): Esc không đóng drawer. */
  readonly blocked = input(false);
  readonly closed = output<void>();
  readonly mutated = output<StudentMutation>();

  protected readonly nameMax = STUDENT_NAME_MAX_LENGTH;
  protected readonly parentNameMax = PARENT_NAME_MAX_LENGTH;
  protected readonly usernameMax = USERNAME_MAX_LENGTH;
  protected readonly nameError = STUDENT_NAME_ERROR;
  protected readonly usernameError = USERNAME_ERROR;

  protected readonly renameForm = new FormGroup({
    fullName: new FormControl('', { nonNullable: true, validators: studentNameValidator }),
  });
  protected readonly accountForm = new FormGroup({
    username: new FormControl('', { nonNullable: true, validators: optionalUsernameValidator }),
  });
  protected readonly parentForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: requiredEmailValidator }),
    fullName: new FormControl('', { nonNullable: true }),
  });

  protected readonly renaming = signal(false);
  protected readonly step = signal<Step>(null);
  protected readonly busy = signal(false);
  protected readonly problem = signal<string | null>(null);
  protected readonly renameTried = signal(false);
  protected readonly accountTried = signal(false);
  protected readonly parentTried = signal(false);

  /** Đổi theo mã học sinh, không đổi khi danh sách tải lại ra object mới của cùng học sinh. */
  private readonly studentId = computed(() => this.student().id);
  protected readonly status = computed(() => ACCOUNT_STATUS[this.student().accountStatus]);
  protected readonly statusTone = computed(() => {
    const tone = this.status().tone;
    return tone ? `cf-tag--${tone}` : '';
  });
  protected readonly canReset = computed(() => {
    const status = this.student().accountStatus;
    return status === 'Active' || status === 'PendingActivation';
  });
  protected readonly treasurerText = computed(() => {
    const { fullName, isTreasurer, accountStatus } = this.student();
    if (isTreasurer) {
      return `${fullName} đang là thủ quỹ. Thu hồi ở trang Phân công thủ quỹ (không thu hồi được thủ quỹ cuối cùng).`;
    }
    return accountStatus === 'Active'
      ? 'Đủ điều kiện để được phân công thủ quỹ.'
      : 'Chưa đủ điều kiện: học sinh cần tài khoản đã kích hoạt.';
  });

  private readonly panel = viewChild.required<ElementRef<HTMLElement>>('panel');
  constructor() {
    afterNextRender(() => this.panel().nativeElement.focus());
    // Mở học sinh khác (hoặc dữ liệu đổi) → bỏ các bước đang dở.
    effect(() => {
      this.studentId();
      untracked(() => this.resetTransientState());
    });
  }

  /** Lỗi hiện dưới ô email: thiếu email hay sai định dạng. */
  protected emailMessage(): string {
    return this.parentForm.controls.email.hasError('required') ? EMAIL_REQUIRED_ERROR : EMAIL_ERROR;
  }

  /** Thông báo lỗi của một ô: lỗi từ server, hoặc lỗi kiểm tra sau khi đã bấm nút gửi. */
  protected message(control: AbstractControl, fallback: string, tried: boolean): string | null {
    return serverError(control) ?? (tried && control.invalid ? fallback : null);
  }

  protected onEscape(): void {
    if (!this.blocked()) {
      this.closed.emit();
    }
  }

  protected startRename(): void {
    this.renameForm.reset({ fullName: this.student().fullName });
    this.renameTried.set(false);
    this.renaming.set(true);
  }

  protected saveRename(): void {
    this.renameTried.set(true);
    if (this.renameForm.invalid) {
      return;
    }
    const fullName = this.renameForm.controls.fullName.value.trim();
    this.run(this.api.rename(this.fundId(), this.student().id, fullName), () => {
      this.renaming.set(false);
      this.mutated.emit({ message: 'Đã đổi họ tên học sinh.' });
    }, this.renameForm);
  }

  protected issueAccount(): void {
    this.accountTried.set(true);
    if (this.accountForm.invalid) {
      return;
    }
    const student = this.student();
    const username = this.accountForm.controls.username.value.trim();
    this.run(
      this.api.issueAccount(this.fundId(), student.id, username || undefined),
      (credential) =>
        this.mutated.emit({ credential: studentCredential(`Tài khoản học sinh – ${student.fullName}`, credential) }),
      this.accountForm,
      { 'user.username': 'username', 'user.username_taken': 'username' },
    );
  }

  protected resetPassword(): void {
    const student = this.student();
    this.run(this.api.resetPassword(this.fundId(), student.id), (credential) => {
      this.step.set(null);
      this.mutated.emit({ credential: studentCredential(`Mật khẩu tạm mới – ${student.fullName}`, credential) });
    });
  }

  protected linkParent(): void {
    this.parentTried.set(true);
    if (this.parentForm.invalid) {
      return;
    }
    const { email, fullName } = this.parentForm.getRawValue();
    this.run(
      this.api.linkParent(this.fundId(), this.student().id, email.trim(), fullName.trim() || undefined),
      (result) => {
        this.parentForm.reset();
        this.parentTried.set(false);
        if (result.credential) {
          this.mutated.emit({
            credential: {
              title: 'Tài khoản phụ huynh mới',
              recipient: 'phụ huynh',
              loginLabel: 'Email đăng nhập',
              login: result.credential.login,
              password: result.credential.temporaryPassword,
            },
          });
        } else {
          this.mutated.emit({ message: 'Đã liên kết với tài khoản phụ huynh hiện có. Không tạo tài khoản mới.' });
        }
      },
      this.parentForm,
      {
        'parent.email_in_use': 'email',
        'parent_link.exists': 'email',
        'parent.email_conflict': 'email',
      },
    );
  }

  protected unlinkParent(): void {
    const student = this.student();
    this.run(this.api.unlinkParent(this.fundId(), student.id), () => {
      this.step.set(null);
      this.mutated.emit({ message: `Đã gỡ liên kết phụ huynh của ${student.fullName}.` });
    });
  }

  private run<T>(
    request: Observable<T>,
    onSuccess: (result: T) => void,
    form?: FormGroup,
    fieldFor: Readonly<Record<string, string>> = {},
  ): void {
    if (this.busy()) {
      return;
    }
    this.problem.set(null);
    this.busy.set(true);
    request
      .pipe(
        finalize(() => this.busy.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: onSuccess,
        error: (error: unknown) => {
          this.step.set(null);
          this.problem.set(form ? applyServerErrors(form, error, fieldFor) : apiMessage(error));
          // Dữ liệu có thể đã đổi từ nơi khác (ví dụ học sinh đã có tài khoản): tải lại danh sách.
          if (error instanceof ApiError && ['student.account_exists', 'student.no_account'].includes(error.code)) {
            this.mutated.emit({});
          }
        },
      });
  }

  private resetTransientState(): void {
    this.renaming.set(false);
    this.step.set(null);
    this.problem.set(null);
    this.renameTried.set(false);
    this.accountTried.set(false);
    this.parentTried.set(false);
    this.accountForm.reset();
    this.parentForm.reset();
  }
}

function studentCredential(title: string, credential: Credential): CredentialNotice {
  return {
    title,
    recipient: 'học sinh',
    loginLabel: 'Tên đăng nhập',
    login: credential.login,
    password: credential.temporaryPassword,
  };
}

function apiMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.';
}
