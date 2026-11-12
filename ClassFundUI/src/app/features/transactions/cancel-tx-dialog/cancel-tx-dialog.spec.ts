import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { fundApiUrl } from '../../../core/config/api.config';
import { apiErrorInterceptor } from '../../../core/interceptors/api-error.interceptor';
import '../transactions.errors';
import { Transaction } from '../transactions.models';
import { CancelTxDialog } from './cancel-tx-dialog';

const transaction: Transaction = {
  id: 't1',
  type: 'Expense',
  amount: 1_250_000,
  occurredOn: '2026-11-08',
  categoryId: 'c4',
  categoryName: 'Sinh hoạt, liên hoan',
  description: 'Liên hoan 20/11',
  isCancelled: false,
};

describe('CancelTxDialog (T-01 hủy)', () => {
  let fixture: ComponentFixture<CancelTxDialog>;
  let backend: HttpTestingController;
  let cancelled: Transaction[];

  const root = () => fixture.nativeElement as HTMLElement;
  const text = () => root().textContent?.replace(/\s+/g, ' ') ?? '';
  const submit = () =>
    [...root().querySelectorAll<HTMLButtonElement>('button')].find((item) => item.textContent?.trim() === 'Hủy giao dịch')!;
  const reason = (value: string) => {
    const box = root().querySelector<HTMLTextAreaElement>('textarea')!;
    box.value = value;
    box.dispatchEvent(new Event('input'));
  };

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting(), provideNoopAnimations()],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CancelTxDialog);
    fixture.componentRef.setInput('fundId', 'f1');
    fixture.componentRef.setInput('transaction', transaction);
    cancelled = [];
    fixture.componentInstance.cancelled.subscribe((value) => cancelled.push(value));
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => backend.verify());

  it('summarises the transaction and explains the consequences', () => {
    expect(text()).toContain('Hủy giao dịch?');
    expect(text()).toContain('Liên hoan 20/11');
    expect(text()).toContain('08/11/2026 · Sinh hoạt, liên hoan');
    expect(text()).toContain('−1.250.000 đ');
    expect(text()).toContain('không thể hoàn tác hay sửa');
  });

  it('requires a reason before calling the API', async () => {
    reason('   ');
    submit().click();
    fixture.detectChanges();

    expect(text()).toContain('Nhập lý do hủy (bắt buộc, tối đa 500 ký tự).');
    backend.expectNone(fundApiUrl('f1', '/transactions/t1/cancel'));
  });

  it('cancels with the trimmed reason and emits the result', async () => {
    reason('  Ghi trùng  ');
    await fixture.whenStable();
    submit().click();

    const request = backend.expectOne(fundApiUrl('f1', '/transactions/t1/cancel'));
    expect(request.request.body).toEqual({ reason: 'Ghi trùng' });
    request.flush({ ...transaction, isCancelled: true, cancelReason: 'Ghi trùng' });

    expect(cancelled.length).toBe(1);
  });

  it('shows transaction.cancelled from the server', async () => {
    reason('Ghi trùng');
    await fixture.whenStable();
    submit().click();
    backend
      .expectOne(fundApiUrl('f1', '/transactions/t1/cancel'))
      .flush({ status: 422, code: 'transaction.cancelled' }, { status: 422, statusText: 'Unprocessable Entity' });
    fixture.detectChanges();

    expect(text()).toContain('Khoản này đã bị hủy, không sửa hoặc hủy lại được.');
    expect(cancelled).toEqual([]);
  });
});
