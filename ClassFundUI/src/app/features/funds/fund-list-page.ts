import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { fundShortName } from '../../core/navigation/fund-short-name';
import { ROLE_DISPLAY } from '../../core/navigation/role-display';
import { VndMoneyPipe } from '../../shared/pipes/vnd-money.pipe';
import { TopBar } from '../account/top-bar';
import { FundsApi } from './funds.api';
import { FundSummary, displaySchoolYear } from './funds.models';

/**
 * F-01 Quỹ của tôi (FR-08): màn đầu tiên sau đăng nhập, chưa có menu quỹ. Chọn một quỹ mới vào khung có menu.
 * Danh sách + vai trò lấy ngay từ `GET /auth/me` (M1); M2 bổ sung số liệu thẻ từ `GET /funds`: năm học, số thành viên,
 * số dư quỹ chung, số tiền mình chưa nộp, số dư cá nhân, trạng thái đã tổng kết.
 */
@Component({
  selector: 'app-fund-list-page',
  imports: [RouterLink, TopBar, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-top-bar variant="funds" />
    <main class="page">
      <div class="head">
        <div>
          <h1>Quỹ của tôi</h1>
          <p class="subtitle">Các quỹ bạn đang tham gia. Vai trò được tính riêng cho từng quỹ.</p>
        </div>
        @if (canCreateFund()) {
          <a routerLink="/funds/new" class="create">+ Tạo quỹ</a>
        }
      </div>

      @if (funds().length > 0) {
        <ul class="grid" aria-label="Danh sách quỹ">
          @for (fund of funds(); track fund.fundId) {
            <li>
              <a
                class="card"
                [class.archived]="fund.summary?.status === 'Archived'"
                [routerLink]="['/funds', fund.fundId]"
                [attr.aria-label]="'Mở quỹ ' + fund.fundName"
              >
                <span class="top">
                  <span class="logo" aria-hidden="true">{{ fund.short }}</span>
                  <span class="text">
                    <b class="name">{{ fund.fundName }}</b>
                    @if (fund.summary; as summary) {
                      <span class="meta">{{ year(summary.schoolYear) }} · {{ summary.memberCount }} thành viên</span>
                    }
                  </span>
                  <span class="badge" [class]="'badge badge--' + fund.role">{{ fund.display.badge }}</span>
                </span>
                <!-- Số liệu M2 (GET /funds); chưa tải xong hoặc lỗi thì thẻ vẫn mở được quỹ. -->
                @if (fund.summary; as summary) {
                  @if (summary.status === 'Archived') {
                    <span class="bottom inline">
                      <span class="tag">Đã tổng kết</span>
                      <span class="meta">Đã quyết toán cuối năm – chỉ xem</span>
                    </span>
                  } @else {
                    <span class="bottom">
                      <span class="kpi">
                        <span class="meta">Số dư quỹ chung</span>
                        <b class="money">{{ summary.generalBalance | vndMoney }}</b>
                      </span>
                      <span class="kpi">
                        <span class="meta">Bạn chưa nộp</span>
                        <b class="money" [class.owed]="summary.myDueAmount > 0">{{ summary.myDueAmount | vndMoney }}</b>
                        @if (summary.myDueCount > 0) {
                          <span class="meta">{{ summary.myDueCount }} khoản</span>
                        }
                      </span>
                      <span class="kpi">
                        <span class="meta">Số dư cá nhân</span>
                        <b class="money">{{ summary.myBalance | vndMoney }}</b>
                      </span>
                    </span>
                  }
                }
              </a>
            </li>
          }
        </ul>
      } @else {
        <section class="empty" aria-labelledby="empty-title">
          <b id="empty-title">Bạn chưa thuộc quỹ nào</b>
          <p>Thủ quỹ sẽ thêm bạn bằng email, hoặc tự tạo quỹ nếu bạn đăng ký làm thủ quỹ.</p>
          @if (canCreateFund()) {
            <a routerLink="/funds/new" class="cf-link-button">Tạo quỹ đầu tiên</a>
          }
        </section>
      }
    </main>
  `,
  styles: `
    :host {
      display: block;
      min-height: 100dvh;
      background: var(--cf-background);
    }
    .page {
      box-sizing: border-box;
      width: 100%;
      max-width: 1152px;
      margin: 0 auto;
      padding: 32px 24px 48px;
    }
    .head {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 16px;
      margin-bottom: 24px;
    }
    h1 {
      font-size: 24px;
    }
    .subtitle {
      margin: 4px 0 0;
      color: var(--cf-text-muted);
    }
    .create {
      display: inline-flex;
      align-items: center;
      min-height: 40px;
      padding: 0 16px;
      border-radius: var(--cf-radius-lg);
      background: var(--cf-ink);
      color: var(--cf-surface);
      font-weight: 600;
      text-decoration: none;
    }
    .create:hover {
      background: var(--cf-ink-hover);
      color: var(--cf-surface);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
      gap: 16px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .card {
      display: flex;
      flex-direction: column;
      gap: 16px;
      box-sizing: border-box;
      padding: 20px;
      border: 1px solid var(--cf-border);
      border-radius: 12px;
      background: var(--cf-surface);
      color: var(--cf-text);
      text-decoration: none;
    }
    .card:hover {
      border-color: var(--cf-border-strong);
      box-shadow: 0 1px 3px rgba(15, 23, 42, 0.08);
      color: var(--cf-text);
    }
    .card.archived {
      background: var(--cf-background);
    }
    .top {
      display: flex;
      align-items: flex-start;
      gap: 12px;
    }
    .meta {
      display: block;
      color: var(--cf-text-muted);
      font-size: 13px;
    }
    .bottom {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 12px;
      padding-top: 16px;
      border-top: 1px solid var(--cf-subtle);
    }
    .bottom.inline {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .kpi {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .money {
      font-variant-numeric: tabular-nums;
    }
    .owed {
      color: var(--cf-warning-fg);
    }
    .tag {
      padding: 0 8px;
      border-radius: var(--cf-radius-pill);
      background: var(--cf-subtle);
      font-size: 12px;
      font-weight: 600;
      line-height: 22px;
    }
    .archived .logo {
      background: var(--cf-subtle);
      color: var(--cf-text-muted);
    }
    .logo {
      display: flex;
      flex: none;
      align-items: center;
      justify-content: center;
      width: 40px;
      height: 40px;
      border-radius: 8px;
      background: var(--cf-ink);
      color: var(--cf-surface);
      font-size: 11px;
      font-weight: 700;
    }
    .text {
      flex: 1;
      min-width: 0;
    }
    .name {
      display: block;
      overflow: hidden;
      font-size: 16px;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .badge {
      flex: none;
      padding: 0 10px;
      border-radius: var(--cf-radius-pill);
      font-size: 12px;
      font-weight: 600;
      line-height: 24px;
    }
    .badge--Owner {
      background: var(--cf-ink);
      color: var(--cf-surface);
    }
    .badge--Treasurer {
      background: var(--cf-role-treasurer-tint);
      color: var(--cf-role-treasurer);
    }
    .badge--Student {
      background: var(--cf-subtle);
      color: var(--cf-text);
    }
    .empty {
      padding: 32px 16px;
      border: 1px dashed var(--cf-border-strong);
      border-radius: 12px;
      text-align: center;
    }
    .empty p {
      margin: 8px 0 16px;
      color: var(--cf-text-muted);
    }
  `,
})
export class FundListPage {
  private readonly auth = inject(AuthService);
  private readonly api = inject(FundsApi);

  protected readonly year = displaySchoolYear;
  protected readonly canCreateFund = this.auth.canCreateFund;

  /** Số liệu thẻ theo mã quỹ (M2). Lỗi → rỗng: thẻ vẫn hiện tên + vai trò từ phiên. */
  private readonly summaries = toSignal(
    this.api.listMine().pipe(
      map((items) => new Map(items.map((item): [string, FundSummary] => [item.id, item]))),
      catchError(() => of(new Map<string, FundSummary>())),
    ),
    { initialValue: new Map<string, FundSummary>() },
  );

  protected readonly funds = computed(() =>
    (this.auth.user()?.memberships ?? []).map((item) => ({
      ...item,
      display: ROLE_DISPLAY[item.role],
      short: fundShortName(item.fundName),
      summary: this.summaries().get(item.fundId) ?? null,
    })),
  );
}
