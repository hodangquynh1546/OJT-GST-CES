import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { ErrorComponent, FormFieldComponent, TextAreaComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { BehaviorSubject, combineLatest, firstValueFrom, map, switchMap } from 'rxjs';

import { Permissions } from '../../../core/auth/permissions';
import { AppNotifications } from '../../../core/services/notification.service';
import { CurrentFund } from '../../../core/services/current-fund.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { ScopeBadge } from '../../../shared/components/scope-badge/scope-badge';
import { ApiError } from '../../../shared/models/api-error';
import { VnDatePipe } from '../../../shared/pipes/vn-date.pipe';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, stateKind, toLoadState } from '../../../shared/state/load-state';
import { actionLabel, formatAuditTime } from '../../audit/audit-format';
import { AttachmentGallery } from '../attachment-gallery/attachment-gallery';
import { LedgerApi } from '../ledger.api';
import '../ledger.errors';
import { MEMBER_ROLE_LABEL, REVERSE_REASON_MAX, signedAmount } from '../ledger.models';

/** L-03 · Chi tiết giao dịch + chứng từ + bút toán đảo (FR-27, FR-29) theo canvas LedgerDetail. */
@Component({
  selector: 'app-ledger-detail',
  imports: [
    AppState,
    AttachmentGallery,
    ButtonComponent,
    DialogActionsComponent,
    DialogComponent,
    ErrorComponent,
    FormFieldComponent,
    LabelComponent,
    ReactiveFormsModule,
    RouterLink,
    ScopeBadge,
    TextAreaComponent,
    VnDatePipe,
    VndMoneyPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let current = state();
    @if (kind(); as stateKindValue) {
      <app-state [kind]="stateKindValue" />
    } @else if (current.status === 'ready') {
      @let detail = current.data;
      @let entry = detail.entry;
      <div class="page">
        <div class="head">
          <div class="head-t">
            @if (entry.eventId) {
              <a class="back" [routerLink]="['/funds', fundId(), 'events', entry.eventId]">← Sự kiện {{ entry.eventName }}</a>
            } @else {
              <a class="back" [routerLink]="['/funds', fundId(), 'ledger']">← {{ ledgerTitle() }}</a>
            }
            <div class="title-row">
              <h1>{{ entry.title }}</h1>
              <app-scope-badge [scope]="entry.eventId ? 'Event' : 'General'" [eventName]="entry.eventName" />
              @if (entry.reversesEntryId) {
                <span class="cf-tag">Bút toán đảo</span>
              } @else if (entry.reversedByEntryId) {
                <span class="cf-tag cf-tag--danger">Đã đảo</span>
              } @else {
                <span class="cf-tag cf-tag--success">Hiệu lực</span>
              }
            </div>
          </div>
          @if (detail.canReverse) {
            <button kendoButton fillMode="outline" themeColor="error" type="button" (click)="openReverse()">Tạo bút toán đảo</button>
          }
        </div>

        <div class="split">
          <section class="cf-card pad grow stack">
            <dl class="meta">
              <dt>Loại</dt>
              <dd>
                <span class="cf-tag" [class.cf-tag--success]="entry.type === 'Income'" [class.cf-tag--danger]="entry.type === 'Expense'">
                  {{ entry.type === 'Income' ? 'Thu' : 'Chi' }}
                </span>
              </dd>
              <dt>Số tiền</dt>
              <dd class="amount" [class.cf-income]="entry.type === 'Income'" [class.cf-expense]="entry.type === 'Expense'">
                {{ signed(entry) | vndMoney: true }}
              </dd>
              @if (detail.purpose) {
                <dt>Mục đích</dt>
                <dd>{{ detail.purpose }}</dd>
              }
              <dt>Danh mục</dt>
              <dd>{{ entry.categoryName ?? '–' }}</dd>
              <dt>Ngày giao dịch</dt>
              <dd>{{ entry.transactionDate | vnDate }}</dd>
              @if (detail.payerName) {
                <dt>Thu từ</dt>
                <dd>{{ detail.payerName }}</dd>
              }
              <dt>Người thực hiện</dt>
              <dd>{{ entry.performedByName }}@if (detail.performedByRole; as role) { ({{ roleLabel[role] }})}</dd>
              <dt>Ghi lúc</dt>
              <dd>{{ entry.createdAt | vnDate }} {{ time(entry.createdAt) }}</dd>
              @if (detail.note) {
                <dt>{{ entry.reversesEntryId ? 'Lý do đảo' : 'Ghi chú' }}</dt>
                <dd>{{ detail.note }}</dd>
              }
            </dl>

            @if (detail.reverses; as original) {
              <div class="cf-alert">
                Bút toán đảo của
                <a [routerLink]="['/funds', fundId(), 'ledger', original.id]">“{{ original.title }}”</a>
                ngày {{ original.transactionDate | vnDate }}. Dòng gốc giữ nguyên trong sổ.
              </div>
            }

            @if (entry.source === 'Manual' && !entry.reversesEntryId) {
              <div class="stack-s">
                <h3>Chứng từ ({{ detail.attachments.length }})</h3>
                @if (detail.attachments.length) {
                  <app-attachment-gallery [fundId]="fundId()" [attachments]="detail.attachments" />
                } @else {
                  <p class="cf-muted small">Không có chứng từ (khoản chi ≤ 500.000 đ được phép không kèm chứng từ).</p>
                }
                <span class="k-form-hint">File lưu trên Neon Object Storage, chỉ tải qua ứng dụng có kiểm quyền.</span>
              </div>
            }

            @if (detail.reversedBy; as reversal) {
              <div class="cf-alert cf-alert--danger">
                Đã đảo bởi
                <a [routerLink]="['/funds', fundId(), 'ledger', reversal.id]">bút toán ngày {{ reversal.transactionDate | vnDate }}</a>
                ({{ reversal.amount | vndMoney: true }}). Lý do: “{{ reversal.reason }}”. Dòng gốc giữ nguyên trong sổ.
              </div>
            }
          </section>

          @if (detail.history.length) {
            <section class="cf-card pad aside stack history" aria-label="Lịch sử thao tác">
              <h2>Lịch sử thao tác</h2>
              @for (item of detail.history; track $index) {
                <div class="stack-xs">
                  <span class="strong">{{ label(item.action) }}</span>
                  <span class="cf-muted xs">{{ item.actorName }} · {{ time(item.at) }}</span>
                </div>
              }
              @if (canAudit()) {
                <a class="small" [routerLink]="['/funds', fundId(), 'audit']">Xem trong nhật ký hoạt động →</a>
              }
            </section>
          }
        </div>
      </div>

      @if (reversing()) {
        <kendo-dialog title="Tạo bút toán đảo" [width]="520" [maxWidth]="'calc(100vw - 32px)'" (close)="reversing.set(false)">
          <div class="stack">
            <p class="cf-muted">
              Sổ thu chi không sửa/xóa được. Hệ thống tạo dòng <strong>Thu {{ entry.amount | vndMoney: true }}</strong> cùng phạm vi để triệt tiêu
              dòng này. Một giao dịch chỉ đảo được một lần.
            </p>
            <kendo-formfield class="field cf-required">
              <kendo-label [for]="reasonInput" text="Lý do" />
              <kendo-textarea #reasonInput [formControl]="reason" [rows]="3" resizable="vertical" [maxlength]="reasonMax" />
              <kendo-formerror>Vui lòng nhập lý do.</kendo-formerror>
            </kendo-formfield>
          </div>
          <kendo-dialog-actions layout="end">
            <button kendoButton type="button" (click)="reversing.set(false)">Hủy</button>
            <button kendoButton themeColor="error" type="button" [disabled]="busy() || !reasonText()" (click)="reverse(entry.id)">
              {{ busy() ? 'Đang đảo…' : 'Đảo giao dịch' }}
            </button>
          </kendo-dialog-actions>
        </kendo-dialog>
      }
    }
  `,
  // Theo canvas L-03 (LedgerDetail.dc.html + cf.css); màu dùng token --cf-*.
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 24px;
      max-width: var(--cf-content-max);
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      justify-content: space-between;
      gap: 16px;
    }
    .head-t {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .back {
      color: var(--cf-text-muted);
      font-size: 13px;
      text-decoration: none;
    }
    .back:hover {
      color: var(--cf-ink);
    }
    h1 {
      font-weight: 600;
      letter-spacing: -0.02em;
    }
    h2 {
      margin: 0;
      font-size: 16px;
      line-height: 24px;
      font-weight: 600;
    }
    h3 {
      margin: 0;
      font-size: 14px;
      line-height: 20px;
      font-weight: 600;
    }
    p {
      margin: 0;
    }
    .small {
      font-size: 13px;
    }
    .xs {
      font-size: 12px;
    }
    .title-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .split {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: 16px;
    }
    .grow {
      flex: 999 1 560px;
      min-width: 0;
    }
    .aside {
      flex: 1 1 320px;
      min-width: 0;
    }
    .pad {
      padding: 20px;
    }
    .stack {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .history {
      gap: 12px;
    }
    .stack-s {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .stack-xs {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .strong {
      font-weight: 500;
    }
    dl.meta {
      display: grid;
      grid-template-columns: 150px 1fr;
      gap: 10px 16px;
      margin: 0;
    }
    dl.meta dt {
      color: var(--cf-text-muted);
    }
    dl.meta dd {
      margin: 0;
    }
    .amount {
      font-weight: 600;
      font-variant-numeric: tabular-nums;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin: 0;
    }
  `,
})
export class LedgerDetail {
  private readonly api = inject(LedgerApi);
  private readonly route = inject(ActivatedRoute);
  private readonly notifications = inject(AppNotifications);
  private readonly currentFund = inject(CurrentFund);
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  protected readonly roleLabel = MEMBER_ROLE_LABEL;
  protected readonly reasonMax = REVERSE_REASON_MAX;
  protected readonly signed = signedAmount;
  protected readonly label = actionLabel;
  protected readonly canAudit = computed(() => this.currentFund.can(Permissions.AuditView));
  protected readonly ledgerTitle = computed(() => (this.currentFund.can(Permissions.LedgerManage) ? 'Thu chi chung' : 'Chi tiêu'));

  protected readonly reversing = signal(false);
  protected readonly busy = signal(false);
  protected readonly reason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(REVERSE_REASON_MAX)] });
  private readonly reasonValue = toSignal(this.reason.valueChanges, { initialValue: '' });
  protected readonly reasonText = computed(() => this.reasonValue().trim());

  protected readonly fundId = toSignal(
    this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')),
    { initialValue: '' },
  );

  protected readonly state = toSignal(
    combineLatest([this.route.paramMap, this.reload$]).pipe(
      switchMap(([params]) => toLoadState(this.api.get(params.get('fundId') ?? '', params.get('entryId') ?? ''))),
    ),
    { initialValue: LOADING },
  );

  protected readonly kind = computed(() => stateKind(this.state(), () => false));

  protected time(value: string): string {
    return formatAuditTime(value).split(' ')[1] ?? '';
  }

  protected openReverse(): void {
    this.reason.reset('');
    this.reversing.set(true);
  }

  protected async reverse(entryId: string): Promise<void> {
    const reason = this.reasonText();
    if (!reason) {
      this.reason.markAsTouched();
      return;
    }
    this.busy.set(true);
    try {
      await firstValueFrom(this.api.reverse(this.fundId(), entryId, reason));
      this.reversing.set(false);
      this.notifications.success('Đã tạo bút toán đảo. Dòng gốc giữ nguyên trong sổ.');
      this.reload$.next();
    } catch (error) {
      this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
      if (error instanceof ApiError && error.code === 'ledger.already_reversed') {
        this.reversing.set(false);
        this.reload$.next();
      }
    } finally {
      this.busy.set(false);
    }
  }

}
