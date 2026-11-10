import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { CurrentUser } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { apiUrl } from '../../../core/config/api.config';
import { AppNotifications } from '../../../core/services/notification.service';
import { FundSummary } from '../funds.models';
import { FundList } from './fund-list';

const teacher: CurrentUser = {
  id: 'u1',
  kind: 'Teacher',
  fullName: 'Trần Văn Hùng',
  mustChangePassword: false,
  accountPermissions: ['Fund.Create'],
  memberships: [],
};

const fund = (overrides: Partial<FundSummary>): FundSummary => ({
  id: 'f1',
  name: 'Quỹ lớp 5A',
  role: 'Teacher',
  permissions: ['Fund.View', 'Fund.Manage'],
  currentBalance: 3_720_000,
  createdAt: '2026-08-15T02:00:00+00:00',
  treasurerStatus: 'Assigned',
  treasurerNames: ['Phạm Thu Trang'],
  studentCount: 34,
  ...overrides,
});

describe('FundList (G-01)', () => {
  let fixture: ComponentFixture<FundList>;
  let backend: HttpTestingController;
  let user: ReturnType<typeof signal<CurrentUser | null>>;
  let restoreSession: jasmine.Spy;
  let success: jasmine.Spy;

  const render = (funds: FundSummary[]) => {
    fixture = TestBed.createComponent(FundList);
    fixture.detectChanges();
    backend.expectOne(apiUrl('/funds')).flush(funds);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  beforeEach(() => {
    user = signal<CurrentUser | null>(teacher);
    restoreSession = jasmine.createSpy('restoreSession').and.resolveTo();
    success = jasmine.createSpy('success');
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: AuthService, useValue: { user, restoreSession } },
        { provide: AppNotifications, useValue: { success } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('GVCN: quỹ đã có thủ quỹ mở S-01, quỹ chưa phân công mở G-03', () => {
    const element = render([fund({}), fund({ id: 'f2', name: 'Quỹ dã ngoại', treasurerStatus: 'Unassigned', treasurerNames: [] })]);
    const cards = element.querySelectorAll<HTMLAnchorElement>('a.fund');

    expect(cards.length).toBe(2);
    expect(cards[0].getAttribute('href')).toBe('/funds/f1');
    expect(cards[0].textContent).toContain('Đã có thủ quỹ');
    expect(cards[0].textContent).toContain('34 học sinh');
    expect(cards[0].textContent).toContain('15/08/2026');
    expect(cards[1].getAttribute('href')).toBe('/funds/f2/treasurers');
    expect(cards[1].classList).toContain('fund--unassigned');
    expect(cards[1].textContent).toContain('Phân công thủ quỹ →');
    expect(element.textContent).toContain('Các quỹ bạn là giáo viên chủ nhiệm · 2 quỹ');
  });

  it('phụ huynh: tiêu đề "Quỹ của con", thẻ có tên con, không có nút tạo quỹ', () => {
    user.set({ ...teacher, kind: 'Parent', accountPermissions: [] });
    const element = render([
      fund({ role: 'Parent', studentCount: undefined, linkedStudents: ['Phạm Gia Huy', 'Phạm Gia Hân'] }),
    ]);

    expect(element.querySelector('h1')?.textContent).toBe('Quỹ của con');
    const card = element.querySelector('a.fund')!;
    expect(card.textContent).toContain('Con: Phạm Gia Huy, Phạm Gia Hân');
    expect(card.textContent).not.toContain('học sinh');
    expect(card.classList).toContain('fund--child');
    expect(element.textContent).not.toContain('+ Tạo quỹ');
  });

  it('GVCN chưa có quỹ: trạng thái rỗng có nút "+ Tạo quỹ đầu tiên"', () => {
    const element = render([]);
    expect(element.textContent).toContain('Bạn chưa có quỹ nào');
    expect(element.textContent).toContain('+ Tạo quỹ đầu tiên');
  });

  it('sau khi tạo quỹ: thông báo, tải lại danh sách, đọc lại phiên (/auth/me)', async () => {
    render([]);

    await (fixture.componentInstance as unknown as { onCreated(f: { id: string; name: string }): Promise<void> }).onCreated({
      id: 'n1',
      name: 'Quỹ lớp 4C',
    });
    backend.expectOne(apiUrl('/funds')).flush([fund({ id: 'n1', name: 'Quỹ lớp 4C', treasurerStatus: 'Unassigned', treasurerNames: [], studentCount: 0 })]);
    fixture.detectChanges();

    expect(success).toHaveBeenCalledWith('Đã tạo “Quỹ lớp 4C”. Bước tiếp: thêm học sinh rồi phân công thủ quỹ.');
    expect(restoreSession).toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('a.fund').length).toBe(1);
  });
});
