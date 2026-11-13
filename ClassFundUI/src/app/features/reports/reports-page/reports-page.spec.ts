import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { FundRole } from '../../../core/auth/auth.models';
import { apiUrl, fundApiUrl } from '../../../core/config/api.config';
import { apiErrorInterceptor } from '../../../core/interceptors/api-error.interceptor';
import { vietnamToday } from '../../transactions/ledger-dates';
import { MonthlyReport, YearlyReport } from '../reports.models';
import { ReportsPage } from './reports-page';

const TREASURER = ['Fund.View', 'Ledger.ViewFull', 'Transaction.Manage', 'Category.Manage', 'Member.View'];
const TEACHER = ['Fund.View', 'Ledger.ViewFull', 'Fund.Manage', 'Treasurer.Assign'];
const PUBLIC = ['Fund.View', 'Ledger.ViewPublic'];

const TODAY = vietnamToday();
const YEAR = Number(TODAY.slice(0, 4));
const MONTH = Number(TODAY.slice(5, 7));

const monthly = (extra: Partial<MonthlyReport> = {}): MonthlyReport => ({
  year: YEAR,
  month: MONTH,
  openingBalance: 2_450_000,
  totalIncome: 3_500_000,
  totalExpense: 1_930_000,
  netChange: 1_570_000,
  closingBalance: 4_020_000,
  byCategory: [
    { type: 'Income', categoryId: 'c1', categoryName: 'Đóng quỹ', count: 1, amount: 3_200_000 },
    { type: 'Income', categoryId: null, categoryName: 'Khoản khác', count: null, amount: 300_000 },
    { type: 'Expense', categoryId: 'c4', categoryName: 'Sinh hoạt, liên hoan', count: 1, amount: 1_250_000 },
    { type: 'Expense', categoryId: 'c3', categoryName: 'Văn phòng phẩm', count: 1, amount: 180_000 },
    { type: 'Expense', categoryId: null, categoryName: 'Khoản khác', count: null, amount: 500_000 },
  ],
  transactions: [
    { id: 't2', type: 'Expense', amount: 180_000, occurredOn: '2026-11-05', categoryId: 'c3', categoryName: 'Văn phòng phẩm', description: 'Mua giấy A4', isCancelled: false },
    { id: 't1', type: 'Income', amount: 3_200_000, occurredOn: '2026-11-02', categoryId: 'c1', categoryName: 'Đóng quỹ', description: 'Đóng quỹ tháng 11', isCancelled: false },
  ],
  ...extra,
});

const yearly = (): YearlyReport => ({
  year: YEAR,
  months: Array.from({ length: 12 }, (_, index) => {
    const month = index + 1;
    const income = month === 1 ? 4_800_000 : 0;
    const expense = month === 1 ? 2_100_000 : 0;
    const opening = month === 1 ? 0 : 2_700_000;
    return {
      month,
      openingBalance: opening,
      totalIncome: income,
      totalExpense: expense,
      netChange: income - expense,
      closingBalance: opening + income - expense,
    };
  }),
});

describe('ReportsPage (S-03)', () => {
  let fixture: ComponentFixture<ReportsPage>;
  let backend: HttpTestingController;
  let permissions: string[] = [];
  let role: FundRole = 'Treasurer';

  const root = () => fixture.nativeElement as HTMLElement;
  const text = () => root().textContent?.replace(/\s+/g, ' ') ?? '';
  /** Text các ô con, nối bằng một dấu cách. */
  const cells = (element: Element) => [...element.children].map((child) => child.textContent?.trim()).join(' ');
  const button = (label: string) =>
    [...root().querySelectorAll<HTMLButtonElement>('button')].find((item) => item.textContent?.trim() === label)!;
  const monthlyRequest = (): TestRequest => backend.expectOne((request) => request.url === fundApiUrl('f1', '/reports/monthly'));
  const yearlyRequest = (): TestRequest => backend.expectOne((request) => request.url === fundApiUrl('f1', '/reports/yearly'));

  function start(viewer: string[], viewerRole: FundRole, report: MonthlyReport = monthly()): void {
    permissions = viewer;
    role = viewerRole;
    fixture = TestBed.createComponent(ReportsPage);
    fixture.componentRef.setInput('fundId', 'f1');
    fixture.detectChanges();
    backend.expectOne(apiUrl('/funds')).flush([{ id: 'f1', currentBalance: 0, createdAt: `${YEAR - 1}-09-01T01:00:00Z` }]);
    const request = monthlyRequest();
    expect(request.request.params.get('year')).toBe(String(YEAR));
    expect(request.request.params.get('month')).toBe(String(MONTH));
    request.flush(report);
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: AuthService, useValue: { membership: () => ({ fundId: 'f1', role, permissions }) } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('thủ quỹ: tháng hiện tại, 5 số liệu, bảng theo danh mục, danh sách theo ngày tăng dần', () => {
    start(TREASURER, 'Treasurer', monthly({ byCategory: monthly().byCategory.filter((line) => line.categoryId) }));

    expect(text()).toContain('Đầy đủ, tự tính từ giao dịch đang hiệu lực');
    expect(root().querySelector('.public-note')).toBeNull();
    const kpis = [...root().querySelectorAll('.kpi')].map((item) => cells(item));
    expect(kpis).toEqual([
      'Số dư đầu tháng 2.450.000 đ',
      'Tổng thu +3.500.000 đ',
      'Tổng chi −1.930.000 đ',
      'Chênh lệch +1.570.000 đ',
      'Số dư cuối tháng 4.020.000 đ',
    ]);
    expect(text()).toContain('= số dư cuối tháng trước (2.450.000 đ)');
    expect(text()).toContain('Giao dịch trong tháng (2)');
    expect(text()).toContain('Không gồm khoản đã hủy');
    const dates = [...root().querySelectorAll('.tx .date')].map((item) => item.textContent?.trim());
    expect(dates).toEqual(['02/11/2026', '05/11/2026']);
    expect(text()).not.toContain('Khoản khác');
  });

  it('GVCN: bản đầy đủ, chỉ xem', () => {
    start(TEACHER, 'Teacher');
    expect(text()).toContain('Đầy đủ · GVCN chỉ xem');
    expect(root().querySelector('.public-note')).toBeNull();
  });

  it('phụ huynh: ghi chú bản công khai, dòng "Khoản khác" không số lượng', () => {
    start(PUBLIC, 'Parent');

    expect(text()).toContain('Bản công khai cho phụ huynh · mọi tháng');
    expect(root().querySelector('.public-note')?.textContent).toContain('gộp vào “Khoản khác”');
    const others = [...root().querySelectorAll('td.other')];
    expect(others.length).toBe(2);
    expect(others[0].textContent?.trim()).toBe('Khoản khác (không công khai chi tiết)');
    expect(others[0].nextElementSibling?.textContent?.trim()).toBe('—');
    expect(text()).toContain('Khoản công khai trong tháng (2)');
    expect(text()).toContain('Tổng thu +3.500.000 đ');
    expect(text()).toContain('Tổng chi −1.930.000 đ');
  });

  it('học sinh: phụ đề bản công khai cho học sinh', () => {
    start(PUBLIC, 'Student');
    expect(text()).toContain('Bản công khai cho học sinh · mọi tháng');
  });

  it('tháng không có giao dịch: số 0, bảng và danh sách báo trống', () => {
    start(TREASURER, 'Treasurer', monthly({ totalIncome: 0, totalExpense: 0, netChange: 0, closingBalance: 2_450_000, byCategory: [], transactions: [] }));

    expect(text()).toContain('Không có khoản thu trong tháng.');
    expect(text()).toContain('Không có khoản chi trong tháng.');
    expect(text()).toContain('Không có giao dịch trong tháng này.');
    expect(text()).toContain('Giao dịch trong tháng (0)');
  });

  it('chuyển sang 12 tháng: gọi yearly, đủ 12 dòng, tháng tương lai để trống', () => {
    start(TREASURER, 'Treasurer');

    button('12 tháng').click();
    fixture.detectChanges();
    const request = yearlyRequest();
    expect(request.request.params.get('year')).toBe(String(YEAR));
    request.flush(yearly());
    fixture.detectChanges();

    expect(text()).toContain(`Thu chi theo tháng – ${YEAR}`);
    const rows = [...root().querySelectorAll('.year tbody tr')];
    expect(rows.length).toBe(12);
    expect(cells(rows[0])).toBe(`T1/${YEAR} 0 +4.800.000 −2.100.000 +2.700.000 2.700.000`);
    expect(rows.filter((row) => row.classList.contains('future')).length).toBe(12 - MONTH);
    expect(root().querySelectorAll('.bars').length).toBe(12);
    expect((root().querySelector('.bar.income') as HTMLElement).style.height).toBe('144px');
  });

  it('lỗi tải: hiện trạng thái lỗi và Thử lại gọi lại API', () => {
    permissions = TREASURER;
    fixture = TestBed.createComponent(ReportsPage);
    fixture.componentRef.setInput('fundId', 'f1');
    fixture.detectChanges();
    backend.expectOne(apiUrl('/funds')).flush([]);
    monthlyRequest().flush({ status: 500, code: 'server_error', title: 'Lỗi' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(text()).toContain('Không tải được dữ liệu');
    button('Thử lại').click();
    fixture.detectChanges();
    monthlyRequest().flush(monthly());
    fixture.detectChanges();
    expect(text()).toContain('Số dư cuối tháng');
  });
});
