import { AbstractControl, ValidationErrors } from '@angular/forms';

import { FUND_NAME_MAX_LENGTH } from './funds.models';

/** Thông báo dưới ô "Tên quỹ" khi sai (Canvas G-01 dialog, G-04). */
export const FUND_NAME_ERROR = `Nhập tên quỹ (tối đa ${FUND_NAME_MAX_LENGTH} ký tự).`;

/** Giống validator backend: 1–100 ký tự tính sau khi cắt khoảng trắng. */
export function fundNameValidator(control: AbstractControl<string | null>): ValidationErrors | null {
  const name = (control.value ?? '').trim();
  return name.length === 0 || name.length > FUND_NAME_MAX_LENGTH ? { fundName: true } : null;
}
