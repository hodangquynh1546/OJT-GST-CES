import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AntiforgeryService } from '../services/antiforgery.service';
import { CurrentUser } from './auth.models';
import { AuthService } from './auth.service';

const USER: CurrentUser = {
  id: 'u1',
  email: 'an@example.com',
  fullName: 'Nguyễn An',
  mustChangePassword: false,
  accountPermissions: ['Fund.Create'],
  memberships: [],
};

describe('AuthService (M1)', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  let antiforgery: AntiforgeryService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    antiforgery = TestBed.inject(AntiforgeryService);
  });

  afterEach(() => http.verify());

  it('đăng nhập: gửi email + mật khẩu, lưu người dùng và token antiforgery mới', async () => {
    const done = auth.login('an@example.com', 'Passw0rd1');
    const request = http.expectOne('/api/v1/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'an@example.com', password: 'Passw0rd1' });
    request.flush({ antiforgeryToken: 'token-moi', user: USER });

    expect(await done).toEqual(USER);
    expect(auth.user()).toEqual(USER);
    expect(auth.isAuthenticated()).toBeTrue();
    expect(auth.canCreateFund()).toBeTrue();
    antiforgery.token().subscribe((token) => expect(token).toBe('token-moi'));
  });

  it('đăng ký: tài khoản mới đăng nhập luôn', async () => {
    const done = auth.register({ fullName: 'Nguyễn An', email: 'an@example.com', password: 'Passw0rd1' });
    const request = http.expectOne('/api/v1/auth/register');
    expect(request.request.body).toEqual({ fullName: 'Nguyễn An', email: 'an@example.com', password: 'Passw0rd1' });
    request.flush({ antiforgeryToken: 't', user: USER });

    await done;
    expect(auth.isAuthenticated()).toBeTrue();
  });

  it('đổi mật khẩu: cập nhật phiên (hết cờ mật khẩu tạm) và điều hướng về trang chính', async () => {
    const temp: CurrentUser = { ...USER, mustChangePassword: true };
    const login = auth.login('an@example.com', 'Temp');
    http.expectOne('/api/v1/auth/login').flush({ antiforgeryToken: 't', user: temp });
    await login;
    expect(auth.homeUrl()).toBe('/change-password');

    const change = auth.changePassword('Temp', 'Passw0rd1');
    const request = http.expectOne('/api/v1/auth/change-password');
    expect(request.request.body).toEqual({ currentPassword: 'Temp', newPassword: 'Passw0rd1' });
    request.flush({ antiforgeryToken: 't2', user: USER });
    await change;

    expect(auth.user()?.mustChangePassword).toBeFalse();
    expect(auth.homeUrl()).toBe('/funds');
  });

  it('sửa họ tên: PUT /auth/me rồi lưu người dùng mới', async () => {
    const done = auth.updateProfile('Trần Bình');
    const request = http.expectOne('/api/v1/auth/me');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ fullName: 'Trần Bình' });
    request.flush({ ...USER, fullName: 'Trần Bình' });

    expect((await done).fullName).toBe('Trần Bình');
    expect(auth.user()?.fullName).toBe('Trần Bình');
  });

  it('đăng xuất: xóa phiên rồi lấy token antiforgery ẩn danh mới', async () => {
    const login = auth.login('an@example.com', 'Passw0rd1');
    http.expectOne('/api/v1/auth/login').flush({ antiforgeryToken: 't', user: USER });
    await login;

    const done = auth.logout();
    http.expectOne('/api/v1/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    await Promise.resolve();
    http.expectOne('/api/v1/auth/antiforgery').flush({ antiforgeryToken: 'an-danh' });
    await done;

    expect(auth.user()).toBeNull();
    antiforgery.token().subscribe((token) => expect(token).toBe('an-danh'));
  });

  it('đăng xuất: vẫn xóa phiên ở client kể cả khi API lỗi', async () => {
    const login = auth.login('an@example.com', 'Passw0rd1');
    http.expectOne('/api/v1/auth/login').flush({ antiforgeryToken: 't', user: USER });
    await login;

    const done = auth.logout().catch(() => 'rejected');
    http.expectOne('/api/v1/auth/logout').flush(null, { status: 500, statusText: 'Server Error' });
    await Promise.resolve();
    http.expectOne('/api/v1/auth/antiforgery').flush({ antiforgeryToken: 'an-danh' });

    expect(await done).toBe('rejected');
    expect(auth.user()).toBeNull();
  });

  it('khôi phục phiên: lỗi 401 nghĩa là chưa đăng nhập', async () => {
    const done = auth.restoreSession();
    http.expectOne('/api/v1/auth/me').flush({ code: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    await done;

    expect(auth.user()).toBeNull();
    expect(auth.homeUrl()).toBe('/login');
  });

  it('một quỹ → vào thẳng quỹ; nhiều quỹ hoặc chưa có → danh sách quỹ', async () => {
    const one: CurrentUser = {
      ...USER,
      memberships: [{ fundId: 'f1', fundName: 'Lớp 10A1', role: 'Owner', permissions: ['Fund.View'] }],
    };
    const done = auth.login('an@example.com', 'Passw0rd1');
    http.expectOne('/api/v1/auth/login').flush({ antiforgeryToken: 't', user: one });
    await done;

    expect(auth.homeUrl()).toBe('/funds/f1');
  });
});
