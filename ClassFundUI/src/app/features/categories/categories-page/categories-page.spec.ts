import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

import { fundApiUrl } from '../../../core/config/api.config';
import { apiErrorInterceptor } from '../../../core/interceptors/api-error.interceptor';
import { AppNotifications } from '../../../core/services/notification.service';
import '../categories.errors';
import { Category } from '../categories.models';
import { CategoriesPage } from './categories-page';

const CATEGORIES: Category[] = [
  { id: 'c1', type: 'Income', name: 'Đóng quỹ', isHidden: false, transactionCount: 5 },
  { id: 'c2', type: 'Income', name: 'Thu khác', isHidden: false, transactionCount: 0 },
  { id: 'c3', type: 'Expense', name: 'Văn phòng phẩm', isHidden: false, transactionCount: 2 },
  { id: 'c4', type: 'Expense', name: 'Photo tài liệu', isHidden: true, transactionCount: 1 },
];

describe('CategoriesPage (T-02)', () => {
  let fixture: ComponentFixture<CategoriesPage>;
  let backend: HttpTestingController;
  let success: jasmine.Spy;

  const element = () => fixture.nativeElement as HTMLElement;
  const text = () => element().textContent ?? '';
  const button = (label: string) =>
    [...element().querySelectorAll<HTMLButtonElement>('button')].find(
      (item) => item.textContent?.trim() === label || item.getAttribute('aria-label') === label,
    )!;
  /** Nút "+ Thêm" của nhóm: nhóm thu đứng trước nhóm chi. */
  const addButton = (kind: 'Income' | 'Expense') =>
    [...element().querySelectorAll<HTMLButtonElement>('button')].filter((item) => item.textContent?.trim() === '+ Thêm')[
      kind === 'Income' ? 0 : 1
    ];
  const input = (id: string) => element().querySelector<HTMLInputElement>(`#${id}`)!;
  const type = (id: string, value: string) => {
    input(id).value = value;
    input(id).dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };
  const listRequest = (): TestRequest =>
    backend.expectOne((request) => request.url === fundApiUrl('f1', '/categories') && request.method === 'GET');

  function start(list: Category[] = CATEGORIES): void {
    fixture.detectChanges();
    const request = listRequest();
    expect(request.request.params.get('includeHidden')).toBe('true');
    request.flush(list);
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
    fixture = TestBed.createComponent(CategoriesPage);
    fixture.componentRef.setInput('fundId', 'f1');
  });

  afterEach(() => backend.verify());

  it('groups income and expense with counts, a hidden tag and transaction counts', () => {
    start();

    const sections = [...element().querySelectorAll('section.card')].map((section) => section.textContent ?? '');
    expect(sections[0]).toContain('Danh mục thu');
    expect(sections[0]).toContain('2 đang dùng · 0 đã ẩn');
    expect(sections[0]).toContain('5 khoản');
    expect(sections[1]).toContain('Danh mục chi');
    expect(sections[1]).toContain('1 đang dùng · 1 đã ẩn');
    expect(sections[1]).toContain('Đã ẩn');
    expect(button('Hiện lại Photo tài liệu')).toBeTruthy();
    expect(button('Ẩn Văn phòng phẩm')).toBeTruthy();
  });

  it('adds a category to the right type, clears the box and reloads', () => {
    start();
    type('cat-new-Expense', '  Quà 20/11 ');

    addButton('Expense').click();
    const request = backend.expectOne((item) => item.method === 'POST');
    expect(request.request.url).toBe(fundApiUrl('f1', '/categories'));
    expect(request.request.body).toEqual({ type: 'Expense', name: 'Quà 20/11' });
    request.flush({ id: 'c9' }, { status: 201, statusText: 'Created' });
    fixture.detectChanges();

    expect(success).toHaveBeenCalledWith('Đã thêm danh mục “Quà 20/11”.');
    expect(input('cat-new-Expense').value).toBe('');
    listRequest().flush([...CATEGORIES, { id: 'c9', type: 'Expense', name: 'Quà 20/11', isHidden: false, transactionCount: 0 }]);
    fixture.detectChanges();
    expect(text()).toContain('Quà 20/11');
  });

  it('requires a name before adding', () => {
    start();

    addButton('Income').click();
    fixture.detectChanges();

    expect(text()).toContain('Nhập tên danh mục.');
    backend.expectNone((request) => request.method === 'POST');
  });

  it('rejects a duplicate name (even of a hidden category) without calling the API', () => {
    start();
    type('cat-new-Expense', 'PHOTO TÀI LIỆU');

    addButton('Expense').click();
    fixture.detectChanges();

    expect(text()).toContain('Đã có danh mục chi tên “PHOTO TÀI LIỆU”.');
    backend.expectNone((request) => request.method === 'POST');
  });

  it('shows the server name_taken error in the box when someone adds the same name at the same time', () => {
    start();
    type('cat-new-Income', 'Tiền hè');
    addButton('Income').click();
    backend
      .expectOne((item) => item.method === 'POST')
      .flush({ status: 422, code: 'category.name_taken' }, { status: 422, statusText: 'Unprocessable Entity' });
    fixture.detectChanges();

    expect(text()).toContain('Đã có danh mục thu tên “Tiền hè”.');
    expect(success).not.toHaveBeenCalled();
    listRequest().flush(CATEGORIES);
  });

  it('renames in place, sending the current hidden flag', () => {
    start();
    button('Đổi tên Văn phòng phẩm').click();
    fixture.detectChanges();
    expect(input('cat-edit-c3').value).toBe('Văn phòng phẩm');
    type('cat-edit-c3', 'Dụng cụ học tập');

    button('Lưu').click();
    const request = backend.expectOne((item) => item.method === 'PUT');
    expect(request.request.url).toBe(fundApiUrl('f1', '/categories/c3'));
    expect(request.request.body).toEqual({ name: 'Dụng cụ học tập', isHidden: false });
    request.flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();

    expect(success).toHaveBeenCalledWith('Đã đổi tên danh mục thành “Dụng cụ học tập”.');
    expect(element().querySelector('#cat-edit-c3')).toBeNull();
    listRequest().flush(CATEGORIES);
  });

  it('rename to the same name just closes the editor; rename to another name of the type shows an error', () => {
    start();
    button('Đổi tên Văn phòng phẩm').click();
    fixture.detectChanges();
    type('cat-edit-c3', 'photo tài liệu');
    button('Lưu').click();
    fixture.detectChanges();
    expect(text()).toContain('Đã có danh mục chi tên “photo tài liệu”.');

    type('cat-edit-c3', ' Văn phòng phẩm ');
    button('Lưu').click();
    fixture.detectChanges();

    backend.expectNone((request) => request.method === 'PUT');
    expect(element().querySelector('#cat-edit-c3')).toBeNull();
  });

  it('Hủy closes the editor without calling the API', () => {
    start();
    button('Đổi tên Đóng quỹ').click();
    fixture.detectChanges();

    button('Hủy').click();
    fixture.detectChanges();

    expect(element().querySelector('#cat-edit-c1')).toBeNull();
    backend.expectNone((request) => request.method === 'PUT');
  });

  it('hides and shows a category again by sending the same name with the flipped flag', () => {
    start();

    button('Ẩn Văn phòng phẩm').click();
    const hide = backend.expectOne((item) => item.method === 'PUT');
    expect(hide.request.body).toEqual({ name: 'Văn phòng phẩm', isHidden: true });
    hide.flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();
    expect(success).toHaveBeenCalledWith('Đã ẩn “Văn phòng phẩm”.');
    listRequest().flush(CATEGORIES);
    fixture.detectChanges();

    button('Hiện lại Photo tài liệu').click();
    const show = backend.expectOne((item) => item.method === 'PUT');
    expect(show.request.url).toBe(fundApiUrl('f1', '/categories/c4'));
    expect(show.request.body).toEqual({ name: 'Photo tài liệu', isHidden: false });
    show.flush(null, { status: 204, statusText: 'No Content' });
    expect(success).toHaveBeenCalledWith('Đã hiện lại “Photo tài liệu”.');
    listRequest().flush(CATEGORIES);
  });

  it('a 403 on load shows the forbidden state', () => {
    fixture.detectChanges();
    listRequest().flush({ status: 403, code: 'forbidden' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(text()).toContain('Trang này không dành cho vai trò của bạn');
  });

  it('retries after a load error', () => {
    fixture.detectChanges();
    listRequest().error(new ProgressEvent('error'));
    fixture.detectChanges();

    button('Thử lại').click();
    listRequest().flush(CATEGORIES);
    fixture.detectChanges();

    expect(text()).toContain('Danh mục thu');
  });
});
