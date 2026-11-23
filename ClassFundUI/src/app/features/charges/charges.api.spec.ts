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

  it('tạo khoản thu chung không hạn và gọi xem khoản của tôi', () => {
    api.create('f1', { title: 'A', purpose: null, amountPerPerson: 1000, dueDate: null, categoryId: 'cat1' }).subscribe();
    const create = http.expectOne((r) => r.url.endsWith('/funds/f1/charges'));
    expect(create.request.method).toBe('POST');
    expect(create.request.body.amountPerPerson).toBe(1000);
    expect(create.request.body.dueDate).toBeNull();
    create.flush({});

    api.mine('f1').subscribe();
    const mine = http.expectOne((r) => r.url.endsWith('/funds/f1/me/assignments'));
    expect(mine.request.method).toBe('GET');
    mine.flush([]);
  });

  it('thủ quỹ tích đã thu (Tiền mặt / Chuyển khoản), bỏ tích và nhắc từng người', () => {
    api.collect('f1', 'a1', { method: 'BankTransfer', transferCode: 'FT1' }).subscribe();
    const collect = http.expectOne((r) => r.url.endsWith('/funds/f1/assignments/a1/collect'));
    expect(collect.request.method).toBe('POST');
    expect(collect.request.body).toEqual({ method: 'BankTransfer', transferCode: 'FT1' });
    collect.flush({});

    api.uncollect('f1', 'a1', 'Tích nhầm').subscribe();
    const uncollect = http.expectOne((r) => r.url.endsWith('/funds/f1/assignments/a1/uncollect'));
    expect(uncollect.request.body).toEqual({ reason: 'Tích nhầm' });
    uncollect.flush({});

    api.remindOne('f1', 'a1').subscribe();
    const remind = http.expectOne((r) => r.url.endsWith('/funds/f1/assignments/a1/remind'));
    expect(remind.request.method).toBe('POST');
    remind.flush({ reminded: 1 });
  });

  it('theo dõi danh sách đã/chưa đóng kèm bộ lọc', () => {
    api.assignments('f1', 'c1', { status: 'Unpaid', search: 'ha', page: 2, pageSize: 20 }).subscribe();
    const request = http.expectOne((r) => r.url.endsWith('/funds/f1/charges/c1/assignments'));
    expect(request.request.params.get('status')).toBe('Unpaid');
    expect(request.request.params.get('search')).toBe('ha');
    request.flush({ items: [], total: 0, page: 2, pageSize: 20 });
  });
});
