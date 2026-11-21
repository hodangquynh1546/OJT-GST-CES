import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { CellTemplateDirective, ColumnComponent, GridComponent, NoRecordsTemplateDirective } from '@progress/kendo-angular-grid';
import { BehaviorSubject, combineLatest, firstValueFrom, map, switchMap } from 'rxjs';

import { AppNotifications } from '../../../core/services/notification.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { ApiError } from '../../../shared/models/api-error';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { BalanceApi } from '../balance.api';
import '../balance.errors';
import { MyBalance, historySource, shortTime } from '../balance.models';

/**
 * B-01 · Số dư cá nhân (FR-40 → FR-42) theo canvas BalanceBody: số dư, trạng thái yêu cầu rút, nút "Rút toàn bộ",
 * số dư ở quỹ khác, lịch sử biến động. Rút luôn là rút toàn bộ, mỗi lúc một yêu cầu (OQ-11).
 */
@Component({
  selector: 'app-balance-page',
  imports: [
    AppState,
    ButtonComponent,
    CellTemplateDirective,
    ColumnComponent,
    DialogActionsComponent,
    DialogComponent,
    GridComponent,
    NoRecordsTemplateDirective,
    RouterLink,
    VndMoneyPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './balance-page.html',
  styleUrl: './balance-page.scss',
})
export class BalancePage {
  private readonly api = inject(BalanceApi);
  private readonly route = inject(ActivatedRoute);
  private readonly notifications = inject(AppNotifications);
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  protected readonly time = shortTime;
  protected readonly source = historySource;
  protected readonly confirming = signal(false);
  protected readonly busy = signal(false);

  protected readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), { initialValue: '' });

  protected readonly state = toSignal(
    combineLatest([this.route.paramMap, this.reload$]).pipe(switchMap(([params]) => toLoadState(this.api.mine(params.get('fundId') ?? '')))),
    { initialValue: LOADING as LoadState<MyBalance> },
  );
  protected readonly kind = computed(() => stateKind(this.state(), () => false));
  protected readonly data = computed(() => {
    const current = this.state();
    return current.status === 'ready' ? current.data : null;
  });
  protected readonly pending = computed(() => this.data()?.latestWithdrawal?.status === 'Requested');

  /** Nhãn nút như canvas: "Rút toàn bộ X", "Đang chờ thủ quỹ trả tiền", "Không còn số dư". */
  protected readonly withdrawLabel = computed(() => {
    const data = this.data();
    if (!data) {
      return '';
    }
    if (this.pending()) {
      return 'Đang chờ thủ quỹ trả tiền';
    }
    return data.balance > 0 ? `Rút toàn bộ ${new Intl.NumberFormat('vi-VN').format(data.balance)} đ` : 'Không còn số dư';
  });

  protected async withdraw(): Promise<void> {
    this.busy.set(true);
    try {
      await firstValueFrom(this.api.requestWithdrawal(this.fundId()));
      this.confirming.set(false);
      this.notifications.success('Đã gửi yêu cầu rút. Thủ quỹ sẽ liên hệ trả tiền rồi xác nhận.');
      this.reload$.next();
    } catch (error) {
      this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
      this.confirming.set(false);
      this.reload$.next();
    } finally {
      this.busy.set(false);
    }
  }
}
