import { AbstractControl, ValidationErrors } from '@angular/forms';

import { STUDENT_NAME_MAX_LENGTH, USERNAME_MAX_LENGTH } from './students.models';

export const STUDENT_NAME_ERROR = `Nhập họ tên học sinh (tối đa ${STUDENT_NAME_MAX_LENGTH} ký tự).`;
export const EMAIL_ERROR = 'Email phụ huynh không hợp lệ.';
export const EMAIL_REQUIRED_ERROR = 'Nhập email phụ huynh.';
export const USERNAME_ERROR = `3–${USERNAME_MAX_LENGTH} ký tự: chữ thường không dấu, số, dấu chấm, gạch dưới, gạch ngang.`;

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const USERNAME_PATTERN = /^[a-z0-9._-]{3,60}$/;

/** Giống backend: 1–100 ký tự sau khi cắt khoảng trắng. */
export function studentNameValidator(control: AbstractControl<string | null>): ValidationErrors | null {
  const name = (control.value ?? '').trim();
  return name.length === 0 || name.length > STUDENT_NAME_MAX_LENGTH ? { studentName: true } : null;
}

/** Email tùy chọn: để trống là hợp lệ. */
export function optionalEmailValidator(control: AbstractControl<string | null>): ValidationErrors | null {
  const email = (control.value ?? '').trim();
  return email === '' || (email.length <= 254 && EMAIL_PATTERN.test(email)) ? null : { email: true };
}

export function requiredEmailValidator(control: AbstractControl<string | null>): ValidationErrors | null {
  const email = (control.value ?? '').trim();
  if (email === '') {
    return { required: true };
  }
  return email.length <= 254 && EMAIL_PATTERN.test(email) ? null : { email: true };
}

/** Tên đăng nhập tùy chọn (bỏ trống → hệ thống gợi ý); backend chuẩn hóa chữ thường nên kiểm tra sau khi hạ chữ thường. */
export function optionalUsernameValidator(control: AbstractControl<string | null>): ValidationErrors | null {
  const username = (control.value ?? '').trim().toLowerCase();
  return username === '' || USERNAME_PATTERN.test(username) ? null : { username: true };
}
