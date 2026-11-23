import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { firstValueFrom, map } from 'rxjs';

import { Permissions } from '../../../core/auth/permissions';
import { CurrentFund } from '../../../core/services/current-fund.service';
import { AppNotifications } from '../../../core/services/notification.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { ScopeBadge } from '../../../shared/components/scope-badge/scope-badge';
import { ApiError } from '../../../shared/models/api-error';
import { VnDatePipe } from '../../../shared/pipes/vn-date.pipe';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import '../../events/events.errors';
import { SETTLEMENT_LINE_LABEL, SURPLUS_METHOD_LABEL, SettlementLine, SurplusMethod } from '../../events/events.models';
import { FundStatusStore } from '../fund-status.store';
import { YearEndApi } from '../year-end.api';
import { YearEndPreview, YearEndResult } from '../year-end.models';

/**
 * S-05 · Tổng kết cuối năm quỹ chung (FR-53, FR-54). Làm một lần: chia tiền thừa cho cả lớp theo tỷ lệ số đã đóng vào các khoản thu chung,
 * hoàn trực tiếp hoặc cộng vào số dư; xong quỹ chuyển "Đã tổng kết" (chỉ xem).
 */
@Component({
  selector: 'app-year-end-page',
  imports: [AppState, ButtonComponent, DialogActionsComponent, DialogComponent, FormsModule, RouterLink, ScopeBadge, VnDatePipe, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="head-t">
        <div class="title-row">
          <h1>Tổng kết cuối năm</h1>
          <app-scope-badge scope="General" />
        </div>
        @if (year(); as value) {
          <span class="cf-muted">Năm học {{ value }}</span>
        }
      </div>

      @if (error(); as message) {
        <app-state kind="error" [message]="message" />
      } @else if (loading()) {
        <app-state kind="loading" />
      } @else if (result(); as done) {
        <div class="cf-alert cf-alert--success">
          Đã tổng kết lúc <b>{{ done.settledAt | vnDate }}</b> bởi {{ done.settledByName }}. Quỹ đã chuyển sang chỉ xem.
          @if (done.surplusMethod) { Cách xử lý tiền thừa: <b>{{ methodLabel[done.surplusMethod] }}</b>. }
        </div>
        <div class="kpis">
          <div class="cf-card kpi"><span class="kpi-l">Thành viên</span><span class="kpi-v">{{ done.memberCount }}</span></div>
          <div class="cf-card kpi"><span class="kpi-l">Tổng thu quỹ chung</span><span class="kpi-v cf-income">{{ done.totalCollected | vndMoney }}</span></div>
          <div class="cf-card kpi"><span class="kpi-l">Tổng chi quỹ chung</span><span class="kpi-v cf-expense">{{ done.totalSpent | vndMoney }}</span></div>
          <div class="cf-card kpi"><span class="kpi-l">Tiền thừa</span><span class="kpi-v">{{ done.surplus | vndMoney }}</span></div>
        </div>
        <section class="cf-card" aria-label="Kết quả chia tiền thừa">
          <div class="card-h"><h2>Kết quả chia tiền thừa</h2></div>
          @if (done.lines.length) {
            <table class="grid">
              <thead><tr><th>Thành viên</th><th class="num">Đã đóng cả năm</th><th class="num">Được nhận lại</th><th>Trạng thái</th><th></th></tr></thead>
              <tbody>
                @for (line of done.lines; track line.userId) {
                  <tr>
                    <td>{{ line.fullName }}</td>
                    <td class="num">{{ line.contributed | vndMoney }}</td>
                    <td class="num strong">{{ line.amount | vndMoney }}</td>
                    <td>@if (line.status) { <span class="cf-tag" [class.cf-tag--success]="line.status !== 'PendingRefund'" [class.cf-tag--warning]="line.status === 'PendingRefund'">{{ lineLabel[line.status] }}</span> }</td>
                    <td class="right">
                      @if (line.status === 'PendingRefund' && line.id) {
                        <button kendoButton size="small" themeColor="primary" type="button" [disabled]="busy()" (click)="refund(line)">Đã hoàn</button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="cf-muted pad">{{ done.surplus === 0 ? 'Thu chi vừa đủ, không có tiền thừa để chia.' : 'Không có phần nào dành cho bạn.' }}</p>
          }
        </section>
      } @else if (preview(); as plan) {
        <div class="kpis">
          <div class="cf-card kpi"><span class="kpi-l">Thành viên đang hoạt động</span><span class="kpi-v">{{ plan.memberCount }}</span></div>
          <div class="cf-card kpi"><span class="kpi-l">Tổng thu quỹ chung</span><span class="kpi-v cf-income">{{ plan.totalCollected | vndMoney }}</span></div>
          <div class="cf-card kpi"><span class="kpi-l">Tổng chi quỹ chung</span><span class="kpi-v cf-expense">{{ plan.totalSpent | vndMoney }}</span></div>
          <div class="cf-card kpi"><span class="kpi-l">Tiền thừa</span><span class="kpi-v">{{ plan.surplus | vndMoney }}</span></div>
        </div>
        <p class="cf-muted">Không gồm tiền của các sự kiện – mỗi sự kiện đã quyết toán riêng.</p>

        @if (plan.unsettledEventCount > 0) {
          <div class="cf-alert cf-alert--danger">
            Còn <b>{{ plan.unsettledEventCount }}</b> sự kiện chưa quyết toán hoặc chưa hủy. Hãy xử lý hết rồi mới tổng kết cuối năm.
            <a [routerLink]="['/funds', fundId(), 'events']">Mở danh sách sự kiện</a>
          </div>
        }
        @if (plan.pendingPaymentCount > 0) {
          <div class="cf-alert cf-alert--danger">
            Còn <b>{{ plan.pendingPaymentCount }}</b> thanh toán chờ xác nhận. Hãy xác nhận hoặc từ chối hết.
            <a [routerLink]="['/funds', fundId(), 'payments']">Mở hàng chờ xác nhận</a>
          </div>
        }
        @if (plan.unpaidObligationCount > 0) {
          <div class="cf-alert">
            Có <b>{{ plan.unpaidObligationCount }}</b> khoản chưa đóng ở {{ plan.openChargeCount }} khoản thu chung còn mở; chúng được đóng lại khi tổng kết, không truy thu và không trừ vào phần của người khác.
          </div>
        }
        <div class="cf-alert cf-alert--warning">
          <b>Chỉ làm một lần.</b> Quỹ sẽ đóng: không tạo khoản thu, giao dịch hay sự kiện mới. Vẫn xem báo cáo, rút số dư cá nhân và xác nhận hoàn tiền.
        </div>

        @if (plan.surplus > 0) {
          <section class="cf-card pad stack" aria-label="Cách xử lý tiền thừa">
            <h2>Cách xử lý tiền thừa</h2>
            <p class="cf-muted">Tiền thừa chia cho cả lớp theo tỷ lệ tổng số tiền mỗi người đã đóng vào các khoản thu chung cả năm.</p>
            <div class="methods" role="radiogroup" aria-label="Cách xử lý tiền thừa">
              @for (option of methodOptions; track option) {
                <label class="method">
                  <input type="radio" name="method" [value]="option" [ngModel]="method()" (ngModelChange)="method.set($event)" />
                  <span>
                    <b>{{ methodLabel[option] }}</b>
                    <span class="cf-muted">{{ option === 'DirectRefund' ? 'Thủ quỹ trả tiền cho từng người rồi bấm "Đã hoàn"; ghi chi vào sổ quỹ chung.' : 'Cộng ngay vào số dư cá nhân, rút được sau khi quỹ đã đóng.' }}</span>
                  </span>
                </label>
              }
            </div>
          </section>
        }

        <section class="cf-card" aria-label="Bảng chia tiền thừa">
          <div class="card-h"><h2>Chia theo tỷ lệ đã đóng</h2></div>
          <table class="grid">
            <thead><tr><th>Thành viên</th><th class="num">Đã đóng cả năm</th><th class="num">Tỷ lệ</th><th class="num">Được nhận lại</th></tr></thead>
            <tbody>
              @for (line of plan.lines; track line.userId) {
                <tr>
                  <td>{{ line.fullName }}</td>
                  <td class="num">{{ line.contributed | vndMoney }}</td>
                  <td class="num cf-muted">{{ ratio(line) }}</td>
                  <td class="num strong">{{ line.amount | vndMoney }}</td>
                </tr>
              }
            </tbody>
          </table>
        </section>

        <div class="actions">
          <button kendoButton themeColor="primary" type="button" [disabled]="!ready() || busy()" (click)="confirming.set(true)">Tổng kết cuối năm</button>
        </div>
      }
    </div>

    @if (confirming()) {
      <kendo-dialog title="Tổng kết cuối năm?" [width]="500" [maxWidth]="'calc(100vw - 32px)'" (close)="confirming.set(false)">
        <p class="cf-muted">
          <b>Chỉ làm một lần.</b> Quỹ chuyển sang "Đã tổng kết" và không quay lại được.
          @if (preview(); as plan) {
            @if (plan.surplus > 0) {
              Tiền thừa <b>{{ plan.surplus | vndMoney }}</b> được chia cho cả lớp theo tỷ lệ đã đóng và
              {{ method() === 'PersonalBalance' ? 'cộng ngay vào số dư cá nhân.' : 'chờ bạn hoàn trực tiếp.' }}
            } @else {
              Thu chi vừa đủ, không có tiền thừa.
            }
          }
        </p>
        <kendo-dialog-actions layout="end">
          <button kendoButton type="button" (click)="confirming.set(false)">Để sau</button>
          <button kendoButton themeColor="error" type="button" [disabled]="busy()" (click)="settle()">Tổng kết và đóng quỹ</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }
  `,
  styles: `
    .page { display: flex; flex-direction: column; gap: 20px; max-width: var(--cf-content-max); }
    .head-t { display: flex; flex-direction: column; gap: 4px; }
    .title-row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .title-row h1 { margin: 0; font-weight: 600; letter-spacing: -0.02em; }
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; }
    .kpi { display: flex; flex-direction: column; gap: 4px; padding: 16px; }
    .kpi-l { color: var(--cf-text-muted); font-size: 13px; }
    .kpi-v { font-size: 20px; font-weight: 600; font-variant-numeric: tabular-nums; }
    .card-h { padding: 16px 20px; border-bottom: 1px solid var(--cf-border); }
    .card-h h2, .stack h2 { margin: 0; font-size: 16px; }
    .pad { padding: 20px; }
    .stack { display: flex; flex-direction: column; gap: 12px; }
    .methods { display: grid; gap: 8px; }
    .method { display: flex; align-items: flex-start; gap: 10px; padding: 10px 12px; border: 1px solid var(--cf-border); border-radius: var(--cf-radius-lg); cursor: pointer; }
    .method span { display: flex; flex-direction: column; }
    .grid { width: 100%; border-collapse: collapse; }
    .grid th, .grid td { padding: 12px 20px; border-top: 1px solid var(--cf-border); text-align: left; }
    .grid thead th { border-top: 0; color: var(--cf-text-muted); font-size: 13px; font-weight: 500; }
    .num { text-align: right !important; font-variant-numeric: tabular-nums; }
    .right { text-align: right !important; }
    .strong { font-weight: 600; }
    .actions { display: flex; justify-content: flex-end; }
  `,
})
export class YearEndPage {
  private readonly api = inject(YearEndApi);
  private readonly route = inject(ActivatedRoute);
  private readonly notifications = inject(AppNotifications);
  private readonly currentFund = inject(CurrentFund);
  private readonly fundStatus = inject(FundStatusStore);

  protected readonly methodLabel = SURPLUS_METHOD_LABEL;
  protected readonly lineLabel = SETTLEMENT_LINE_LABEL;
  protected readonly methodOptions: SurplusMethod[] = ['DirectRefund', 'PersonalBalance'];

  protected readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), { initialValue: '' });
  protected readonly canExecute = computed(() => this.currentFund.can(Permissions.SettlementExecute));

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly confirming = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly preview = signal<YearEndPreview | null>(null);
  protected readonly result = signal<YearEndResult | null>(null);
  protected readonly method = signal<SurplusMethod | null>(null);
  protected readonly year = computed(() => this.result()?.schoolYear ?? this.preview()?.schoolYear ?? null);

  protected readonly ready = computed(() => {
    const plan = this.preview();
    return !!plan && plan.canExecute && (plan.surplus === 0 || this.method() !== null);
  });

  constructor() {
    void this.load();
  }

  protected ratio(line: SettlementLine): string {
    const total = this.preview()?.lines.reduce((sum, item) => sum + item.contributed, 0) ?? 0;
    return total > 0 ? `${Math.round((line.contributed / total) * 1000) / 10}%` : '–';
  }

  private currentFundId(): string {
    return this.fundId() || this.route.snapshot.paramMap.get('fundId') || '';
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    const fundId = this.currentFundId();
    try {
      this.result.set(await firstValueFrom(this.api.result(fundId)));
    } catch (error) {
      this.result.set(null);
      if (!(error instanceof ApiError && error.status === 404)) {
        this.error.set(error instanceof ApiError ? error.message : 'Không tải được dữ liệu tổng kết.');
      } else if (this.canExecute()) {
        try {
          this.preview.set(await firstValueFrom(this.api.preview(fundId)));
        } catch (inner) {
          this.error.set(inner instanceof ApiError ? inner.message : 'Không tải được số liệu tổng kết.');
        }
      } else {
        this.error.set('Quỹ chưa tổng kết cuối năm.');
      }
    } finally {
      this.loading.set(false);
    }
  }

  protected async settle(): Promise<void> {
    const plan = this.preview();
    if (!plan || this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      this.result.set(await firstValueFrom(this.api.settle(this.currentFundId(), plan.surplus > 0 ? this.method() : null)));
      this.confirming.set(false);
      void this.fundStatus.refresh(this.currentFundId());
      this.notifications.success('Đã tổng kết cuối năm. Quỹ chuyển sang chỉ xem; thao tác đã ghi vào nhật ký hoạt động.');
    } catch (error) {
      this.confirming.set(false);
      this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  protected async refund(line: SettlementLine): Promise<void> {
    if (!line.id || this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      await firstValueFrom(this.api.confirmRefund(this.currentFundId(), line.id));
      this.notifications.success(`Đã ghi nhận hoàn ${line.fullName}.`);
    } catch (error) {
      this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      this.busy.set(false);
      await this.load();
    }
  }
}
