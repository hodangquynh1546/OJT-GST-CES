import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { fundApiUrl } from '../../../core/config/api.config';
import { apiErrorInterceptor } from '../../../core/interceptors/api-error.interceptor';
import { AppNotifications } from '../../../core/services/notification.service';
import '../treasurers.errors';
import { StudentOption, TreasurerAssignment } from '../treasurers.models';
import { TreasurersPage } from './treasurers-page';

const assignment = (id: string, studentId: string, studentName: string, extra: Partial<TreasurerAssignment> = {}): TreasurerAssignment => ({
  id,
  studentId,
  studentName,
  isActive: true,
  assignedAt: '2026-09-05T01:00:00Z',
  assignedByName: 'Trần Văn Hùng',
  revokedAt: null,
  revokedByName: null,
  ...extra,
});

const KHANG = assignment('a2', 's1', 'Nguyễn Minh Khang');
const TRANG = assignment('a3', 's2', 'Phạm Thu Trang', { assignedAt: '2026-10-01T00:10:00Z' });
const BAO_REVOKED = assignment('a1', 's3', 'Lê Gia Bảo', {
  isActive: false,
  assignedAt: '2026-09-05T00:55:00Z',
  revokedAt: '2026-09-05T00:58:00Z',
  revokedByName: 'Trần Văn Hùng',
});

const students: StudentOption[] = [
  { id: 's1', fullName: 'Nguyễn Minh Khang', username: 'khang.nguyen.204', accountStatus: 'Active', isTreasurer: true },
];

describe('TreasurersPage (G-03)', () => {
  let fixture: ComponentFixture<TreasurersPage>;
  let backend: HttpTestingController;
  let success: jasmine.Spy;

  const element = () => fixture.nativeElement as HTMLElement;
  const text = () => element().textContent ?? '';
  const buttons = (label: string) =>
    [...element().querySelectorAll<HTMLButtonElement>('button')].filter((item) => item.textContent?.trim() === label);

  const confirmRevoke = () =>
    [...element().querySelectorAll<HTMLButtonElement>('.confirm button')].find((item) => item.textContent?.trim() === 'Thu hồi')!;

  function start(list: TreasurerAssignment[]): void {
    fixture.detectChanges();
    backend.expectOne(fundApiUrl('f1', '/treasurers')).flush(list);
    backend.expectOne(fundApiUrl('f1', '/students')).flush(students);
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
        { provide: AppNotifications, useValue: { success } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TreasurersPage);
    fixture.componentRef.setInput('fundId', 'f1');
  });

  afterEach(() => backend.verify());

  it('lists active treasurers with assigner, time (giờ Việt Nam), username and history', () => {
    start([TRANG, KHANG, BAO_REVOKED]);

    expect(text()).toContain('Đang là thủ quỹ (2)');
    expect(text()).toContain('Phân công 05/09/2026 08:00 bởi Trần Văn Hùng · khang.nguyen.204');
    expect(text()).toContain('Phân công 01/10/2026 07:10 bởi Trần Văn Hùng');
    const rows = [...element().querySelectorAll('tbody tr')].map((row) =>
      [...row.querySelectorAll('td')].map((cell) => cell.textContent?.trim()).join(' | '),
    );
    expect(rows).toEqual([
      'Phạm Thu Trang | 01/10/2026 07:10 · Trần Văn Hùng | — | Đang hiệu lực',
      'Nguyễn Minh Khang | 05/09/2026 08:00 · Trần Văn Hùng | — | Đang hiệu lực',
      'Lê Gia Bảo | 05/09/2026 07:55 · Trần Văn Hùng | 05/09/2026 07:58 · Trần Văn Hùng | Đã thu hồi',
    ]);
    expect(buttons('Thu hồi').every((button) => !button.disabled)).toBeTrue();
    expect(element().querySelector('[role="status"]')).toBeNull();
  });

  it('disables revoke of the last treasurer with the explanation', () => {
    start([KHANG, BAO_REVOKED]);

    const [revoke] = buttons('Thu hồi');
    expect(revoke.disabled).toBeTrue();
    expect(revoke.getAttribute('aria-describedby')).toBe('tr-last-hint');
    expect(text()).toContain('Thủ quỹ cuối cùng – phân công người mới trước');
  });

  it('shows the unassigned banner when there is no active treasurer', () => {
    start([BAO_REVOKED]);

    expect(element().querySelector('[role="status"]')?.textContent).toContain('Quỹ chưa phân công thủ quỹ.');
    expect(text()).toContain('Đang là thủ quỹ (0)');
    expect(text()).toContain('Chưa có thủ quỹ.');
  });

  it('asks for confirmation, revokes, toasts and reloads', () => {
    start([TRANG, KHANG]);

    buttons('Thu hồi')[1].click();
    fixture.detectChanges();
    expect(element().querySelector('[role="alert"]')?.textContent).toContain('Thu hồi quyền thủ quỹ của Nguyễn Minh Khang?');

    confirmRevoke().click();
    const request = backend.expectOne(fundApiUrl('f1', '/treasurers/a2/revoke'));
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
    backend.expectOne(fundApiUrl('f1', '/treasurers')).flush([TRANG, { ...KHANG, isActive: false, revokedAt: '2026-11-05T02:30:00Z', revokedByName: 'Trần Văn Hùng' }]);
    fixture.detectChanges();

    expect(success).toHaveBeenCalledWith('Đã thu hồi quyền thủ quỹ của Nguyễn Minh Khang.');
    expect(text()).toContain('Đang là thủ quỹ (1)');
    expect(text()).toContain('05/11/2026 09:30 · Trần Văn Hùng');
  });

  it('shows treasurer.last from the server (thu hồi đồng thời) and reloads', () => {
    start([TRANG, KHANG]);

    buttons('Thu hồi')[0].click();
    fixture.detectChanges();
    confirmRevoke().click();
    backend
      .expectOne(fundApiUrl('f1', '/treasurers/a3/revoke'))
      .flush({ status: 422, code: 'treasurer.last' }, { status: 422, statusText: 'Unprocessable Entity' });
    backend.expectOne(fundApiUrl('f1', '/treasurers')).flush([TRANG]);
    fixture.detectChanges();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'Không thể thu hồi thủ quỹ cuối cùng. Hãy phân công thủ quỹ mới trước rồi mới thu hồi người cũ.',
    );
    expect(success).not.toHaveBeenCalled();
  });

  it('shows the error state with retry when the list fails', () => {
    fixture.detectChanges();
    backend.expectOne(fundApiUrl('f1', '/treasurers')).flush({ code: 'internal_error' }, { status: 500, statusText: 'Error' });
    backend.expectOne(fundApiUrl('f1', '/students')).flush(students);
    fixture.detectChanges();

    buttons('Thử lại')[0].click();
    backend.expectOne(fundApiUrl('f1', '/treasurers')).flush([KHANG]);
    fixture.detectChanges();
    expect(text()).toContain('Đang là thủ quỹ (1)');
  });
});
