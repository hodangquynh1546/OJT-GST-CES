import { Injectable, inject } from '@angular/core';
import { DialogCloseResult, DialogService } from '@progress/kendo-angular-dialog';
import { Observable, map } from 'rxjs';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel: string;
  /** Hành động phá hủy (thu hồi, hủy khoản…): nút xác nhận màu đỏ. */
  danger?: boolean;
}

/**
 * Dialog xác nhận dùng chung (design-system.md mục 6 – Dialog 480 px; hành động phá hủy luôn có xác nhận).
 * Phát `true` khi người dùng bấm nút xác nhận; Esc, nút đóng hoặc "Hủy bỏ" → `false`.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmDialog {
  private readonly dialogs = inject(DialogService);

  confirm(options: ConfirmOptions): Observable<boolean> {
    const confirmAction = {
      text: options.confirmLabel,
      themeColor: options.danger ? ('error' as const) : ('primary' as const),
    };
    const ref = this.dialogs.open({
      title: options.title,
      content: options.message,
      width: 480,
      actions: [{ text: 'Hủy bỏ' }, confirmAction],
    });
    return ref.result.pipe(map((result) => !(result instanceof DialogCloseResult) && result === confirmAction));
  }
}
