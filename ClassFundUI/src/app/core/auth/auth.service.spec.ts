import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { apiUrl } from '../config/api.config';
import { antiforgeryInterceptor } from '../interceptors/antiforgery.interceptor';
import { apiErrorInterceptor } from '../interceptors/api-error.interceptor';
import { AntiforgeryService } from '../services/antiforgery.service';
import { CurrentUser } from './auth.models';
import { AuthService } from './auth.service';

const TEACHER: CurrentUser = {
  id: 'u1',
  kind: 'Teacher',
  fullName: 'Cô Hạnh',
  email: 'co.hanh@example.vn',
  username: null,
  mustChangePassword: false,
  accountPermissions: ['Fund.Create'],
  memberships: [],
};

describe('AuthService (M1)', () => {
  let auth: AuthService;
  let backend: HttpTestingController;
  let antiforgery: AntiforgeryService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([antiforgeryInterceptor, apiErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    auth = TestBed.inject(AuthService);
    backend = TestBed.inject(HttpTestingController);
    antiforgery = TestBed.inject(AntiforgeryService);
    antiforgery.set('anon-token');
  });

  afterEach(() => backend.verify());

  it('login gửi { login, password } (không có vai trò), lưu người dùng và token mới', async () => {
    const pending = auth.login('co.hanh@example.vn', 'Matkhau123');
    const request = backend.expectOne(apiUrl('/auth/login'));
    expect(request.request.body).toEqual({ login: 'co.hanh@example.vn', password: 'Matkhau123' });
    expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('anon-token');
    request.flush({ antiforgeryToken: 'user-token', user: TEACHER });

    expect(await pending).toEqual(TEACHER);
    expect(auth.user()).toEqual(TEACHER);
    expect(auth.homeUrl()).toBe('/funds');

    let token = '';
    antiforgery.token().subscribe((value) => (token = value));
    expect(token).toBe('user-token');
  });

  it('registerTeacher đăng nhập luôn sau khi đăng ký', async () => {
    const body = { fullName: 'Cô Hạnh', email: 'co.hanh@example.vn', password: 'Matkhau123', confirmPassword: 'Matkhau123' };
    const pending = auth.registerTeacher(body);
    const request = backend.expectOne(apiUrl('/auth/register-teacher'));
    expect(request.request.body).toEqual(body);
    request.flush({ antiforgeryToken: 't', user: TEACHER }, { status: 201, statusText: 'Created' });

    await pending;
    expect(auth.isAuthenticated()).toBeTrue();
  });

  it('changePassword xóa cờ mật khẩu tạm → trang chính theo vai trò', async () => {
    const student: CurrentUser = {
      ...TEACHER,
      kind: 'Student',
      email: null,
      username: 'khang.nguyen.204',
      accountPermissions: [],
      memberships: [{ fundId: 'f1', fundName: 'Quỹ lớp 5A', role: 'Student', permissions: ['Fund.View'] }],
    };
    const pending = auth.changePassword('Tam-1234', 'MatKhauMoi1');
    const request = backend.expectOne(apiUrl('/auth/change-password'));
    expect(request.request.body).toEqual({ currentPassword: 'Tam-1234', newPassword: 'MatKhauMoi1' });
    request.flush({ antiforgeryToken: 't2', user: student });

    await pending;
    expect(auth.homeUrl()).toBe('/funds/f1');
  });

  it('changePassword sai mật khẩu hiện tại → ApiError, không đổi phiên', async () => {
    const pending = auth.changePassword('sai', 'MatKhauMoi1');
    backend
      .expectOne(apiUrl('/auth/change-password'))
      .flush({ code: 'auth.wrong_password', title: 'x' }, { status: 422, statusText: 'Unprocessable', headers: { 'Content-Type': 'application/problem+json' } });

    await expectAsync(pending).toBeRejectedWith(jasmine.objectContaining({ code: 'auth.wrong_password' }));
    expect(auth.user()).toBeNull();
  });

  it('updateProfile cập nhật người dùng hiện tại', async () => {
    const pending = auth.updateProfile('Trần Thị Hạnh');
    const request = backend.expectOne(apiUrl('/auth/me'));
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ fullName: 'Trần Thị Hạnh' });
    request.flush({ ...TEACHER, fullName: 'Trần Thị Hạnh' });

    expect((await pending).fullName).toBe('Trần Thị Hạnh');
    expect(auth.user()?.fullName).toBe('Trần Thị Hạnh');
  });

  it('logout xóa phiên và lấy token antiforgery ẩn danh mới', async () => {
    const login = auth.login('a', 'b');
    backend.expectOne(apiUrl('/auth/login')).flush({ antiforgeryToken: 'user-token', user: TEACHER });
    await login;

    const pending = auth.logout();
    backend.expectOne(apiUrl('/auth/logout')).flush(null, { status: 204, statusText: 'No Content' });
    await Promise.resolve();
    await Promise.resolve();
    backend.expectOne(apiUrl('/auth/antiforgery')).flush({ antiforgeryToken: 'fresh-anon' });
    await pending;

    expect(auth.user()).toBeNull();
    let token = '';
    antiforgery.token().subscribe((value) => (token = value));
    expect(token).toBe('fresh-anon');
  });
});
