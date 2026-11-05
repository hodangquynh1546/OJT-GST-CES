import { HttpErrorResponse } from '@angular/common/http';

import { messageForCode } from '../utils/error-messages';

interface ProblemBody {
  code?: string;
  title?: string;
  detail?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

/** Lỗi API đã chuẩn hóa từ application/problem+json. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    message: string,
    readonly fieldErrors: Record<string, string[]> = {},
    readonly correlationId?: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  static from(response: HttpErrorResponse): ApiError {
    const body: ProblemBody =
      response.error !== null && typeof response.error === 'object' ? response.error : {};
    const code = body.code ?? (response.status === 0 ? 'network_error' : 'unknown_error');
    return new ApiError(
      response.status,
      code,
      messageForCode(code, body.title),
      body.errors,
      body.correlationId,
    );
  }
}
