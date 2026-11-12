import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { CategoriesApi } from './categories.api';

const FUND = '019a0000-0000-7000-8000-000000000001';

describe('CategoriesApi', () => {
  let api: CategoriesApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(CategoriesApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists categories of the fund with the optional type and includeHidden filters', () => {
    api.list(FUND, { type: 'Expense', includeHidden: true }).subscribe();

    const request = http.expectOne((r) => r.url.endsWith(`/funds/${FUND}/categories`));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('type')).toBe('Expense');
    expect(request.request.params.get('includeHidden')).toBe('true');
    request.flush([]);
  });

  it('sends no query when no filter is given (the hidden ones are left out by the server)', () => {
    api.list(FUND).subscribe();

    const request = http.expectOne((r) => r.url.endsWith(`/funds/${FUND}/categories`));
    expect(request.request.params.keys()).toEqual([]);
    request.flush([]);
  });

  it('creates a category and returns the new id', () => {
    let id = '';
    api.create(FUND, { type: 'Income', name: 'Quyên góp' }).subscribe((value) => (id = value));

    const request = http.expectOne((r) => r.url.endsWith(`/funds/${FUND}/categories`));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ type: 'Income', name: 'Quyên góp' });
    request.flush({ id: 'c-new' });
    expect(id).toBe('c-new');
  });

  it('updates name and isHidden together', () => {
    api.update(FUND, 'c1', { name: 'Đóng quỹ', isHidden: true }).subscribe();

    const request = http.expectOne((r) => r.url.endsWith(`/funds/${FUND}/categories/c1`));
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ name: 'Đóng quỹ', isHidden: true });
    request.flush(null);
  });
});
