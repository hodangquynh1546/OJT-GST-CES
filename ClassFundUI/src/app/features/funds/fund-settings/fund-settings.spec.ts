import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { apiUrl } from '../../../core/config/api.config';
import { AppNotifications } from '../../../core/services/notification.service';
import { FundSummary } from '../funds.models';
import { FundSettings } from './fund-settings';

const summary: FundSummary = {
  id: 'f1',
  name: 'Quỹ lớp 5A',
  role: 'Teacher',
  permissions: ['Fund.View', 'Fund.Manage'],
  currentBalance: 0,
  createdAt: '2026-08-15T02:00:00+00:00',
  treasurerStatus: 'Unassigned',
  treasurerNames: [],
  studentCount: 0,
};

describe('FundSettings (G-04)', () => {
  let fixture: ComponentFixture<FundSettings>;
  let backend: HttpTestingController;
  let restoreSession: jasmine.Spy;
  let success: jasmine.Spy;

  const element = () => fixture.nativeElement as HTMLElement;
  const input = () => element().querySelector<HTMLInputElement>('kendo-textbox input')!;
  const button = (text: string) =>
    [...element().querySelectorAll<HTMLButtonElement>('button')].find((item) => item.textContent?.trim() === text)!;
  const type = (value: string) => {
    input().value = value;
    input().dispatchEvent(new Event('input'));
  };

  beforeEach(() => {
    restoreSession = jasmine.createSpy('restoreSession').and.resolveTo();
    success = jasmine.createSpy('success');
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: AuthService, useValue: { user: signal({ fullName: 'Trần Văn Hùng' }), restoreSession } },
        { provide: AppNotifications, useValue: { success } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(FundSettings);
    fixture.componentRef.setInput('fundId', 'f1');
    fixture.detectChanges();
    backend.expectOne(apiUrl('/funds')).flush([summary]);
    fixture.detectChanges();
  });

  afterEach(() => backend.verify());

  it('hiện thông tin quỹ từ GET /funds', () => {
    expect(input().value).toBe('Quỹ lớp 5A');
    expect(element().textContent).toContain('15/08/2026');
    expect(element().textContent).toContain('Trần Văn Hùng (bạn)');
    expect(element().textContent).toContain('0 học sinh');
  });

  it('tên trống: hiện lỗi sau khi bấm Lưu, không gọi API', () => {
    type('  ');
    button('Lưu').click();
    fixture.detectChanges();

    expect(element().querySelector('kendo-formerror')?.textContent).toContain('Nhập tên quỹ (tối đa 100 ký tự).');
    backend.expectNone(apiUrl('/funds/f1'));
  });

  it('Lưu gửi tên đã cắt khoảng trắng, thông báo và đọc lại phiên để header đổi tên', async () => {
    type('  Quỹ lớp 5A – 2026 ');
    button('Lưu').click();
    const request = backend.expectOne(apiUrl('/funds/f1'));
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ name: 'Quỹ lớp 5A – 2026' });
    request.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(success).toHaveBeenCalledWith('Đã đổi tên quỹ.');
    expect(restoreSession).toHaveBeenCalled();
    expect(input().value).toBe('Quỹ lớp 5A – 2026');
  });

  it('Hoàn tác trả về tên đang lưu và bỏ lỗi', () => {
    type('');
    button('Lưu').click();
    fixture.detectChanges();
    button('Hoàn tác').click();
    fixture.detectChanges();

    expect(input().value).toBe('Quỹ lớp 5A');
    expect(element().querySelector('kendo-formerror')).toBeNull();
  });

  it('quỹ không có trong danh sách → 404', () => {
    fixture.componentRef.setInput('fundId', 'khac');
    fixture.detectChanges();
    backend.expectOne(apiUrl('/funds')).flush([summary]);
    fixture.detectChanges();
    expect(element().textContent).toContain('Không tìm thấy trang');
  });
});
