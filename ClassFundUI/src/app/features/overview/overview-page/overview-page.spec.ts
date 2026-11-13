import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { Permission, Permissions } from '../../../core/auth/permissions';
import { fundApiUrl } from '../../../core/config/api.config';
import { apiErrorInterceptor } from '../../../core/interceptors/api-error.interceptor';
import { CurrentFund } from '../../../core/services/current-fund.service';
import { YearlyReport } from '../../reports/reports.models';
import { FundOverview } from '../overview.models';
import { OverviewPage } from './overview-page';

const TEACHER: Permission[] = [Permissions.FundView, Permissions.LedgerViewFull, Permissions.FundManage, Permissions.StudentManage, Permissions.TreasurerAssign, Permissions.MemberView];
const TREASURER: Permission[] = [Permissions.FundView, Permissions.LedgerViewFull, Permissions.MemberView, Permissions.TransactionManage, Permissions.CategoryManage];
const PUBLIC: Permission[] = [Permissions.FundView, Permissions.LedgerViewPublic];

const OVERVIEW: FundOverview = {
  role: 'Teacher',
  permissions: [],
  currentBalance: 1_300_000,
  totalIncome: 1_500_000,
  totalExpense: 200_000,
  currentMonth: { income: 500_000, expense: 200_000, net: 300_000 },
  treasurerStatus: 'Assigned',
  managers: [
    { fullName: 'Trần Văn Hùng', role: 'Teacher' },
    { fullName: 'Phạm Thu Trang', role: 'Treasurer' },
  ],
  recentTransactions: [
    { id: 't1', type: 'Income', amount: 500_000, occurredOn: '2026-11-10', categoryId: 'c1', categoryName: 'Đóng quỹ', description: 'Đóng quỹ tháng 11', isCancelled: false },
    { id: 't2', type: 'Expense', amount: 200_000, occurredOn: '2026-11-09', categoryId: 'c2', categoryName: 'Văn phòng phẩm', description: 'Mua bút', isCancelled: false },
  ],
};

describe('OverviewPage (S-01)', () => {
  let fixture: ComponentFixture<OverviewPage>;
  let backend: HttpTestingController;

  const element = () => fixture.nativeElement as HTMLElement;
  const text = () => (element().textContent ?? '').replace(/\s+/g, ' ');
  const byId = (id: string) => element().querySelector(`[data-testid="${id}"]`);
  const button = (label: string) =>
    [...element().querySelectorAll<HTMLButtonElement>('button')].find((item) => item.textContent?.includes(label));

  function create(permissions: Permission[]): void {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: CurrentFund, useValue: { can: (permission: Permission) => permissions.includes(permission), membership: () => ({ fundName: 'Quỹ lớp 5A' }) } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(OverviewPage);
    fixture.componentRef.setInput('fundId', 'f1');
    fixture.detectChanges();
  }

  afterEach(() => backend.verify());

  const yearlyRequests = (): TestRequest[] =>
    backend.match((request) => request.url === fundApiUrl('f1', '/reports/yearly'));

  function flushYearly(): void {
    for (const request of yearlyRequests()) {
      const year = Number(request.request.params.get('year'));
      const report: YearlyReport = { year, months: Array.from({ length: 12 }, (_, index) => ({ month: index + 1, openingBalance: 0, totalIncome: 100, totalExpense: 50, netChange: 50, closingBalance: 50 })) };
      request.flush(report);
    }
    fixture.detectChanges();
  }

  function load(overview: Partial<FundOverview> = {}): void {
    backend.expectOne(fundApiUrl('f1')).flush({ ...OVERVIEW, ...overview });
    flushYearly();
  }

  it('shows the KPI cards, recent transactions, managers and the chart for a teacher', () => {
    create(TEACHER);
    load();

    expect(byId('kpi-balance')?.textContent?.trim()).toBe('1.300.000 đ');
    expect(byId('kpi-income')?.textContent?.trim()).toBe('1.500.000 đ');
    expect(byId('kpi-expense')?.textContent?.trim()).toBe('200.000 đ');
    expect(byId('kpi-month')?.textContent?.trim()).toBe('+300.000 đ');
    expect(text()).toContain('Thu 500.000 đ · Chi 200.000 đ');
    expect(element().querySelectorAll('[data-testid="recent"] li').length).toBe(2);
    expect(byId('recent')?.textContent).toContain('+500.000 đ');
    expect(byId('recent')?.textContent).toContain('−200.000 đ');
    expect(byId('recent')?.textContent).toContain('10/11/2026');
    expect(byId('managers')?.textContent).toContain('Trần Văn Hùng');
    expect(byId('managers')?.textContent).toContain('Thủ quỹ');
    expect(byId('trend-chart')).not.toBeNull();
    expect(byId('negative-balance')).toBeNull();
    expect(byId('no-treasurer')).toBeNull();
    expect(byId('public-note')).toBeNull();
    expect(button('+ Khoản thu')).toBeUndefined();
    expect(text()).toContain('Phân công thủ quỹ');
    expect(text()).toContain('Đổi tên quỹ');
  });

  it('warns about a negative balance and about a fund without a treasurer', () => {
    create(TEACHER);
    load({ currentBalance: -150_000, treasurerStatus: 'Unassigned', managers: [OVERVIEW.managers[0]] });

    expect(byId('negative-balance')?.textContent).toContain('Số dư quỹ đang âm');
    expect(byId('kpi-balance')?.textContent?.trim()).toBe('−150.000 đ');
    expect(byId('kpi-balance')?.classList).toContain('cf-expense');
    expect(byId('no-treasurer')?.textContent).toContain('Chưa phân công thủ quỹ');
    expect(byId('no-treasurer')?.querySelector('a')?.textContent).toContain('Phân công thủ quỹ');
  });

  it('shows an empty hint when there are no transactions yet', () => {
    create(TREASURER);
    load({ recentTransactions: [], currentBalance: 0, totalIncome: 0, totalExpense: 0, currentMonth: { income: 0, expense: 0, net: 0 } });

    expect(byId('recent')).toBeNull();
    expect(text()).toContain('Chưa có khoản thu chi nào.');
    expect(text()).toContain('+ Khoản thu');
  });

  it('treasurer gets add buttons that open the transaction dialog with the fund categories', () => {
    create(TREASURER);
    load();

    expect(button('+ Khoản thu')).toBeDefined();
    expect(text()).toContain('Danh mục thu chi');
    expect(text()).not.toContain('Đổi tên quỹ');

    button('+ Khoản chi')!.click();
    fixture.detectChanges();

    const request = backend.expectOne((candidate) => candidate.url === fundApiUrl('f1', '/categories'));
    expect(request.request.params.get('includeHidden')).toBe('true');
    request.flush([{ id: 'c2', type: 'Expense', name: 'Văn phòng phẩm', isHidden: false, transactionCount: 1 }]);
    fixture.detectChanges();
    expect(element().querySelector('app-tx-dialog')).not.toBeNull();
  });

  it('student / parent see the public note and no management actions or links', () => {
    create(PUBLIC);
    load({ role: 'Student', recentTransactions: [OVERVIEW.recentTransactions[0]] });

    expect(byId('public-note')?.textContent).toContain('bản công khai');
    expect(button('+ Khoản thu')).toBeUndefined();
    expect(text()).not.toContain('Phân công thủ quỹ');
    expect(text()).not.toContain('Danh mục thu chi');
    expect(element().querySelectorAll('[data-testid="recent"] li').length).toBe(1);
  });

  it('keeps the KPIs when only the chart fails, and retries the chart', () => {
    create(TEACHER);
    backend.expectOne(fundApiUrl('f1')).flush(OVERVIEW);
    for (const request of yearlyRequests()) {
      request.flush({ code: 'server_error' }, { status: 500, statusText: 'Server Error' });
    }
    fixture.detectChanges();

    expect(byId('kpi-balance')).not.toBeNull();
    expect(text()).toContain('Không tải được biểu đồ');

    button('Thử lại')!.click();
    flushYearly();
    expect(byId('trend-chart')).not.toBeNull();
  });

  it('shows an error state with retry when the overview fails to load', () => {
    create(TEACHER);
    backend.expectOne(fundApiUrl('f1')).flush({ code: 'server_error' }, { status: 500, statusText: 'Server Error' });
    yearlyRequests().forEach((request) => request.flush({ year: 2026, months: [] }));
    fixture.detectChanges();

    expect(text()).toContain('Không tải được dữ liệu');
    button('Thử lại')!.click();
    load();
    expect(byId('kpi-balance')).not.toBeNull();
  });

  it('shows not-found when the fund is not the user\'s', () => {
    create(TEACHER);
    backend.expectOne(fundApiUrl('f1')).flush({ code: 'not_found' }, { status: 404, statusText: 'Not Found' });
    yearlyRequests().forEach((request) => request.flush({ year: 2026, months: [] }));
    fixture.detectChanges();

    expect(text()).toContain('Không tìm thấy trang');
  });

  it('shows the loading skeleton until the overview arrives', () => {
    create(TEACHER);

    expect(element().querySelector('[aria-label="Đang tải"]')).not.toBeNull();
    backend.expectOne(fundApiUrl('f1')).flush(OVERVIEW);
    flushYearly();
    expect(element().querySelector('[aria-label="Đang tải"]')).toBeNull();
  });
});
