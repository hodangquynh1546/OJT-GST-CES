import { firstValueFrom, of, throwError, toArray } from 'rxjs';

import { ApiError } from '../models/api-error';
import { LOADING, stateKind, toLoadState } from './load-state';

describe('LoadState', () => {
  it('phát loading rồi ready', async () => {
    const states = await firstValueFrom(toLoadState(of([1])).pipe(toArray()));
    expect(states).toEqual([LOADING, { status: 'ready', data: [1] }]);
  });

  it('lỗi API giữ nguyên ApiError, lỗi khác thành unknown_error', async () => {
    const apiError = new ApiError(404, 'not_found', 'x');
    const [, failed] = await firstValueFrom(toLoadState(throwError(() => apiError)).pipe(toArray()));
    expect(failed).toEqual({ status: 'error', error: apiError });

    const [, other] = await firstValueFrom(toLoadState(throwError(() => new Error('x'))).pipe(toArray()));
    expect(other.status === 'error' && other.error.code).toBe('unknown_error');
  });

  it('đổi state thành kind của app-state', () => {
    expect(stateKind(LOADING)).toBe('loading');
    expect(stateKind({ status: 'ready', data: [] })).toBe('empty');
    expect(stateKind({ status: 'ready', data: [1] })).toBeNull();
    expect(stateKind({ status: 'error', error: new ApiError(403, 'forbidden', 'x') })).toBe('forbidden');
    expect(stateKind({ status: 'error', error: new ApiError(404, 'not_found', 'x') })).toBe('not-found');
    expect(stateKind({ status: 'error', error: new ApiError(500, 'internal_error', 'x') })).toBe('error');
  });
});
