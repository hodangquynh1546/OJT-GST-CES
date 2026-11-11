import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { CheckBoxDirective } from '@progress/kendo-angular-inputs';

/** Thông tin đăng nhập vừa cấp, hiện đúng một lần cho GVCN (BR-35, NFR-11). */
export interface CredentialNotice {
  title: string;
  /** "học sinh" / "phụ huynh" – dùng trong lời nhắc. */
  recipient: string;
  /** "Tên đăng nhập" (học sinh) hoặc "Email đăng nhập" (phụ huynh). */
  loginLabel: string;
  login: string;
  password: string;
}

/**
 * Hộp thoại mật khẩu tạm một lần (Canvas G-02, FR-25): phải tick "Tôi đã lưu / chuyển thông tin này" mới đóng được –
 * Esc và nút ✕ cũng bị chặn tới lúc đó. Hệ thống không gửi email, GVCN tự chuyển thông tin cho người nhận.
 */
@Component({
  selector: 'app-credential-dialog',
  imports: [ButtonComponent, CheckBoxDirective, DialogComponent, DialogActionsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <kendo-dialog [title]="notice().title" [width]="520" (close)="tryClose()">
      <div class="body" role="alertdialog" aria-describedby="credential-warning">
        <div id="credential-warning" class="cf-alert cf-alert--warning" role="alert">
          <b>Chỉ hiển thị một lần.</b> Đóng hộp thoại này là không xem lại được mật khẩu. Hệ thống
          <b>không gửi email</b> – hãy tự chuyển thông tin cho {{ notice().recipient }}.
        </div>
        <div class="field">
          <span class="label">{{ notice().loginLabel }}</span>
          <div class="cred" data-testid="credential-login">{{ notice().login }}</div>
        </div>
        <div class="field">
          <span class="label">Mật khẩu tạm</span>
          <div class="cred" data-testid="credential-password">{{ notice().password }}</div>
        </div>
        <span class="cf-muted note">Lần đăng nhập đầu, {{ notice().recipient }} phải đặt mật khẩu mới.</span>
        @if (copied()) {
          <span class="copied" role="status">✓ Đã sao chép {{ notice().loginLabel.toLowerCase() }} và mật khẩu tạm.</span>
        }
        <label class="ack">
          <input type="checkbox" kendoCheckBox [checked]="acknowledged()" (change)="toggle($event)" />
          Tôi đã lưu / chuyển thông tin này
        </label>
      </div>
      <kendo-dialog-actions layout="end">
        <button kendoButton type="button" (click)="copy()">Sao chép</button>
        <button kendoButton themeColor="primary" type="button" [disabled]="!acknowledged()" (click)="tryClose()">
          Đóng
        </button>
      </kendo-dialog-actions>
    </kendo-dialog>
  `,
  styles: `
    .body {
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .label {
      font-size: 13px;
      font-weight: 600;
    }
    .cred {
      padding: 10px 12px;
      border: 1px dashed #8c959f;
      border-radius: var(--cf-radius-md);
      background: var(--cf-background);
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      font-size: 18px;
      letter-spacing: 0.04em;
      overflow-wrap: anywhere;
      user-select: all;
    }
    .note {
      font-size: 13px;
    }
    .copied {
      color: var(--cf-success-fg);
      font-size: 13px;
    }
    .ack {
      display: flex;
      align-items: center;
      gap: 8px;
      min-height: 32px;
      cursor: pointer;
    }
  `,
})
export class CredentialDialog {
  readonly notice = input.required<CredentialNotice>();
  readonly closed = output<void>();

  protected readonly acknowledged = signal(false);
  protected readonly copied = signal(false);

  protected toggle(event: Event): void {
    this.acknowledged.set((event.target as HTMLInputElement).checked);
  }

  protected async copy(): Promise<void> {
    const { loginLabel, login, password } = this.notice();
    try {
      await navigator.clipboard.writeText(`${loginLabel}: ${login}\nMật khẩu tạm: ${password}`);
      this.copied.set(true);
    } catch {
      // Không có quyền clipboard: người dùng vẫn chọn tay được (user-select: all).
      this.copied.set(false);
    }
  }

  /** Esc, ✕ và nút "Đóng" đều đi qua đây: chưa tick xác nhận thì không đóng. */
  protected tryClose(): void {
    if (this.acknowledged()) {
      this.closed.emit();
    }
  }
}
