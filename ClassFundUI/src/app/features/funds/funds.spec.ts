import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { apiUrl, fundApiUrl } from '../../core/config/api.config';
import { createFundForm, fundBody } from './fund-form';
import { FundsApi } from './funds.api';
import { currentSchoolYear, displaySchoolYear, isValidSchoolYear, schoolYearOptions } from './funds.models';

describe('funds models', () => {
  it('năm học hợp lệ là hai năm liên tiếp', () => {
    expect(isValidSchoolYear('2026-2027')).toBeTrue();
    expect(isValidSchoolYear('2026 – 2027')).toBeTrue();
    expect(isValidSchoolYear('2026-2028')).toBeFalse();
    expect(isValidSchoolYear('26-27')).toBeFalse();
    expect(isValidSchoolYear(null)).toBeFalse();
  });

  it('năm học hiện tại đổi từ tháng 8', () => {
    expect(currentSchoolYear(new Date(2026, 6, 31))).toBe('2025-2026');
    expect(currentSchoolYear(new Date(2026, 7, 1))).toBe('2026-2027');
    expect(schoolYearOptions(new Date(2026, 10, 18))).toEqual(['2025-2026', '2026-2027', '2027-2028']);
  });

  it('hiển thị năm học như bản thiết kế', () => {
    expect(displaySchoolYear('2026-2027')).toBe('2026–2027');
  });

  it('form quỹ: tên chỉ có khoảng trắng là lỗi, giá trị gửi đi được trim', () => {
    const form = createFundForm({ name: '   ', schoolYear: '2026-2027' });
    expect(form.controls.name.hasError('required')).toBeTrue();
    form.controls.name.setValue('  Lớp 10A1 ');
    expect(form.valid).toBeTrue();
    expect(fundBody(form)).toEqual({ name: 'Lớp 10A1', schoolYear: '2026-2027' });
    form.controls.schoolYear.setValue('2026-2029');
    expect(form.controls.schoolYear.hasError('schoolYear')).toBeTrue();
  });
});

describe('FundsApi', () => {
  let api: FundsApi;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(FundsApi);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('gọi đúng endpoint', () => {
    api.listMine().subscribe();
    expect(backend.expectOne(apiUrl('/funds')).request.method).toBe('GET');

    api.create({ name: 'A', schoolYear: '2026-2027' }).subscribe();
    const create = backend.expectOne(apiUrl('/funds'));
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ name: 'A', schoolYear: '2026-2027' });

    api.get('f1').subscribe();
    backend.expectOne(fundApiUrl('f1'));

    api.update('f1', { name: 'B', schoolYear: '2026-2027' }).subscribe();
    expect(backend.expectOne(fundApiUrl('f1')).request.method).toBe('PUT');
  });
});
