import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { fundApiUrl } from '../../../core/config/api.config';
import { apiErrorInterceptor } from '../../../core/interceptors/api-error.interceptor';
import '../treasurers.errors';
import { StudentOption } from '../treasurers.models';
import { AssignTreasurerDialog, AssignedTreasurer } from './assign-treasurer-dialog';

const students: StudentOption[] = [
  { id: 's1', fullName: 'Lê Gia Bảo', username: null, accountStatus: 'None', isTreasurer: false },
  { id: 's2', fullName: 'Phạm Gia Huy', username: 'huy.pham.482', accountStatus: 'PendingActivation', isTreasurer: false },
  { id: 's3', fullName: 'Nguyễn Minh Khang', username: 'khang.nguyen.317', accountStatus: 'Active', isTreasurer: false },
  { id: 's4', fullName: 'Trần Quốc Việt', username: 'viet.tran.095', accountStatus: 'Disabled', isTreasurer: false },
  { id: 's5', fullName: 'Phạm Thu Trang', username: 'trang.pham.318', accountStatus: 'Active', isTreasurer: false },
];

describe('AssignTreasurerDialog (G-03)', () => {
  let fixture: ComponentFixture<AssignTreasurerDialog>;
  let backend: HttpTestingController;
  let assigned: AssignedTreasurer[];

  const root = () => fixture.nativeElement as HTMLElement;
  const radios = () => [...root().querySelectorAll<HTMLInputElement>('input[type="radio"]')];
  const option = (name: string) =>
    [...root().querySelectorAll<HTMLLabelElement>('label.opt')].find((label) => label.textContent?.includes(name))!;
  const button = (label: string) =>
    [...root().querySelectorAll<HTMLButtonElement>('kendo-dialog button')].find((item) => item.textContent?.trim() === label)!;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting(), provideNoopAnimations()],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AssignTreasurerDialog);
    fixture.componentRef.setInput('fundId', 'f1');
    fixture.componentRef.setInput('students', { status: 'ready', data: students });
    fixture.componentRef.setInput('activeStudentIds', ['s5']);
    assigned = [];
    fixture.componentInstance.assigned.subscribe((value) => assigned.push(value));
    fixture.detectChanges();
  });

  afterEach(() => {
    backend.verify();
    fixture.destroy();
  });

  it('only lets Active students who are not treasurers be picked, with the reason beside each name', () => {
    expect(radios().map((radio) => radio.disabled)).toEqual([true, true, false, true, true]);
    expect(option('Lê Gia Bảo').textContent).toContain('Cấp tài khoản ở trang Học sinh trước');
    expect(option('Lê Gia Bảo').textContent).toContain('Chưa có tài khoản');
    expect(option('Phạm Gia Huy').textContent).toContain('huy.pham.482 · chưa đổi mật khẩu tạm');
    expect(option('Phạm Gia Huy').textContent).toContain('Chờ kích hoạt');
    expect(option('Nguyễn Minh Khang').textContent).toContain('Đã kích hoạt');
    expect(option('Trần Quốc Việt').textContent).toContain('Bị khóa');
    expect(option('Phạm Thu Trang').textContent).toContain('Đang là thủ quỹ');
  });

  it('requires a pick before assigning', () => {
    button('Phân công').click();
    fixture.detectChanges();

    expect(root().querySelector('kendo-dialog [role="alert"]')?.textContent).toContain('Chọn một học sinh đủ điều kiện.');
  });

  it('assigns the picked student and emits its name', () => {
    radios()[2].click();
    fixture.detectChanges();
    button('Phân công').click();

    const request = backend.expectOne(fundApiUrl('f1', '/treasurers'));
    expect(request.request.body).toEqual({ studentId: 's3' });
    request.flush({ id: 'a9' }, { status: 201, statusText: 'Created' });

    expect(assigned).toEqual([{ id: 'a9', studentName: 'Nguyễn Minh Khang' }]);
  });

  it('shows the server reason when the student is no longer eligible', () => {
    radios()[2].click();
    fixture.detectChanges();
    button('Phân công').click();
    backend
      .expectOne(fundApiUrl('f1', '/treasurers'))
      .flush({ status: 422, code: 'treasurer.account_disabled' }, { status: 422, statusText: 'Unprocessable Entity' });
    fixture.detectChanges();

    expect(root().querySelector('kendo-dialog .cf-alert--danger')?.textContent).toContain('Tài khoản của học sinh đang bị khóa.');
    expect(assigned).toEqual([]);
  });
});
