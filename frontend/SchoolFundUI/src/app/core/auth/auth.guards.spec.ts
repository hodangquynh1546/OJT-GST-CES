import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
  convertToParamMap,
  provideRouter,
} from '@angular/router';

import { authGuard, fundPermissionGuard, guestGuard } from './auth.guards';
import { CurrentUser } from './auth.models';
import { AuthService } from './auth.service';
import { Permissions } from './permissions';

const user = (overrides: Partial<CurrentUser> = {}): CurrentUser => ({
  id: 'u1',
  kind: 'Teacher',
  fullName: 'Trần Văn Hùng',
  mustChangePassword: false,
  accountPermissions: [],
  memberships: [
    { fundId: 'f1', fundName: 'Quỹ lớp 5A', role: 'Teacher', permissions: [Permissions.FundView] },
  ],
  ...overrides,
});

describe('auth guards', () => {
  const current = signal<CurrentUser | null>(null);

  beforeEach(() => {
    current.set(null);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            user: current,
            isAuthenticated: () => current() !== null,
            homeUrl: () => '/funds',
            membership: (id: string | null) => current()?.memberships.find((m) => m.fundId === id),
          },
        },
      ],
    });
  });

  const run = (guard: CanActivateFn, url = '/funds', fundId?: string) =>
    TestBed.runInInjectionContext(() =>
      guard(
        { paramMap: convertToParamMap(fundId ? { fundId } : {}) } as ActivatedRouteSnapshot,
        { url } as RouterStateSnapshot,
      ),
    );

  const redirect = (result: unknown) =>
    TestBed.inject(Router).serializeUrl(result as UrlTree);

  it('chưa đăng nhập bị chuyển về /login', () => {
    expect(redirect(run(authGuard))).toBe('/login');
  });

  it('còn mật khẩu tạm bị chuyển về /change-password', () => {
    current.set(user({ mustChangePassword: true }));
    expect(redirect(run(authGuard))).toBe('/change-password');
    expect(run(authGuard, '/change-password')).toBeTrue();
  });

  it('đã đăng nhập không vào lại trang khách', () => {
    current.set(user());
    expect(redirect(run(guestGuard))).toBe('/funds');
  });

  it('không thuộc quỹ thì về trang 404', () => {
    current.set(user());
    expect(redirect(run(fundPermissionGuard(Permissions.FundView), '/', 'khac'))).toBe('/not-found');
  });

  it('thiếu quyền thì về trang 403, đủ quyền thì đi tiếp', () => {
    current.set(user());
    expect(redirect(run(fundPermissionGuard(Permissions.TransactionManage), '/', 'f1'))).toBe('/forbidden');
    expect(run(fundPermissionGuard(Permissions.FundView), '/', 'f1')).toBeTrue();
  });
});
