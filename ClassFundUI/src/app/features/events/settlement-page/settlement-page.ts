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
import { EventsApi } from '../events.api';
import '../events.errors';
import {
  SETTLEMENT_LINE_LABEL,
  SURPLUS_METHOD_LABEL,
  SettlementLine,
  SettlementPreview,
  SettlementResult,
  SurplusMethod,
} from '../events.models';

/**
 * E-04 · Quyết toán sự kiện (FR-35 → FR-39). Thủ quỹ: số liệu tổng hợp, chọn cách trả tiền thừa, bảng chia theo tỷ lệ đã đóng,
 * quyết toán một lần; hoàn trực tiếp thì đánh dấu "Đã hoàn" từng người. Học sinh tham gia: xem phần của mình sau khi quyết toán.
 */
@Component({
  selector: 'app-settlement-page',
  imports: [AppState, ButtonComponent, DialogActionsComponent, DialogComponent, FormsModule, RouterLink, ScopeBadge, VnDatePipe, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="head">
        <div class="head-t">
          <a class="back" [routerLink]="['/funds', fundId(), 'events', eventId()]">← Về sự kiện</a>
          <div class="title-row">
            <h1>Quyết toán sự kiện</h1>
            @if (name(); as eventName) {
              <app-scope-badge scope="Event" [eventName]="eventName" />
            }
          </div>
        </div>
      </div>

      @if (error(); as message) {
        <app-state kind="error" [message]="message" />
      } @else if (loading()) {
        <app-state kind="loading" />
      } @else if (result(); as done) {
        <div class="cf-alert cf-alert--success">
          Đã quyết toán lúc <b>{{ done.settledAt | vnDate }}</b> bởi {{ done.settledByName }}.
          @if (done.surplusMethod) { Cách xử lý tiền thừa: <b>{{ methodLabel[done.surplusMethod] }}</b>. }
        </div>
        <div class="kpis">
          <div class="cf-card kpi"><span class="kpi-l">Người tham gia</span><span class="kpi-v">{{ done.participantCount }}</span></div>
          <div class="cf-card kpi"><span class="kpi-l">Tổng thu</span><span class="kpi-v cf-income">{{ done.totalCollected | vndMoney }}</span></div>
          <div class="cf-card kpi"><span class="kpi-l">Tổng chi</span><span class="kpi-v cf-expense">{{ done.totalSpent | vndMoney }}</span></div>
          <div class="cf-card kpi"><span class="kpi-l">Tiền thừa</span><span class="kpi-v">{{ done.surplus | vndMoney }}</span></div>
        </div>
        <section class="cf-card" [attr.aria-label]="canExecute() ? 'Kết quả chia tiền thừa' : 'Phần của bạn'">
          <div class="card-h"><h2>{{ canExecute() ? 'Kết quả chia tiền thừa' : 'Phần của bạn' }}</h2></div>
          @if (done.lines.length) {
            <table class="grid">
              <thead>
                <tr><th>Người tham gia</th><th class="num">Đã đóng</th><th class="num">Được nhận lại</th><th>Trạng thái</th>@if (canExecute()) { <th></th> }</tr>
              </thead>
              <tbody>
                @for (line of done.lines; track line.userId) {
                  <tr>
                    <td>{{ line.fullName }}</td>
                    <td class="num">{{ line.contributed | vndMoney }}</td>
                    <td class="num strong">{{ line.amount | vndMoney }}</td>
                    <td>@if (line.status) { <span class="cf-tag" [class.cf-tag--success]="line.status !== 'PendingRefund'" [class.cf-tag--warning]="line.status === 'PendingRefund'">{{ lineLabel[line.status] }}</span> }</td>
                    @if (canExecute()) {
                      <td class="right">
                        @if (line.status === 'PendingRefund' && line.id) {
                          <button kendoButton size="small" themeColor="primary" type="button" [disabled]="busy()" (click)="refund(line)">Đã hoàn</button>
                        }
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="cf-muted pad">{{ done.surplus === 0 ? 'Thu chi vừa đủ, không có tiền thừa để chia.' : 'Bạn không có phần nào trong lần quyết toán này.' }}</p>
          }
        </section>
      } @else if (preview(); as plan) {
        <div class="kpis">
          <div class="cf-card kpi"><span class="kpi-l">Người tham gia</span><span class="kpi-v">{{ plan.participantCount }}</span></div>
          <div class="cf-card kpi"><span class="kpi-l">Tổng thu</span><span class="kpi-v cf-income">{{ plan.totalCollected | vndMoney }}</span></div>
          <div class="cf-card kpi"><span class="kpi-l">Tổng chi</span><span class="kpi-v cf-expense">{{ plan.totalSpent | vndMoney }}</span></div>
          <div class="cf-card kpi"><span class="kpi-l">Tiền thừa</span><span class="kpi-v">{{ plan.surplus | vndMoney }}</span></div>
        </div>

        @if (plan.status !== 'Completed') {
          <div class="cf-alert cf-alert--warning">Chỉ quyết toán được khi sự kiện đã kết thúc.</div>
        }
        @if (plan.pendingPaymentCount > 0) {
          <div class="cf-alert cf-alert--danger">
            Còn <b>{{ plan.pendingPaymentCount }}</b> thanh toán chờ xác nhận. Hãy xác nhận hoặc từ chối hết rồi mới quyết toán.
            <a [routerLink]="['/funds', fundId(), 'payments']">Mở hàng chờ xác nhận</a>
          </div>
        }
        @if (plan.unpaidObligationCount > 0) {
          <div class="cf-alert">
            Có <b>{{ plan.unpaidObligationCount }}</b> khoản chưa đóng sẽ được đóng lại khi quyết toán; không truy thu và không trừ vào phần của người khác.
          </div>
        }

        @if (plan.surplus > 0) {
          <section class="cf-card pad stack" aria-label="Cách xử lý tiền thừa">
            <h2>Cách xử lý tiền thừa</h2>
            <p class="cf-muted">Tiền thừa luôn chia theo tỷ lệ số tiền mỗi người đã đóng cho sự kiện.</p>
            <div class="methods" role="radiogroup" aria-label="Cách xử lý tiền thừa">
              @for (option of methodOptions; track option) {
                <label class="method">
                  <input type="radio" name="method" [value]="option" [ngModel]="method()" (ngModelChange)="method.set($event)" />
                  <span>
                    <b>{{ methodLabel[option] }}</b>
                    <span class="cf-muted">{{ option === 'DirectRefund' ? 'Thủ quỹ trả tiền cho từng người rồi bấm "Đã hoàn"; ghi chi vào sổ sự kiện.' : 'Cộng ngay vào số dư cá nhân, dùng đóng khoản thu sau hoặc rút.' }}</span>
                  </span>
                </label>
              }
            </div>
          </section>
        }

        <section class="cf-card" aria-label="Bảng chia tiền thừa">
          <div class="card-h"><h2>Chia theo tỷ lệ đã đóng</h2></div>
          <table class="grid">
            <thead>
              <tr><th>Người tham gia</th><th class="num">Đã đóng</th><th class="num">Tỷ lệ</th><th class="num">Được nhận lại</th></tr>
            </thead>
            <tbody>
              @for (line of plan.lines; track line.userId) {
                <tr>
                  <td>{{ line.fullName }}</td>
                  <td class="num">{{ line.contributed | vndMoney }}</td>
                  <td class="num cf-muted">{{ ratio(line) }}</td>
                  <td class="num strong">{{ line.amount | vndMoney }}</td>
                </tr>
              } @empty {
                <tr><td colspan="4" class="cf-muted center">Chưa có người tham gia.</td></tr>
              }
            </tbody>
          </table>
        </section>

        <div class="actions">
          <button kendoButton themeColor="primary" type="button" [disabled]="!ready() || busy()" (click)="confirming.set(true)">Quyết toán sự kiện</button>
        </div>
      }
    </div>

    @if (confirming()) {
      <kendo-dialog title="Quyết toán sự kiện?" [width]="480" [maxWidth]="'calc(100vw - 32px)'" (close)="confirming.set(false)">
        <p class="cf-muted">
          Sự kiện chuyển sang "Đã quyết toán" và không quay lại được.
          @if (preview(); as plan) {
            @if (plan.surplus > 0) {
              Tiền thừa <b>{{ plan.surplus | vndMoney }}</b> được chia cho {{ plan.lines.length }} người theo tỷ lệ đã đóng và
              {{ method() === 'PersonalBalance' ? 'cộng ngay vào số dư cá nhân.' : 'chờ bạn hoàn trực tiếp.' }}
            } @else {
              Thu chi vừa đủ, không có tiền thừa.
            }
          }
        </p>
        <kendo-dialog-actions layout="end">
          <button kendoButton type="button" (click)="confirming.set(false)">Để sau</button>
          <button kendoButton themeColor="primary" type="button" [disabled]="busy()" (click)="settle()">Quyết toán</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }
  `,
  styles: `
    .page { display: flex; flex-direction: column; gap: 24px; max-width: var(--cf-content-max); }
    .head-t { display: flex; flex-direction: column; gap: 4px; }
    .back { color: var(--cf-text-muted); font-size: 13px; text-decoration: none; }
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
    .center { text-align: center !important; }
    .strong { font-weight: 600; }
    .actions { display: flex; justify-content: flex-end; }
  `,
})
export class SettlementPage {
  private readonly api = inject(EventsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly notifications = inject(AppNotifications);
  private readonly currentFund = inject(CurrentFund);

  protected readonly methodLabel = SURPLUS_METHOD_LABEL;
  protected readonly lineLabel = SETTLEMENT_LINE_LABEL;
  protected readonly methodOptions: SurplusMethod[] = ['DirectRefund', 'PersonalBalance'];

  protected readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), { initialValue: '' });
  protected readonly eventId = toSignal(this.route.paramMap.pipe(map((params) => params.get('eventId') ?? '')), { initialValue: '' });
  protected readonly canExecute = computed(() => this.currentFund.can(Permissions.SettlementExecute));

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly confirming = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly preview = signal<SettlementPreview | null>(null);
  protected readonly result = signal<SettlementResult | null>(null);
  protected readonly method = signal<SurplusMethod | null>(null);
  protected readonly name = computed(() => this.result()?.eventName ?? this.preview()?.eventName ?? null);

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

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    const fundId = this.fundId() || this.route.snapshot.paramMap.get('fundId') || '';
    const eventId = this.eventId() || this.route.snapshot.paramMap.get('eventId') || '';
    try {
      this.result.set(await firstValueFrom(this.api.settlement(fundId, eventId)));
    } catch (error) {
      this.result.set(null);
      const notSettled = error instanceof ApiError && error.status === 404;
      if (!notSettled) {
        this.error.set(error instanceof ApiError ? error.message : 'Không tải được dữ liệu quyết toán.');
      } else if (this.canExecute()) {
        try {
          this.preview.set(await firstValueFrom(this.api.settlementPreview(fundId, eventId)));
        } catch (inner) {
          this.error.set(inner instanceof ApiError ? inner.message : 'Không tải được số liệu quyết toán.');
        }
      } else {
        this.error.set('Sự kiện này chưa được quyết toán.');
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
      this.result.set(await firstValueFrom(this.api.settle(this.fundId(), this.eventId(), plan.surplus > 0 ? this.method() : null)));
      this.confirming.set(false);
      this.notifications.success('Đã quyết toán sự kiện. Thao tác đã ghi vào nhật ký hoạt động.');
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
      await firstValueFrom(this.api.confirmRefund(this.fundId(), this.eventId(), line.id));
      this.notifications.success(`Đã ghi nhận hoàn ${line.fullName}.`);
    } catch (error) {
      this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      this.busy.set(false);
      await this.load();
    }
  }
}
