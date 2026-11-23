import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { CellTemplateDirective, ColumnComponent, GridComponent, NoRecordsTemplateDirective } from '@progress/kendo-angular-grid';
import { switchMap } from 'rxjs';

import { AppState } from '../../../shared/components/app-state/app-state';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { BalanceApi } from '../balance.api';
import '../balance.errors';
import { MyBalance, historySource, shortTime } from '../balance.models';

/**
 * B-01 · Số dư cá nhân (FR-40 → FR-42), chỉ xem: số dư và lịch sử biến động. Học sinh không tự rút;
 * thủ quỹ rút giúp ở "Rút số dư" (B-02).
 */
@Component({
  selector: 'app-balance-page',
  imports: [AppState, CellTemplateDirective, ColumnComponent, GridComponent, NoRecordsTemplateDirective, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './balance-page.html',
  styleUrl: './balance-page.scss',
})
export class BalancePage {
  private readonly api = inject(BalanceApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly time = shortTime;
  protected readonly source = historySource;

  protected readonly state = toSignal(
    this.route.paramMap.pipe(switchMap((params) => toLoadState(this.api.mine(params.get('fundId') ?? '')))),
    { initialValue: LOADING as LoadState<MyBalance> },
  );
  protected readonly kind = computed(() => stateKind(this.state(), () => false));
  protected readonly data = computed(() => {
    const current = this.state();
    return current.status === 'ready' ? current.data : null;
  });
}
