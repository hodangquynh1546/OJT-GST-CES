import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { FundsApi } from '../funds/funds.api';

/** Trạng thái quỹ đang mở (đã tổng kết cuối năm hay chưa) cho banner "Quỹ đã tổng kết"; lỗi mạng thì giữ trạng thái cũ. */
@Injectable({ providedIn: 'root' })
export class FundStatusStore {
  private readonly api = inject(FundsApi);

  readonly archived = signal(false);

  async refresh(fundId: string): Promise<void> {
    try {
      this.archived.set((await firstValueFrom(this.api.get(fundId))).status === 'Archived');
    } catch {
      // Banner chỉ là thông tin phụ; không báo lỗi.
    }
  }

  reset(): void {
    this.archived.set(false);
  }
}
