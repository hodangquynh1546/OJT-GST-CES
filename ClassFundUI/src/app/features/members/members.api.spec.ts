import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { MembersApi } from './members.api';

describe('MembersApi', () => {
  let api: MembersApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(MembersApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('danh sách: gửi bộ lọc dạng query và bỏ giá trị trống', () => {
    api.list('f1', { search: '', status: 'Removed', page: 2, pageSize: 20 }).subscribe();
    const request = http.expectOne((r) => r.url === '/api/v1/funds/f1/members');
    expect(request.request.params.get('status')).toBe('Removed');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.has('search')).toBeFalse();
    request.flush({ items: [], total: 0, page: 2, pageSize: 20 });
  });

  it('thêm một email, thêm nhiều email, sửa tên', () => {
    api.add('f1', 'a@x.com', 'An').subscribe();
    const add = http.expectOne('/api/v1/funds/f1/members');
    expect(add.request.method).toBe('POST');
    expect(add.request.body).toEqual({ email: 'a@x.com', fullName: 'An' });
    add.flush({});

    api
      .addMany('f1', [
        { email: 'a@x.com', fullName: 'Nguyễn Văn An' },
        { email: 'b@x.com', fullName: '' },
      ])
      .subscribe();
    const bulk = http.expectOne('/api/v1/funds/f1/members/bulk');
    expect(bulk.request.body).toEqual({
      members: [
        { email: 'a@x.com', fullName: 'Nguyễn Văn An' },
        { email: 'b@x.com', fullName: '' },
      ],
    });
    bulk.flush({ results: [] });

    api.update('f1', 'm1', 'Tên mới').subscribe();
    const update = http.expectOne('/api/v1/funds/f1/members/m1');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ fullName: 'Tên mới' });
    update.flush({});
  });

  it('cấp lại mật khẩu, xem trước và ngừng thành viên', () => {
    api.resetPassword('f1', 'm1').subscribe();
    http.expectOne('/api/v1/funds/f1/members/m1/reset-password').flush({ temporaryPassword: 'x' });

    api.removalPreview('f1', 'm1').subscribe();
    const preview = http.expectOne('/api/v1/funds/f1/members/m1/removal-preview');
    expect(preview.request.method).toBe('GET');
    preview.flush({});

    api.remove('f1', 'm1').subscribe();
    const remove = http.expectOne('/api/v1/funds/f1/members/m1/remove');
    expect(remove.request.method).toBe('POST');
    remove.flush({ cancelledAssignments: 0 });
  });
});
