import { Observable, catchError, map, of, startWith } from 'rxjs';

import { ApiError } from '../models/api-error';

/**
 * Quy ước quản lý state (docs/architecture/foundation-guide.md): không dùng store toàn cục;
 * state của màn hình là signal trong component / service của module, dữ liệu tải về đi qua `LoadState`.
 */
export type LoadState<T> =
  | { readonly status: 'loading' }
  | { readonly status: 'ready'; readonly data: T }
  | { readonly status: 'error'; readonly error: ApiError };

export const LOADING: LoadState<never> = { status: 'loading' };

/** Bọc một request: phát `loading` → `ready` / `error`. Dùng với `toSignal(..., { initialValue: LOADING })`. */
export function toLoadState<T>(source: Observable<T>): Observable<LoadState<T>> {
  return source.pipe(
    map((data): LoadState<T> => ({ status: 'ready', data })),
    catchError((error: unknown) =>
      of<LoadState<T>>({
        status: 'error',
        error: error instanceof ApiError ? error : new ApiError(0, 'unknown_error', 'Đã có lỗi xảy ra. Vui lòng thử lại.'),
      }),
    ),
    startWith(LOADING),
  );
}

/** Kind cho `<app-state>` theo state: loading / error / forbidden / not-found / empty; null khi có dữ liệu để hiện. */
export function stateKind<T>(
  state: LoadState<T>,
  isEmpty: (data: T) => boolean = (data) => Array.isArray(data) && data.length === 0,
): 'loading' | 'error' | 'forbidden' | 'not-found' | 'empty' | null {
  switch (state.status) {
    case 'loading':
      return 'loading';
    case 'error':
      return state.error.status === 403 ? 'forbidden' : state.error.status === 404 ? 'not-found' : 'error';
    case 'ready':
      return isEmpty(state.data) ? 'empty' : null;
  }
}
