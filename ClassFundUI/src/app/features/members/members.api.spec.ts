import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { fundApiUrl } from '../../core/config/api.config';
import { MembersApi } from './members.api';

describe('MembersApi', () => {
  it('GET /funds/{fundId}/members', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const backend = TestBed.inject(HttpTestingController);
    let result: unknown;

    TestBed.inject(MembersApi).list('f1').subscribe((members) => (result = members));
    const request = backend.expectOne(fundApiUrl('f1', '/members'));
    request.flush([{ fullName: 'Trần Văn Hùng', role: 'Teacher' }]);

    expect(request.request.method).toBe('GET');
    expect(result).toEqual([{ fullName: 'Trần Văn Hùng', role: 'Teacher' }]);
    backend.verify();
  });
});
