import { FormControl, FormGroup } from '@angular/forms';

import {
  differsFromValidator,
  emailFormatValidator,
  isPasswordPolicySatisfied,
  isValidEmail,
  passwordPolicyValidator,
  passwordsMatchValidator,
} from './auth-validators';

describe('auth-validators', () => {
  it('mật khẩu: 8–128 ký tự, có chữ và số (BR-28)', () => {
    expect(isPasswordPolicySatisfied('Passw0rd1')).toBeTrue();
    expect(isPasswordPolicySatisfied('mậtkhẩu123')).toBeTrue();
    expect(isPasswordPolicySatisfied('short1a')).toBeFalse();
    expect(isPasswordPolicySatisfied('chỉ-có-chữ-cái')).toBeFalse();
    expect(isPasswordPolicySatisfied('12345678')).toBeFalse();
    expect(isPasswordPolicySatisfied('a1'.repeat(64))).toBeTrue();
    expect(isPasswordPolicySatisfied('a1'.repeat(64) + 'b')).toBeFalse();
  });

  it('email: khớp EmailAddress ở backend (BR-27)', () => {
    expect(isValidEmail('an@example.com')).toBeTrue();
    expect(isValidEmail('  AN@Example.com ')).toBeTrue();
    expect(isValidEmail('an@example')).toBeFalse();
    expect(isValidEmail('an@@example.com')).toBeFalse();
    expect(isValidEmail('an example@x.com')).toBeFalse();
    expect(isValidEmail('an@example.')).toBeFalse();
    expect(isValidEmail('@example.com')).toBeFalse();
    expect(isValidEmail('')).toBeFalse();
  });

  it('validator bỏ qua giá trị rỗng để required báo lỗi riêng', () => {
    expect(passwordPolicyValidator(new FormControl(''))).toBeNull();
    expect(emailFormatValidator(new FormControl(''))).toBeNull();
    expect(passwordPolicyValidator(new FormControl('abc'))).toEqual({ passwordPolicy: true });
    expect(emailFormatValidator(new FormControl('abc'))).toEqual({ email: true });
  });

  it('nhập lại mật khẩu: lỗi gắn vào ô xác nhận và tự hết khi khớp', () => {
    const group = new FormGroup(
      { password: new FormControl('Passw0rd1'), confirm: new FormControl('khac') },
      { validators: passwordsMatchValidator('password', 'confirm') },
    );
    expect(group.get('confirm')?.errors).toEqual({ mismatch: true });

    group.get('confirm')?.setValue('Passw0rd1');
    expect(group.get('confirm')?.errors).toBeNull();

    group.get('password')?.setValue('Passw0rd2');
    expect(group.get('confirm')?.errors).toEqual({ mismatch: true });
  });

  it('mật khẩu mới phải khác mật khẩu hiện tại', () => {
    const group = new FormGroup(
      { current: new FormControl('Passw0rd1'), next: new FormControl('Passw0rd1') },
      { validators: differsFromValidator('current', 'next') },
    );
    expect(group.get('next')?.errors).toEqual({ unchanged: true });

    group.get('next')?.setValue('Passw0rd2');
    expect(group.get('next')?.errors).toBeNull();
  });
});
