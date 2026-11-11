import { FormControl } from '@angular/forms';

import {
  optionalEmailValidator,
  optionalUsernameValidator,
  requiredEmailValidator,
  studentNameValidator,
} from './students.validators';

const errors = (validator: (control: FormControl<string>) => unknown, value: string) =>
  validator(new FormControl(value, { nonNullable: true }));

describe('students validators', () => {
  it('requires a trimmed student name of at most 100 characters', () => {
    expect(errors(studentNameValidator, '  Phạm Đức Huy  ')).toBeNull();
    expect(errors(studentNameValidator, '   ')).toEqual({ studentName: true });
    expect(errors(studentNameValidator, 'a'.repeat(101))).toEqual({ studentName: true });
    expect(errors(studentNameValidator, 'a'.repeat(100))).toBeNull();
  });

  it('accepts an empty optional email but rejects malformed ones', () => {
    expect(errors(optionalEmailValidator, '')).toBeNull();
    expect(errors(optionalEmailValidator, ' ha.le@example.com ')).toBeNull();
    expect(errors(optionalEmailValidator, 'khong-phai-email')).toEqual({ email: true });
  });

  it('distinguishes a missing required email from a malformed one', () => {
    expect(errors(requiredEmailValidator, ' ')).toEqual({ required: true });
    expect(errors(requiredEmailValidator, 'a@b')).toEqual({ email: true });
    expect(errors(requiredEmailValidator, 'a@b.vn')).toBeNull();
  });

  it('checks the username format after lower-casing, empty means "let the system suggest"', () => {
    expect(errors(optionalUsernameValidator, '')).toBeNull();
    expect(errors(optionalUsernameValidator, 'Huy.Pham.482')).toBeNull();
    expect(errors(optionalUsernameValidator, 'ab')).toEqual({ username: true });
    expect(errors(optionalUsernameValidator, 'bảo.lê')).toEqual({ username: true });
    expect(errors(optionalUsernameValidator, 'a'.repeat(61))).toEqual({ username: true });
  });
});
