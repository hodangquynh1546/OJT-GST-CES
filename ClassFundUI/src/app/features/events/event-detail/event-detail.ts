import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { CellTemplateDirective, ColumnComponent, GridComponent, NoRecordsTemplateDirective } from '@progress/kendo-angular-grid';
import { ErrorComponent, FormFieldComponent, TextAreaComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { BehaviorSubject, combineLatest, filter, firstValueFrom, map, of, switchMap } from 'rxjs';

import { Permissions } from '../../../core/auth/permissions';
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
import { EventFormDialog } from '../event-form-dialog/event-form-dialog';
import { EventsApi } from '../events.api';
import '../events.errors';
import {
  CANCEL_REASON_MAX,
  CompletionPreview,
  EVENT_STATUS_BADGE,
  EventDetail as EventDetailModel,
  EventParticipant,
  EventParticipants,
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
 * Thủ quỹ: tiến trình, KPI, tab Người tham gia (lọc tình trạng đóng) · Khoản thu sự kiện · Thu chi sự kiện (ghi chi ≤ số dư),
 * Kết thúc (xem trước người bị loại / được nhắc, BR-19), Hủy (khi chưa có tiền). Học sinh: trả lời tham gia, khoản của mình, chi công khai.
 */
@Component({
  selector: 'app-event-detail',
  imports: [
    AppState,
    AttachmentGallery,
    ButtonComponent,
    CellTemplateDirective,
    ColumnComponent,
    DialogActionsComponent,
    DialogComponent,
    ErrorComponent,
    EventFormDialog,
    ExpenseDialog,
    FormFieldComponent,
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
  private readonly route = inject(ActivatedRoute);
  private readonly notifications = inject(AppNotifications);
  private readonly currentFund = inject(CurrentFund);
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  protected readonly statusBadge = EVENT_STATUS_BADGE;
  protected readonly paymentBadge = PAYMENT_STATUS_BADGE;
  protected readonly assignmentLabel = ASSIGNMENT_STATUS_LABEL;
  protected readonly assignmentTone = ASSIGNMENT_TONE;
  protected readonly signed = signedAmount;
  protected readonly longDate = longDate;
  protected readonly reasonMax = CANCEL_REASON_MAX;
  protected readonly peopleFilters: readonly { value: PeopleFilter; label: string }[] = [
    { value: 'all', label: 'Tất cả' },
    { value: 'Paid', label: 'Đã đóng' },
    { value: 'Pending', label: 'Chờ xác nhận' },
    { value: 'Unpaid', label: 'Chưa đóng' },
  ];

  protected readonly canManage = computed(() => this.currentFund.can(Permissions.EventManage));
  protected readonly canConfirm = computed(() => this.currentFund.can(Permissions.PaymentConfirm));
  protected readonly tab = signal<DetailTab>('people');
  protected readonly peopleFilter = signal<PeopleFilter>('all');
  protected readonly busy = signal(false);
  protected readonly dialog = signal<'end' | 'cancel' | 'expense' | 'edit' | null>(null);
  protected readonly preview = signal<CompletionPreview | null>(null);
  protected readonly proof = signal<LedgerEntryDetail | null>(null);
  protected readonly cancelReason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(CANCEL_REASON_MAX)] });

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
  /** Bước hiện tại của tiến trình: 0 đang mở, 1 đã kết thúc, 2 đã quyết toán. */
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
    return value === 'all' ? data.total : value === 'Paid' ? data.paidCount : value === 'Pending' ? data.pendingCount : data.unpaidCount;
  }

  /** Cột Ghi chú như canvas: thiếu bao nhiêu, đang chờ, sẽ bị loại khi kết thúc. */
  protected note(person: EventParticipant): string {
    if (person.paymentStatus === 'Pending') {
      return `${person.pendingPaymentCount} thanh toán chờ xác nhận`;
    }
    if (person.paymentStatus !== 'Unpaid') {
      return '';
    }
    if (person.paid === 0 && this.isOpen()) {
      return 'Chưa đóng khoản nào – bị loại khi kết thúc';
    }
    const missing = `Thiếu ${new Intl.NumberFormat('vi-VN').format(Math.max(0, person.due - person.paid))} đ`;
    return this.isOpen() ? missing : `${missing} · vẫn được đóng`;
  }

  /** `yyyy-MM-dd` → "17/11" như cột Ngày của canvas. */
  protected shortDate(value: string): string {
    const [, month, day] = value.split('-');
    return `${day}/${month}`;
  }

  protected names(people: { fullName: string }[]): string {
    return people.map((person) => person.fullName).join(' · ');
  }

  /** Canvas: "Phạm Đức Long (thiếu 80.000 đ) và 4 người khác". */
  protected underpaidText(plan: CompletionPreview): string {
    const money = new Intl.NumberFormat('vi-VN');
    const shown = plan.underpaid.slice(0, 3).map((person) => `${person.fullName} (thiếu ${money.format(person.missing)} đ)`);
    const rest = plan.underpaid.length - shown.length;
    return rest > 0 ? `${shown.join(', ')} và ${rest} người khác` : shown.join(', ');
  }

  protected badgeOf(status: ParticipantPaymentStatus): { text: string; tone: Tone } {
    return this.paymentBadge[status];
  }

  // ---------- Thao tác thủ quỹ ----------

  protected async askEnd(event: EventDetailModel): Promise<void> {
    this.preview.set(null);
    this.dialog.set('end');
    try {
      this.preview.set(await firstValueFrom(this.api.completionPreview(this.fundId(), event.id)));
    } catch (error) {
      this.dialog.set(null);
      this.notifications.error(error instanceof ApiError ? error.message : 'Không tải được danh sách. Vui lòng thử lại.');
    }
  }

  protected async end(event: EventDetailModel): Promise<void> {
    await this.run(async () => {
      const result = await firstValueFrom(this.api.complete(this.fundId(), event.id));
      this.dialog.set(null);
      this.peopleFilter.set('all');
      this.notifications.success(
        `Đã kết thúc sự kiện: còn ${result.remainingCount} người. Đã loại ${result.excluded.length} người và nhắc ${result.underpaid.length} người chưa đóng đủ.`,
      );
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
