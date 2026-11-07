import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { ApiError } from '../../shared/models/api-error';
import { AuthService } from '../auth/auth.service';
import { apiUrl } from '../config/api.config';
import { antiforgeryInterceptor } from './antiforgery.interceptor';
import { apiErrorInterceptor } from './api-error.interceptor';

describe('HTTP interceptors', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let navigate: jasmine.Spy;
  let clearSession: jasmine.Spy;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([antiforgeryInterceptor, apiErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    clearSession = spyOn(TestBed.inject(AuthService), 'clearSession');
  });

  afterEach(() => backend.verify());

  const problem = { headers: { 'Content-Type': 'application/problem+json' } };

  it('lấy token rồi gắn header X-XSRF-TOKEN cho request ghi, dùng lại ở lần sau', () => {
    http.post(apiUrl('/funds'), {}).subscribe();
    backend.expectOne(apiUrl('/auth/antiforgery')).flush({ antiforgeryToken: 'tok' });
    const first = backend.expectOne(apiUrl('/funds'));
    expect(first.request.headers.get('X-XSRF-TOKEN')).toBe('tok');
    first.flush({});

    http.put(apiUrl('/funds/1'), {}).subscribe();
    backend.expectOne(apiUrl('/funds/1')).flush({});
  });

  it('không gắn token cho request đọc', () => {
    http.get(apiUrl('/funds')).subscribe();
    const request = backend.expectOne(apiUrl('/funds'));
    expect(request.request.headers.has('X-XSRF-TOKEN')).toBeFalse();
    request.flush([]);
  });

  it('đổi problem+json thành ApiError có lỗi từng ô', () => {
    let error: ApiError | undefined;
    http.get(apiUrl('/funds')).subscribe({ error: (e) => (error = e) });
    backend.expectOne(apiUrl('/funds')).flush(
      { code: 'validation_failed', title: 'Dữ liệu không hợp lệ.', errors: { name: ['Nhập tên.'] } },
      { status: 400, statusText: 'Bad Request', ...problem },
    );

    expect(error).toBeInstanceOf(ApiError);
    expect(error?.code).toBe('validation_failed');
    expect(error?.message).toBe('Dữ liệu không hợp lệ.');
    expect(error?.fieldErrors['name']).toEqual(['Nhập tên.']);
  });

  it('401 ở API thường thì xóa phiên và về /login', () => {
    http.get(apiUrl('/funds')).subscribe({ error: () => undefined });
    backend.expectOne(apiUrl('/funds')).flush({ code: 'unauthorized' }, { status: 401, statusText: 'x' });

    expect(clearSession).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('401 khi đọc /auth/me không điều hướng', () => {
    http.get(apiUrl('/auth/me')).subscribe({ error: () => undefined });
    backend.expectOne(apiUrl('/auth/me')).flush({ code: 'unauthorized' }, { status: 401, statusText: 'x' });

    expect(navigate).not.toHaveBeenCalled();
  });

  it('403 password.change_required chuyển sang /change-password', () => {
    http.get(apiUrl('/funds')).subscribe({ error: () => undefined });
    backend
      .expectOne(apiUrl('/funds'))
      .flush({ code: 'password.change_required' }, { status: 403, statusText: 'x' });

    expect(navigate).toHaveBeenCalledWith('/change-password');
  });

  it('mất kết nối trả thông báo tiếng Việt', () => {
    let error: ApiError | undefined;
    http.get(apiUrl('/funds')).subscribe({ error: (e) => (error = e) });
    backend.expectOne(apiUrl('/funds')).error(new ProgressEvent('error'));

    expect(error?.code).toBe('network_error');
    expect(error?.message).toContain('Không kết nối được');
  });
});
