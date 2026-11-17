import { AbstractControl, FormGroup } from '@angular/forms';

import { ApiError } from '../models/api-error';

/**
 * Gắn lỗi từ API vào form (design-system.md mục 6 – lỗi hiện sau khi bấm Lưu):
 * - 400 `validation_failed`: `errors[field]` → control cùng tên (camelCase) nhận lỗi `{ server: message }`.
 * - Lỗi nghiệp vụ (422 / 409…) có `fieldFor[code]` → gắn vào control đó.
 * Trả về thông báo chung còn lại để hiện ở hộp tóm tắt đầu form (null nếu mọi lỗi đã gắn vào ô).
 */
export function applyServerErrors(
  form: FormGroup,
  error: unknown,
  fieldFor: Readonly<Record<string, string>> = {},
): string | null {
  if (!(error instanceof ApiError)) {
    return 'Đã có lỗi xảy ra. Vui lòng thử lại.';
  }

  let unmatched = false;
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    unmatched = !setServerError(form.get(field), messages[0]) || unmatched;
  }

  const field = fieldFor[error.code];
  if (field && setServerError(form.get(field), error.message)) {
    return null;
  }

  const hasFieldErrors = Object.keys(error.fieldErrors).length > 0;
  return hasFieldErrors && !unmatched ? null : error.message;
}

/** Thông báo lỗi server của một control (dùng trong `kendo-formerror`). */
export function serverError(control: AbstractControl | null): string | null {
  const value = control?.errors?.['server'];
  return typeof value === 'string' ? value : null;
}

function setServerError(control: AbstractControl | null, message: string | undefined): boolean {
  if (!control || !message) {
    return false;
  }
  control.setErrors({ ...(control.errors ?? {}), server: message });
  control.markAsTouched();
  return true;
}
