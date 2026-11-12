import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { Location } from '@angular/common';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { apiUrl, fundApiUrl } from '../../../core/config/api.config';
import { apiErrorInterceptor } from '../../../core/interceptors/api-error.interceptor';
import { AppNotifications } from '../../../core/services/notification.service';
import { monthRange, vietnamToday } from '../ledger-dates';
import '../transactions.errors';
import { Transaction, TransactionPage } from '../transactions.models';
import { LedgerPage } from './ledger-page';

const TREASURER = ['Fund.View', 'Ledger.ViewFull', 'Transaction.Manage', 'Category.Manage', 'Member.View'];
const TEACHER = ['Fund.View', 'Ledger.ViewFull', 'Fund.Manage', 'Treasurer.Assign'];
const PUBLIC = ['Fund.View', 'Ledger.ViewPublic'];

const tx = (id: string, extra: Partial<Transaction> = {}): Transaction => ({
  id,
  type: 'Expense',
  amount: 180_000,
  occurredOn: '2026-11-05',
  categoryId: 'c3',
  categoryName: 'Văn phòng phẩm',
  description: `Khoản ${id}`,
  isCancelled: false,
  ...extra,
});

const page = (items: Transaction[], total = items.length): TransactionPage => ({ items, total, page: 1, pageSize: 20 });

describe('LedgerPage (S-02)', () => {
  let fixture: ComponentFixture<LedgerPage>;
  let backend: HttpTestingController;
  let success: jasmine.Spy;
  /** Quyền của người xem trong quỹ f1 (AuthService.membership). */
  let current: string[] = [];

  const root = () => fixture.nativeElement as HTMLElement;
  const text = () => root().textContent?.replace(/\s+/g, ' ') ?? '';
  const buttons = (label: string) =>
    [...root().querySelectorAll<HTMLButtonElement>('button')].filter((item) => item.textContent?.trim() === label);
  const listRequest = (): TestRequest => backend.expectOne((request) => request.url === fundApiUrl('f1', '/transactions'));

  function start(permissions: string[], items: Transaction[], txId?: string): void {
    current = permissions;
    fixture = TestBed.createComponent(LedgerPage);
    fixture.componentRef.setInput('fundId', 'f1');
    if (txId) {
      fixture.componentRef.setInput('txId', txId);
    }
    fixture.detectChanges();
    backend.expectOne(apiUrl('/funds')).flush([{ id: 'f1', currentBalance: 4_020_000, createdAt: '2026-09-01T01:00:00Z' }]);
    backend.expectOne((request) => request.url === fundApiUrl('f1', '/categories')).flush([
      { id: 'c1', type: 'Income', name: 'Đóng quỹ' },
      { id: 'c3', type: 'Expense', name: 'Văn phòng phẩm' },
    ]);
    listRequest().flush(page(items));
    fixture.detectChanges();
  }

  beforeEach(() => {
    success = jasmine.createSpy('success');
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: AuthService, useValue: { membership: () => ({ fundId: 'f1', permissions: current }) } },
        { provide: AppNotifications, useValue: { success, warning: jasmine.createSpy('warning') } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('loads the current month, active only, page 1 of 20 by default', () => {
    current = TREASURER;
    fixture = TestBed.createComponent(LedgerPage);
    fixture.componentRef.setInput('fundId', 'f1');
    fixture.detectChanges();
    backend.expectOne(apiUrl('/funds')).flush([]);
    backend.expectOne((request) => request.url === fundApiUrl('f1', '/categories')).flush([]);

    const request = listRequest();
    const { from, to } = monthRange(vietnamToday().slice(0, 7));
    expect(request.request.params.get('from')).toBe(from);
    expect(request.request.params.get('to')).toBe(to);
    expect(request.request.params.get('status')).toBe('Active');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.has('type')).toBeFalse();
    request.flush(page([]));
    fixture.detectChanges();

    expect(text()).toContain('Không có giao dịch phù hợp');
  });

  it('treasurer sees add buttons, private tag, cancelled styling and paging', () => {
    start(TREASURER, [tx('t1', { isPublic: false }), tx('t2', { isCancelled: true, type: 'Income', amount: 300_000 })]);

    expect(buttons('+ Khoản thu').length).toBe(1);
    expect(buttons('+ Khoản chi').length).toBe(1);
    expect(text()).toContain('Không công khai');
    expect(text()).toContain('Đã hủy');
    expect(text()).toContain('−180.000 đ');
    expect(text()).toContain('2 giao dịch · 20 / trang');
    expect(text()).toContain('Trang 1 / 1');
  });

  it('filters by type and resets to page 1', () => {
    start(TREASURER, [tx('t1')]);

    buttons('Chi')[0].click();
    fixture.detectChanges();

    const request = listRequest();
    expect(request.request.params.get('type')).toBe('Expense');
    expect(request.request.params.get('page')).toBe('1');
    request.flush(page([tx('t1')]));
  });

  it('debounces search 300 ms', fakeAsync(() => {
    start(TREASURER, [tx('t1')]);
    const component = fixture.componentInstance as unknown as { onSearch(value: string): void };

    component.onSearch(' liên hoan ');
    tick(299);
    backend.expectNone((request) => request.url === fundApiUrl('f1', '/transactions'));
    tick(1);
    fixture.detectChanges();

    const request = listRequest();
    expect(request.request.params.get('search')).toBe('liên hoan');
    request.flush(page([]));
  }));

  it('opening /transactions/:txId loads detail and history for full viewers and shows actions to the treasurer', () => {
    start(TREASURER, [tx('t1')], 't1');
    backend.expectOne(fundApiUrl('f1', '/transactions/t1')).flush(
      tx('t1', { note: 'Có hóa đơn', isPublic: true, createdByName: 'Nguyễn Minh Khang', createdAt: '2026-11-05T13:15:00Z' }),
    );
    backend.expectOne(fundApiUrl('f1', '/transactions/t1/history')).flush([
      { occurredAt: '2026-11-05T13:15:00Z', action: 'Transaction.Create', actorName: 'Nguyễn Minh Khang', oldValues: null, newValues: { amount: 180000, categoryId: 'c3' } },
    ]);
    fixture.detectChanges();

    expect(text()).toContain('Khoản chi · 05/11/2026');
    expect(text()).toContain('Có hóa đơn');
    expect(text()).toContain('Nguyễn Minh Khang · 05/11/2026 20:15');
    expect(text()).toContain('Tạo · 05/11/2026 20:15 · Nguyễn Minh Khang');
    expect(text()).toContain('Số tiền 180.000 đ · Văn phòng phẩm');
    expect(buttons('Sửa').length).toBe(1);
    expect(buttons('Hủy giao dịch').length).toBe(1);
  });

  it('teacher sees full detail but no write actions', () => {
    start(TEACHER, [tx('t1')], 't1');
    backend.expectOne(fundApiUrl('f1', '/transactions/t1')).flush(tx('t1', { note: 'Ghi chú', isPublic: true }));
    backend.expectOne(fundApiUrl('f1', '/transactions/t1/history')).flush([]);
    fixture.detectChanges();

    expect(buttons('+ Khoản chi').length).toBe(0);
    expect(buttons('Sửa').length).toBe(0);
    expect(text()).toContain('GVCN chỉ xem (không ghi thu chi)');
    expect(text()).toContain('Ghi chú');
  });

  it('public viewer: public title, no history request, cancelled message without reason', () => {
    start(PUBLIC, [tx('t1')], 't1');
    backend.expectOne(fundApiUrl('f1', '/transactions/t1')).flush(tx('t1', { isCancelled: true, cancelledOn: '2026-11-07' }));
    fixture.detectChanges();

    expect(text()).toContain('Thu chi công khai');
    expect(text()).toContain('Khoản này đã bị hủy ngày 07/11/2026 và không còn tính vào số dư.');
    expect(text()).toContain('Phụ huynh và học sinh không xem được ghi chú nội bộ');
    expect(text()).not.toContain('Lịch sử thay đổi');
  });

  it('clicking a row selects it and updates the URL without reloading the page', () => {
    start(TREASURER, [tx('t1')]);
    const location = TestBed.inject(Location);
    spyOn(location, 'replaceState');

    root().querySelector<HTMLButtonElement>('.rowbtn')!.click();
    fixture.detectChanges();

    expect(location.replaceState).toHaveBeenCalledWith('/funds/f1/transactions/t1');
    backend.expectOne(fundApiUrl('f1', '/transactions/t1')).flush(tx('t1'));
    backend.expectOne(fundApiUrl('f1', '/transactions/t1/history')).flush([]);
  });

  it('shows the error state with retry when the list fails', () => {
    current = PUBLIC;
    fixture = TestBed.createComponent(LedgerPage);
    fixture.componentRef.setInput('fundId', 'f1');
    fixture.detectChanges();
    backend.expectOne(apiUrl('/funds')).flush([]);
    backend.expectOne((request) => request.url === fundApiUrl('f1', '/categories')).flush([]);
    listRequest().flush({ code: 'internal_error' }, { status: 500, statusText: 'Error' });
    fixture.detectChanges();

    buttons('Thử lại')[0].click();
    listRequest().flush(page([tx('t1')]));
    fixture.detectChanges();
    expect(text()).toContain('Khoản t1');
  });
});
