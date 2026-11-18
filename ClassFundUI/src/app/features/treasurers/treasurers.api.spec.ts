import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { TreasurersApi } from './treasurers.api';

describe('TreasurersApi', () => {
  let api: TreasurersApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(TreasurersApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('danh sách, phân quyền và thu hồi gọi đúng đường dẫn theo quỹ', () => {
    api.list('f1').subscribe();
    const list = http.expectOne('/api/v1/funds/f1/treasurers');
    expect(list.request.method).toBe('GET');
    list.flush([]);

    api.grant('f1', 'm1').subscribe();
    const grant = http.expectOne('/api/v1/funds/f1/treasurers');
    expect(grant.request.method).toBe('POST');
    expect(grant.request.body).toEqual({ memberId: 'm1' });
    grant.flush({});

    api.revoke('f1', 'm1').subscribe();
    const revoke = http.expectOne('/api/v1/funds/f1/treasurers/m1/revoke');
    expect(revoke.request.method).toBe('POST');
    revoke.flush({});
  });

  it('danh sách chọn: chỉ học sinh đang hoạt động, lọc theo từ khóa, nhãn "tên – email"', () => {
    let labels: string[] = [];
    api.candidates('f1', 'hà').subscribe((items) => (labels = items.map((item) => item.label)));

    const request = http.expectOne((r) => r.url === '/api/v1/funds/f1/members');
    expect(request.request.params.get('role')).toBe('Student');
    expect(request.request.params.get('status')).toBe('Active');
    expect(request.request.params.get('search')).toBe('hà');
    request.flush({
      items: [{ id: 'm1', fullName: 'Lê Thu Hà', email: 'ha@x.vn' }],
      total: 1,
      page: 1,
      pageSize: 50,
    });

    expect(labels).toEqual(['Lê Thu Hà – ha@x.vn']);
  });
});
