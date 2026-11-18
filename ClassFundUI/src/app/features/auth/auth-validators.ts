import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Quy tắc khớp backend: `PasswordPolicy` (BR-28) và `EmailAddress` (BR-27). Backend vẫn kiểm tra lại. */
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 128;
export const EMAIL_MAX_LENGTH = 254;
export const FULL_NAME_MAX_LENGTH = 100;

export const PASSWORD_POLICY_MESSAGE = 'Mật khẩu cần 8–128 ký tự, có cả chữ và số.';

export function isPasswordPolicySatisfied(password: string): boolean {
  return (
    password.length >= PASSWORD_MIN_LENGTH &&
    password.length <= PASSWORD_MAX_LENGTH &&
    /\p{L}/u.test(password) &&
    /\p{N}/u.test(password)
  );
}

export function isValidEmail(email: string): boolean {
  const value = email.trim().toLowerCase();
  if (value.length === 0 || value.length > EMAIL_MAX_LENGTH || value.includes(' ')) {
    return false;
  }
  const at = value.indexOf('@');
  return at > 0 && at === value.lastIndexOf('@') && value.indexOf('.', at) > at + 1 && !value.endsWith('.');
}

/** Bỏ qua giá trị rỗng: để `Validators.required` báo lỗi riêng. */
export const passwordPolicyValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value as string | null;
  return !value || isPasswordPolicySatisfied(value) ? null : { passwordPolicy: true };
};

export const emailFormatValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value as string | null;
  return !value || isValidEmail(value) ? null : { email: true };
};

/** Nhập lại mật khẩu: đặt trên FormGroup, lỗi gắn vào control xác nhận để hiện đúng ô. */
export function passwordsMatchValidator(passwordKey: string, confirmKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const confirm = group.get(confirmKey);
    if (!confirm) {
      return null;
    }
    const mismatch = !!confirm.value && confirm.value !== group.get(passwordKey)?.value;
    const { mismatch: _removed, ...others } = confirm.errors ?? {};
    confirm.setErrors(mismatch ? { ...others, mismatch: true } : Object.keys(others).length > 0 ? others : null);
    return null;
  };
}

/** Mật khẩu mới phải khác mật khẩu hiện tại (backend: `auth.password_unchanged`). */
export function differsFromValidator(currentKey: string, newKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const next = group.get(newKey);
    if (!next) {
      return null;
    }
    const same = !!next.value && next.value === group.get(currentKey)?.value;
    const { unchanged: _removed, ...others } = next.errors ?? {};
    next.setErrors(same ? { ...others, unchanged: true } : Object.keys(others).length > 0 ? others : null);
    return null;
  };
}
