import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ChargesApi } from './charges.api';

describe('ChargesApi', () => {
  let api: ChargesApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(ChargesApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lấy danh sách khoản thu theo trạng thái', () => {
    api.list('f1', { status: 'Open', page: 1, pageSize: 100 }).subscribe();
    const request = http.expectOne((r) => r.url.endsWith('/funds/f1/charges'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('status')).toBe('Open');
    expect(request.request.params.get('pageSize')).toBe('100');
    request.flush({ items: [], total: 0, page: 1, pageSize: 100 });
  });

  it('tạo khoản thu và nộp tiền đúng đường dẫn', () => {
    api.create('f1', { title: 'A', purpose: null, amountPerPerson: 1000, dueDate: '2026-12-01', categoryId: 'cat1' }).subscribe();
    const create = http.expectOne((r) => r.url.endsWith('/funds/f1/charges'));
    expect(create.request.method).toBe('POST');
    expect(create.request.body.amountPerPerson).toBe(1000);
    create.flush({});

    api.submit('f1', 'a1', { method: 'BankTransfer', transferCode: 'FT1', attachmentId: 'p1' }).subscribe();
    const submit = http.expectOne((r) => r.url.endsWith('/funds/f1/me/assignments/a1/payments'));
    expect(submit.request.body).toEqual({ method: 'BankTransfer', transferCode: 'FT1', attachmentId: 'p1' });
    submit.flush({});
  });

  it('xác nhận và từ chối thanh toán', () => {
    api.confirm('f1', 'p1').subscribe();
    http.expectOne((r) => r.url.endsWith('/funds/f1/payments/p1/confirm')).flush({});

    api.reject('f1', 'p1', 'Sai số tiền').subscribe();
    const reject = http.expectOne((r) => r.url.endsWith('/funds/f1/payments/p1/reject'));
    expect(reject.request.body).toEqual({ reason: 'Sai số tiền' });
    reject.flush({});
  });

  it('theo dõi danh sách đã/chưa đóng kèm bộ lọc', () => {
    api.assignments('f1', 'c1', { status: 'Unpaid', search: 'ha', page: 2, pageSize: 20 }).subscribe();
    const request = http.expectOne((r) => r.url.endsWith('/funds/f1/charges/c1/assignments'));
    expect(request.request.params.get('status')).toBe('Unpaid');
    expect(request.request.params.get('search')).toBe('ha');
    request.flush({ items: [], total: 0, page: 2, pageSize: 20 });
  });
});
