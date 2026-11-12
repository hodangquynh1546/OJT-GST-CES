import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { fundApiUrl } from '../../../core/config/api.config';
import { apiErrorInterceptor } from '../../../core/interceptors/api-error.interceptor';
import { Member } from '../members.models';
import { MembersPage } from './members-page';

const MEMBERS: Member[] = [
  { fullName: 'Trần Văn Hùng', role: 'Teacher' },
  { fullName: 'Phạm Thu Trang', role: 'Treasurer' },
  { fullName: 'Lê Gia Bảo', role: 'Student' },
  { fullName: 'Võ Gia Huy', role: 'Student' },
];

describe('MembersPage (T-03)', () => {
  let fixture: ComponentFixture<MembersPage>;
  let backend: HttpTestingController;

  const element = () => fixture.nativeElement as HTMLElement;
  const text = () => element().textContent ?? '';
  const names = () => [...element().querySelectorAll('.row .name')].map((item) => item.textContent?.trim());
  const chip = (label: string) =>
    [...element().querySelectorAll<HTMLButtonElement>('.chip')].find((item) => item.textContent?.includes(label))!;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
        provideNoopAnimations(),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(MembersPage);
    fixture.componentRef.setInput('fundId', 'f1');
    fixture.detectChanges();
  });

  afterEach(() => backend.verify());

  function load(list: Member[] = MEMBERS): void {
    backend.expectOne(fundApiUrl('f1', '/members')).flush(list);
    fixture.detectChanges();
  }

  it('lists members with initial, name and role tag, and the view-only note; no email or username', () => {
    load();

    expect(names()).toEqual(['Trần Văn Hùng', 'Phạm Thu Trang', 'Lê Gia Bảo', 'Võ Gia Huy']);
    const first = element().querySelector('.row')!;
    expect(first.querySelector('.avatar')?.textContent?.trim()).toBe('H');
    expect(first.querySelector('.role')?.textContent?.trim()).toBe('GVCN');
    expect(text()).toContain('chỉ xem');
    expect(text()).toContain('Không hiển thị email, tên đăng nhập');
    expect(text()).not.toContain('(Bạn)');
  });

  it('chips show counts and filter by role', () => {
    load();

    expect(chip('Tất cả').textContent).toContain('(4)');
    expect(chip('GVCN').textContent).toContain('(1)');
    expect(chip('Thủ quỹ').textContent).toContain('(1)');
    expect(chip('Học sinh').textContent).toContain('(2)');

    chip('Học sinh').click();
    fixture.detectChanges();
    expect(names()).toEqual(['Lê Gia Bảo', 'Võ Gia Huy']);
    expect(chip('Học sinh').getAttribute('aria-checked')).toBe('true');

    chip('Tất cả').click();
    fixture.detectChanges();
    expect(names().length).toBe(4);
  });

  it('a 403 shows the forbidden state', () => {
    backend
      .expectOne(fundApiUrl('f1', '/members'))
      .flush({ status: 403, code: 'forbidden' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(text()).toContain('Trang này không dành cho vai trò của bạn');
  });

  it('retries after a load error', () => {
    backend.expectOne(fundApiUrl('f1', '/members')).error(new ProgressEvent('error'));
    fixture.detectChanges();

    [...element().querySelectorAll<HTMLButtonElement>('button')].find((item) => item.textContent?.trim() === 'Thử lại')!.click();
    load();

    expect(names().length).toBe(4);
  });
});
