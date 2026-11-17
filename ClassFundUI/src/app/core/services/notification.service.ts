import { Injectable, inject } from '@angular/core';
import { NotificationService } from '@progress/kendo-angular-notification';

/** Thông báo ngắn góc dưới phải, tự tắt sau 4 giây (AppState – Success). */
@Injectable({ providedIn: 'root' })
export class AppNotifications {
  private readonly notifications = inject(NotificationService);

  success(content: string): void {
    this.show(content, 'success');
  }

  warning(content: string): void {
    this.show(content, 'warning');
  }

  error(content: string): void {
    this.show(content, 'error');
  }

  private show(content: string, style: 'success' | 'warning' | 'error'): void {
    this.notifications.show({
      content,
      hideAfter: 4000,
      position: { horizontal: 'right', vertical: 'bottom' },
      animation: { type: 'fade', duration: 200 },
      type: { style, icon: true },
    });
  }
}
