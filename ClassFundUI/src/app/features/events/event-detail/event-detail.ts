import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { CellTemplateDirective, ColumnComponent, GridComponent, NoRecordsTemplateDirective } from '@progress/kendo-angular-grid';
import { ErrorComponent, FormFieldComponent, TextAreaComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { BehaviorSubject, combineLatest, filter, firstValueFrom, map, of, switchMap } from 'rxjs';

import { Permissions } from '../../../core/auth/permissions';
import { ConfirmDialog } from '../../../core/services/confirm-dialog.service';
import { AppNotifications } from '../../../core/services/notification.service';
import { CurrentFund } from '../../../core/services/current-fund.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { ScopeBadge } from '../../../shared/components/scope-badge/scope-badge';
import { ApiError } from '../../../shared/models/api-error';
import { ASSIGNMENT_STATUS_LABEL, AssignmentStatus } from '../../../shared/models/labels';
import { VnDatePipe } from '../../../shared/pipes/vn-date.pipe';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { AttachmentGallery } from '../../ledger/attachment-gallery/attachment-gallery';
import { ExpenseDialog } from '../../ledger/expense-dialog/expense-dialog';
import { LedgerApi } from '../../ledger/ledger.api';
import { LedgerEntry, LedgerEntryDetail, LedgerList, signedAmount } from '../../ledger/ledger.models';
import { CategoriesApi } from '../../categories/categories.api';
import { Category } from '../../categories/categories.models';
import { ChargeCollectList } from '../../charges/charge-collect-list';
import { CHARGE_STATUS_LABEL, ChargeStatus } from '../../charges/charges.models';
import { EventFormDialog } from '../event-form-dialog/event-form-dialog';
import { EventsApi } from '../events.api';
import '../events.errors';
import '../../charges/charges.errors';
import {
  CANCEL_REASON_MAX,
  CloseListPreview,
  EventCharge,
  EVENT_STATUS_BADGE,
  EventDetail as EventDetailModel,
  EventParticipant,
  EventParticipants,
  MY_RESPONSE_BADGE,
  PAYMENT_STATUS_BADGE,
  ParticipantPaymentStatus,
  ParticipationResponse,
  Tone,
  longDate,
} from '../events.models';

type DetailTab = 'people' | 'charges' | 'ledger';
type PeopleFilter = 'all' | Exclude<ParticipantPaymentStatus, 'NoCharge'>;

const ASSIGNMENT_TONE: Record<AssignmentStatus, Tone> = {
  Unpaid: 'danger',
  AwaitingConfirmation: 'warning',
  Paid: 'success',
  Excluded: 'neutral',
  Cancelled: 'neutral',
};

/**
 * E-03 · Chi tiết sự kiện (FR-31 → FR-34) theo canvas Event (thủ quỹ) và StuEventDetail (học sinh).
 * Thủ quỹ: tiến trình, KPI, tab Người tham gia · Khoản thu sự kiện (tích "đã thu", nhắc đóng) · Thu chi sự kiện (chỉ ghi chi sau khi đóng danh sách),
 * "Đóng danh sách" (xem trước người bị loại và tiền hoàn vào số dư, BR-19), lịch sử đóng danh sách, Hủy (khi chưa có tiền).
 * Học sinh: trả lời tham gia, khoản của mình (chỉ xem), chi công khai.
 */
@Component({
  selector: 'app-event-detail',
  imports: [
    AppState,
    AttachmentGallery,
    ButtonComponent,
    ChargeCollectList,
    CellTemplateDirective,
    ColumnComponent,
    DialogActionsComponent,
    DialogComponent,
    ErrorComponent,
    EventFormDialog,
    ExpenseDialog,
    FormFieldComponent,
    FormsModule,
    GridComponent,
    LabelComponent,
    NoRecordsTemplateDirective,
    ReactiveFormsModule,
    RouterLink,
    ScopeBadge,
    TextAreaComponent,
    VnDatePipe,
    VndMoneyPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './event-detail.html',
  styleUrl: './event-detail.scss',
})
export class EventDetail {
  private readonly api = inject(EventsApi);
  private readonly ledgerApi = inject(LedgerApi);
  private readonly categoryApi = inject(CategoriesApi);
  private readonly route = inject(ActivatedRoute);
  private readonly notifications = inject(AppNotifications);
  private readonly currentFund = inject(CurrentFund);
  private readonly confirm = inject(ConfirmDialog);
  protected readonly reload$ = new BehaviorSubject<void>(undefined);

  protected readonly statusBadge = EVENT_STATUS_BADGE;
  protected readonly paymentBadge = PAYMENT_STATUS_BADGE;
  protected readonly myResponseBadge = MY_RESPONSE_BADGE;
  protected readonly assignmentLabel = ASSIGNMENT_STATUS_LABEL;
  protected readonly assignmentTone = ASSIGNMENT_TONE;
  protected readonly signed = signedAmount;
  protected readonly longDate = longDate;
  protected readonly reasonMax = CANCEL_REASON_MAX;
  protected readonly peopleFilters: readonly { value: PeopleFilter; label: string }[] = [
    { value: 'all', label: 'Tất cả' },
    { value: 'Paid', label: 'Đã nộp' },
    { value: 'Unpaid', label: 'Chưa nộp' },
  ];

  protected readonly canManage = computed(() => this.currentFund.can(Permissions.EventManage));
  protected readonly canRemind = computed(() => this.currentFund.can(Permissions.ChargeManage));
  protected readonly tab = signal<DetailTab>('people');
  protected readonly peopleFilter = signal<PeopleFilter>('all');
  protected readonly busy = signal(false);
  protected readonly dialog = signal<'end' | 'cancel' | 'expense' | 'edit' | 'charge' | null>(null);
  protected readonly preview = signal<CloseListPreview | null>(null);
  protected readonly confirmEarly = signal(false);
  /** Khoản thu sự kiện đang mở danh sách người nộp (thủ quỹ tích "đã thu"). */
  protected readonly tracked = signal<string | null>(null);
  protected readonly proof = signal<LedgerEntryDetail | null>(null);
  protected readonly cancelReason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(CANCEL_REASON_MAX)] });

  // ---- Tạo khoản thu sự kiện ----
  protected readonly chargeTitle = signal('');
  protected readonly chargePurpose = signal('');
  protected readonly chargeAmount = signal(0);
  protected readonly chargeAmountText = computed(() => (this.chargeAmount() > 0 ? this.chargeAmount().toLocaleString('vi-VN') : ''));
  protected readonly chargeCategoryId = signal('');
  protected readonly chargeCategories = signal<Category[]>([]);
  protected readonly chargeErrors = signal<{ title?: string; category?: string; amount?: string }>({});
  protected readonly chargeCreateError = signal<string | null>(null);

  protected readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), { initialValue: '' });
  protected readonly eventId = toSignal(this.route.paramMap.pipe(map((params) => params.get('eventId') ?? '')), { initialValue: '' });

  protected readonly state = toSignal(
    combineLatest([this.route.paramMap, this.reload$]).pipe(
      switchMap(([params]) => toLoadState(this.api.get(params.get('fundId') ?? '', params.get('eventId') ?? ''))),
    ),
    { initialValue: LOADING as LoadState<EventDetailModel> },
  );
  protected readonly kind = computed(() => stateKind(this.state(), () => false));
  protected readonly event = computed(() => {
    const current = this.state();
    return current.status === 'ready' ? current.data : null;
  });

  /** Thủ quỹ: danh sách người tham gia kèm tiền; học sinh không cần (chỉ hiện số người). */
  private readonly participantsState = toSignal(
    combineLatest([toObservable(this.event), this.reload$]).pipe(
      filter(([event]) => event !== null),
      switchMap(([event]) =>
        this.canManage() ? toLoadState(this.api.participants(this.fundId(), event!.id)) : of<LoadState<EventParticipants>>(LOADING),
      ),
    ),
    { initialValue: LOADING as LoadState<EventParticipants> },
  );
  protected readonly participants = computed(() => {
    const current = this.participantsState();
    return current.status === 'ready' ? current.data : null;
  });
  protected readonly visiblePeople = computed(() => {
    const items = this.participants()?.items ?? [];
    const wanted = this.peopleFilter();
    return wanted === 'all' ? items : items.filter((item) => item.paymentStatus === wanted);
  });

  /** Sổ thu chi sự kiện: thủ quỹ thấy cả thu (tab Thu chi sự kiện); học sinh đã tham gia thấy khoản chi công khai. */
  private readonly ledgerState = toSignal(
    combineLatest([toObservable(this.event), this.reload$]).pipe(
      filter(([event]) => event !== null),
      switchMap(([event]) =>
        this.canManage() || event!.me?.response === 'Accepted'
          ? toLoadState(
              this.ledgerApi.list(this.fundId(), { eventId: event!.id, type: this.canManage() ? null : 'Expense', page: 1, pageSize: 100 }),
            )
          : of<LoadState<LedgerList>>(LOADING),
      ),
    ),
    { initialValue: LOADING as LoadState<LedgerList> },
  );
  protected readonly ledger = computed(() => {
    const current = this.ledgerState();
    return current.status === 'ready' ? current.data : null;
  });

  protected readonly isOpen = computed(() => this.event()?.status === 'Open');
  protected readonly isCompleted = computed(() => this.event()?.status === 'Completed');
  /** Bước hiện tại của tiến trình: 0 đang mở, 1 đã đóng danh sách, 2 đã quyết toán. */
  protected readonly step = computed(() => ({ Open: 0, Completed: 1, Settled: 2, Cancelled: -1 })[this.event()?.status ?? 'Open']);

  protected dotClass(index: number): string {
    const step = this.step();
    return index < step || step === 2 ? 'dot done' : index === step ? 'dot cur' : 'dot';
  }

  protected setPeopleFilter(value: PeopleFilter): void {
    this.peopleFilter.set(value);
  }

  protected count(value: PeopleFilter): number {
    const data = this.participants();
    if (!data) {
      return 0;
    }
    return value === 'all' ? data.total : value === 'Paid' ? data.paidCount : data.unpaidCount;
  }

  /** Cột Ghi chú: thiếu bao nhiêu, hoặc sẽ bị loại khi đóng danh sách. */
  protected note(person: EventParticipant): string {
    if (person.paymentStatus !== 'Unpaid') {
      return '';
    }
    if (person.paid === 0 && this.isOpen()) {
      return 'Chưa nộp khoản nào – bị loại khi đóng danh sách';
    }
    const missing = `Thiếu ${new Intl.NumberFormat('vi-VN').format(Math.max(0, person.due - person.paid))} đ`;
    return this.isOpen() ? `${missing} – bị loại khi đóng danh sách, tiền đã nộp hoàn vào số dư` : missing;
  }

  /** `yyyy-MM-dd` → "17/11" như cột Ngày của canvas. */
  protected shortDate(value: string): string {
    const [, month, day] = value.split('-');
    return `${day}/${month}`;
  }

  protected badgeOf(status: ParticipantPaymentStatus): { text: string; tone: Tone } {
    return this.paymentBadge[status];
  }

  // ---------- Thao tác thủ quỹ ----------

  protected async askCloseList(event: EventDetailModel): Promise<void> {
    this.preview.set(null);
    this.confirmEarly.set(false);
    this.dialog.set('end');
    try {
      this.preview.set(await firstValueFrom(this.api.closeListPreview(this.fundId(), event.id)));
    } catch (error) {
      this.dialog.set(null);
      this.notifications.error(error instanceof ApiError ? error.message : 'Không tải được danh sách. Vui lòng thử lại.');
    }
  }

  protected async closeList(event: EventDetailModel): Promise<void> {
    await this.run(async () => {
      const result = await firstValueFrom(this.api.closeList(this.fundId(), event.id, this.confirmEarly()));
      this.dialog.set(null);
      this.peopleFilter.set('all');
      this.notifications.success(`Đã đóng danh sách: còn ${result.remainingCount} người, loại ${result.excluded.length} người (hoàn ${new Intl.NumberFormat('vi-VN').format(result.totalRefund)} đ vào số dư).`);
    });
  }

  protected askCancel(): void {
    this.cancelReason.reset('');
    this.dialog.set('cancel');
  }

  protected async cancel(event: EventDetailModel): Promise<void> {
    const reason = this.cancelReason.value.trim();
    if (!reason) {
      this.cancelReason.setErrors({ required: true });
      this.cancelReason.markAsTouched();
      return;
    }
    await this.run(async () => {
      await firstValueFrom(this.api.cancel(this.fundId(), event.id, reason));
      this.dialog.set(null);
      this.notifications.success('Đã hủy sự kiện. Thao tác đã ghi vào nhật ký hoạt động.');
    });
  }

  /** "Nhắc đóng" từng người (TD-18): thông báo riêng cho người còn thiếu; không tải lại trang. */
  protected async remind(person: EventParticipant): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      await firstValueFrom(this.api.remindParticipant(this.fundId(), this.eventId(), person.userId));
      this.notifications.success(`Đã nhắc ${person.fullName} đóng tiền.`);
    } catch (error) {
      this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      this.busy.set(false);
    }
  }

  protected onChanged(): void {
    this.dialog.set(null);
    this.reload$.next();
  }

  // ---------- Học sinh ----------

  protected async respond(event: EventDetailModel, response: Exclude<ParticipationResponse, 'Pending'>): Promise<void> {
    if (event.me?.response === response) {
      return;
    }
    await this.run(async () => {
      await firstValueFrom(this.api.respond(this.fundId(), event.id, response));
      this.notifications.success(response === 'Accepted' ? 'Đã xác nhận tham gia.' : 'Đã ghi nhận không tham gia.');
    });
  }

  protected async showProof(entry: LedgerEntry): Promise<void> {
    try {
      this.proof.set(await firstValueFrom(this.ledgerApi.get(this.fundId(), entry.id)));
    } catch (error) {
      this.notifications.error(error instanceof ApiError ? error.message : 'Không tải được chứng từ.');
    }
  }

  // ---- Khoản thu sự kiện (M6b) ----

  protected chargeStatusLabel(status: ChargeStatus): string {
    return CHARGE_STATUS_LABEL[status];
  }

  protected setChargeAmount(text: string): void {
    const digits = text.replace(/\D/g, '').slice(0, 12);
    this.chargeAmount.set(digits ? Number(digits) : 0);
  }

  protected openCreateCharge(): void {
    this.chargeTitle.set('');
    this.chargePurpose.set('');
    this.chargeAmount.set(0);
    this.chargeCategoryId.set('');
    this.chargeErrors.set({});
    this.chargeCreateError.set(null);
    this.dialog.set('charge');
    void firstValueFrom(this.categoryApi.list(this.fundId(), { type: 'Income', activeOnly: true })).then((items) =>
      this.chargeCategories.set(items),
    );
  }

  protected async doCreateCharge(event: EventDetailModel): Promise<void> {
    const errors: { title?: string; category?: string; amount?: string } = {};
    if (!this.chargeTitle().trim()) errors.title = 'Vui lòng nhập tên khoản thu.';
    if (!this.chargeCategoryId()) errors.category = 'Vui lòng chọn danh mục thu.';
    if (this.chargeAmount() <= 0) errors.amount = 'Số tiền mỗi người phải lớn hơn 0.';
    this.chargeErrors.set(errors);
    this.chargeCreateError.set(null);
    if (Object.keys(errors).length > 0 || this.busy()) return;

    await this.run(async () => {
      const charge = await firstValueFrom(
        this.api.createCharge(this.fundId(), event.id, {
          title: this.chargeTitle().trim(),
          purpose: this.chargePurpose().trim() || null,
          amountPerPerson: this.chargeAmount(),
          dueDate: null,
          categoryId: this.chargeCategoryId(),
        }),
      );
      this.dialog.set(null);
      this.notifications.success(`Đã tạo khoản thu sự kiện "${charge.title}".`);
    });
  }

  protected toggleTracked(chargeId: string): void {
    this.tracked.update((current) => (current === chargeId ? null : chargeId));
  }

  /** Cảnh báo khi còn người chưa nộp: đóng khoản thu thì không tích "đã thu" thêm được nữa. */
  protected askCloseCharge(event: EventDetailModel, charge: EventCharge): void {
    this.confirm
      .confirm({
        title: 'Đóng khoản thu sự kiện',
        message:
          charge.unpaidCount > 0
            ? `Còn ${charge.unpaidCount} người chưa nộp khoản “${charge.title}”. Sau khi đóng khoản thu bạn không tích “đã thu” thêm được nữa. Vẫn đóng khoản thu?`
            : `Đóng khoản “${charge.title}”? Mọi người đã nộp đủ.`,
        confirmLabel: 'Đóng khoản thu',
      })
      .subscribe((confirmed) => confirmed && void this.doCloseCharge(event, charge.id));
  }

  private async doCloseCharge(event: EventDetailModel, chargeId: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.closeCharge(this.fundId(), event.id, chargeId));
      this.notifications.success('Đã đóng khoản thu sự kiện.');
    });
  }

  protected async doCancelCharge(event: EventDetailModel, chargeId: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.cancelCharge(this.fundId(), event.id, chargeId));
      this.notifications.success('Đã hủy khoản thu sự kiện.');
    });
  }

  private async run(work: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await work();
      this.reload$.next();
    } catch (error) {
      this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
      this.reload$.next();
    } finally {
      this.busy.set(false);
    }
  }
}
