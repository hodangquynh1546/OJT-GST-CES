import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { fundApiUrl } from '../../../core/config/api.config';
import { apiErrorInterceptor } from '../../../core/interceptors/api-error.interceptor';
import { vietnamToday } from '../ledger-dates';
import '../transactions.errors';
import { Category } from '../../categories/categories.models';
import { Transaction } from '../transactions.models';
import { TxDialog } from './tx-dialog';

const categories: Category[] = [
  { id: 'c1', type: 'Income', name: 'Đóng quỹ' },
  { id: 'c3', type: 'Expense', name: 'Văn phòng phẩm' },
  { id: 'c5', type: 'Expense', name: 'Khen thưởng', isHidden: true },
];

const saved: Transaction = {
  id: 't1',
  type: 'Expense',
  amount: 180_000,
  occurredOn: '2026-11-05',
  categoryId: 'c3',
  categoryName: 'Văn phòng phẩm',
  description: 'Mua giấy A4',
  isCancelled: false,
  isPublic: true,
};

describe('TxDialog (T-01)', () => {
  let fixture: ComponentFixture<TxDialog>;
  let backend: HttpTestingController;
  let emitted: Transaction[];

  const root = () => fixture.nativeElement as HTMLElement;
  const text = () => root().textContent?.replace(/\s+/g, ' ') ?? '';
  const button = (label: string) =>
    [...root().querySelectorAll<HTMLButtonElement>('button')].find((item) => item.textContent?.trim() === label)!;
  const typeInto = (selector: string, value: string) => {
    const input = root().querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
  };
  const form = () => (fixture.componentInstance as unknown as { form: TxDialog['form'] }).form;

  function create(inputs: Record<string, unknown>): void {
    fixture = TestBed.createComponent(TxDialog);
    fixture.componentRef.setInput('fundId', 'f1');
    fixture.componentRef.setInput('categories', categories);
    for (const [key, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(key, value);
    }
    emitted = [];
    fixture.componentInstance.saved.subscribe((value) => emitted.push(value));
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting(), provideNoopAnimations()],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('shows field errors and the summary only after pressing Lưu', () => {
    create({ addType: 'Expense' });
    expect(text()).toContain('Thêm khoản chi');
    expect(text()).not.toContain('Chưa lưu được');

    button('Lưu').click();
    fixture.detectChanges();

    expect(text()).toContain('Chưa lưu được.');
    expect(text()).toContain('Kiểm tra 3 ô được đánh dấu.');
    expect(text()).toContain('Nhập nội dung (tối đa 200 ký tự).');
    expect(text()).toContain('Số tiền phải là số nguyên lớn hơn 0.');
    expect(text()).toContain('Chọn danh mục.');
  });

  it('creates with type, trimmed values, today and one idempotency key reused on retry', () => {
    create({ addType: 'Expense' });
    form().patchValue({ description: '  Mua giấy A4 ', amount: 180_000, categoryId: 'c3', note: '   ' });

    button('Lưu').click();
    const first = backend.expectOne(fundApiUrl('f1', '/transactions'));
    expect(first.request.body).toEqual(
      jasmine.objectContaining({
        type: 'Expense',
        amount: 180_000,
        occurredOn: vietnamToday(),
        categoryId: 'c3',
        description: 'Mua giấy A4',
        note: null,
        isPublic: true,
      }),
    );
    const key = first.request.body.idempotencyKey as string;
    first.flush({ status: 0 }, { status: 0, statusText: 'Network error' });
    fixture.detectChanges();

    button('Lưu').click();
    const retry = backend.expectOne(fundApiUrl('f1', '/transactions'));
    expect(retry.request.body.idempotencyKey).toBe(key);
    retry.flush(saved, { status: 201, statusText: 'Created' });

    expect(emitted).toEqual([saved]);
  });

  it('only offers visible categories of the same type', () => {
    create({ addType: 'Expense' });
    const choices = (fixture.componentInstance as unknown as { categoryChoices: () => Category[] }).categoryChoices();
    expect(choices.map((category) => category.id)).toEqual(['c3']);
  });

  it('warns before saving an expense larger than the balance and when turning public off', () => {
    create({ addType: 'Expense', balance: 100_000 });
    form().patchValue({ amount: 250_000, isPublic: false });
    fixture.detectChanges();

    expect(text()).toContain('Sau khoản chi này, số dư quỹ sẽ là −150.000 đ');
    expect(text()).toContain('Khoản này sẽ không công khai chi tiết');
  });

  it('edits without type, keeps a hidden current category and maps server errors to fields', () => {
    create({ transaction: { ...saved, categoryId: 'c5', categoryName: 'Khen thưởng', note: 'Có hóa đơn' } });
    expect(text()).toContain('Sửa khoản chi');
    expect(text()).toContain('Không đổi được loại thu ↔ chi');
    const choices = (fixture.componentInstance as unknown as { categoryChoices: () => Category[] }).categoryChoices();
    expect(choices.map((category) => category.id)).toEqual(['c3', 'c5']);

    typeInto('kendo-textbox input', 'Mua giấy A4 – sửa');
    button('Lưu').click();
    const request = backend.expectOne(fundApiUrl('f1', '/transactions/t1'));
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.type).toBeUndefined();
    expect(request.request.body.note).toBe('Có hóa đơn');
    request.flush({ status: 422, code: 'transaction.category_invalid' }, { status: 422, statusText: 'Unprocessable Entity' });
    fixture.detectChanges();

    expect(text()).toContain('Danh mục không hợp lệ (khác loại, đã ẩn hoặc không thuộc quỹ). Chọn danh mục khác.');
    expect(emitted).toEqual([]);
  });
});
