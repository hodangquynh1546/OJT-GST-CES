import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DatePickerComponent } from '@progress/kendo-angular-dateinputs';
import { combineLatest, map, of, switchMap } from 'rxjs';

import { AppState } from '../../../shared/components/app-state/app-state';
import { ScopeBadge } from '../../../shared/components/scope-badge/scope-badge';
import { ASSIGNMENT_STATUS_LABEL } from '../../../shared/models/labels';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { toApiDate } from '../../audit/audit-format';
import { EVENT_STATUS_BADGE } from '../../events/events.models';
import { ReportsApi } from '../reports.api';
import { EventReportRow, GeneralReport, StudentReportRow, percent, plain, surplusHandling, toCsv } from '../reports.models';

type ReportTab = 'general' | 'events' | 'students';

/** Mặc định như canvas: từ đầu năm học (01/09) tới hôm nay. */
function schoolYearStart(today = new Date()): Date {
  const year = today.getMonth() >= 8 ? today.getFullYear() : today.getFullYear() - 1;
  return new Date(year, 8, 1);
}

/**
 * S-02 · Báo cáo (FR-49 → FR-51) theo canvas Reports: tab Thu chi chung (KPI, chi theo danh mục, số dư theo tháng),
 * Sự kiện (mỗi sự kiện một dòng), Học sinh. Quỹ chung và sự kiện luôn tách riêng (BR-02). "Xuất Excel" xuất tab đang xem ra CSV.
 */
@Component({
  selector: 'app-reports-page',
  imports: [AppState, ButtonComponent, DatePickerComponent, FormsModule, RouterLink, ScopeBadge, VndMoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reports-page.html',
  styleUrl: './reports-page.scss',
})
export class ReportsPage {
  private readonly api = inject(ReportsApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly statusBadge = EVENT_STATUS_BADGE;
  /** Nhãn ngắn trong bảng báo cáo như canvas. */
  protected readonly shortStatus: Record<string, string> = { Open: 'Đang mở', Completed: 'Chờ quyết toán', Settled: 'Đã quyết toán', Cancelled: 'Đã hủy' };
  protected readonly assignmentLabel = ASSIGNMENT_STATUS_LABEL;
  protected readonly plain = plain;
  protected readonly percent = percent;
  protected readonly handling = surplusHandling;
  protected readonly tabs: readonly { value: ReportTab; label: string }[] = [
    { value: 'general', label: 'Thu chi chung' },
    { value: 'events', label: 'Sự kiện' },
    { value: 'students', label: 'Học sinh' },
  ];

  protected readonly tab = signal<ReportTab>((this.route.snapshot.data['tab'] as ReportTab | undefined) ?? 'general');
  protected readonly from = signal<Date | null>(schoolYearStart());
  protected readonly to = signal<Date | null>(new Date(new Date().getFullYear(), new Date().getMonth(), new Date().getDate()));
  protected readonly maxFrom = computed(() => this.to() ?? new Date(2100, 0, 1));
  protected readonly minTo = computed(() => this.from() ?? new Date(2000, 0, 1));

  protected readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), { initialValue: '' });

  protected readonly general = toSignal(
    combineLatest([toObservable(this.fundId), toObservable(this.from), toObservable(this.to)]).pipe(
      switchMap(([fundId, from, to]) => toLoadState(this.api.general(fundId, toApiDate(from), toApiDate(to)))),
    ),
    { initialValue: LOADING as LoadState<GeneralReport> },
  );

  protected readonly events = toSignal(
    combineLatest([toObservable(this.fundId), toObservable(this.tab)]).pipe(
      switchMap(([fundId, tab]) => (tab === 'events' ? toLoadState(this.api.events(fundId)) : of(LOADING as LoadState<EventReportRow[]>))),
    ),
    { initialValue: LOADING as LoadState<EventReportRow[]> },
  );

  protected readonly students = toSignal(
    combineLatest([toObservable(this.fundId), toObservable(this.tab)]).pipe(
      switchMap(([fundId, tab]) => (tab === 'students' ? toLoadState(this.api.students(fundId)) : of(LOADING as LoadState<StudentReportRow[]>))),
    ),
    { initialValue: LOADING as LoadState<StudentReportRow[]> },
  );

  protected readonly current = computed<LoadState<unknown>>(() =>
    this.tab() === 'general' ? this.general() : this.tab() === 'events' ? this.events() : this.students(),
  );
  protected readonly kind = computed(() => stateKind(this.current(), () => false));
  protected readonly generalData = computed(() => ready(this.general()));
  protected readonly eventRows = computed(() => ready(this.events()) ?? []);
  protected readonly studentRows = computed(() => ready(this.students()) ?? []);

  protected monthLabel(month: { year: number; month: number }): string {
    return `${String(month.month).padStart(2, '0')}/${month.year}`;
  }

  protected participants(row: EventReportRow): string {
    if (row.status === 'Cancelled' && row.participantCount === 0) {
      return '– / –';
    }
    return `${row.participantCount} / ${row.finalCount ?? '–'}`;
  }

  /** Xuất tab đang xem ra CSV (Excel mở được, giữ tiếng Việt). */
  protected exportExcel(): void {
    const stamp = toApiDate(new Date());
    let csv: string;
    let name: string;
    if (this.tab() === 'general') {
      const data = this.generalData();
      if (!data) {
        return;
      }
      csv = toCsv(
        ['Tháng', 'Thu', 'Chi', 'Số dư cuối'],
        [
          ...data.months.map((month) => [this.monthLabel(month), month.income, month.expense, month.closingBalance]),
          [],
          ['Danh mục chi', 'Số tiền'],
          ...data.expenseByCategory.map((row) => [row.categoryName, row.amount]),
          [],
          ['Tổng thu', data.totalIncome],
          ['Trong đó tự ghi nhận', data.selfDeclaredIncome],
          ['Tổng chi', data.totalExpense],
          ['Số dư', data.balance],
        ],
      );
      name = `bao-cao-thu-chi-chung-${stamp}.csv`;
    } else if (this.tab() === 'events') {
      csv = toCsv(
        ['Sự kiện', 'Tham gia', 'Sau kết thúc', 'Cần thu', 'Đã thu', 'Chưa thu', 'Tổng chi', 'Tiền thừa', 'Xử lý', 'Quyết toán'],
        this.eventRows().map((row) => [
          row.name,
          row.participantCount,
          row.finalCount ?? '',
          row.amountDue,
          row.collected,
          row.uncollected,
          row.totalExpense,
          row.surplus ?? '',
          this.handling(row),
          this.statusBadge[row.status].text,
        ]),
      );
      name = `bao-cao-su-kien-${stamp}.csv`;
    } else {
      csv = toCsv(
        ['Học sinh', 'Đã đóng quỹ chung', 'Khoản chung chưa đóng', 'Đã đóng sự kiện', 'Được hoàn', 'Đã dùng số dư', 'Số dư hiện tại'],
        this.studentRows().map((row) => [
          row.fullName,
          row.paidGeneral,
          row.unpaidGeneral.map((item) => item.title).join('; '),
          row.paidEvents,
          row.refunded,
          row.usedBalance,
          row.balance,
        ]),
      );
      name = `bao-cao-hoc-sinh-${stamp}.csv`;
    }
    downloadFile(new Blob([csv], { type: 'text/csv;charset=utf-8' }), name);
  }
}

function ready<T>(state: LoadState<T>): T | null {
  return state.status === 'ready' ? state.data : null;
}

/** Tải file: gắn link vào trang rồi mới bấm, thu hồi URL sau khi trình duyệt đã bắt đầu tải. */
function downloadFile(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
