import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { OverviewApi } from './overview.api';

describe('OverviewApi', () => {
  let api: OverviewApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(OverviewApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets the overview of one fund', () => {
    api.get('fund 1').subscribe();

    const request = http.expectOne('/api/v1/funds/fund%201');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });
});
