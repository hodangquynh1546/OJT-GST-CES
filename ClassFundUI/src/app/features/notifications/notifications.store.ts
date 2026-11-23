import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { NotificationsApi } from './notifications.api';

/** Số thông báo chưa đọc của quỹ đang mở (chuông ở thanh trên, mục menu); lỗi mạng thì giữ số cũ. */
@Injectable({ providedIn: 'root' })
export class NotificationsStore {
  private readonly api = inject(NotificationsApi);

  readonly unread = signal(0);

  async refresh(fundId: string): Promise<void> {
    try {
      this.unread.set((await firstValueFrom(this.api.unreadCount(fundId))).count);
    } catch {
      // Chuông chỉ là gợi ý; không làm phiền người dùng khi lỗi tạm thời.
    }
  }

  set(count: number): void {
    this.unread.set(Math.max(0, count));
  }

  reset(): void {
    this.unread.set(0);
  }
}
