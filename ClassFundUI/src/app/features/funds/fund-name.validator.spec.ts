import { FormControl } from '@angular/forms';

import { fundNameValidator } from './fund-name.validator';

describe('fundNameValidator', () => {
  const errors = (value: string | null) => fundNameValidator(new FormControl(value));

  it('trống / chỉ khoảng trắng / null → lỗi', () => {
    expect(errors('')).toEqual({ fundName: true });
    expect(errors('   ')).toEqual({ fundName: true });
    expect(errors(null)).toEqual({ fundName: true });
  });

  it('tính độ dài sau khi cắt khoảng trắng: 100 ký tự hợp lệ, 101 lỗi', () => {
    expect(errors(`  ${'A'.repeat(100)}  `)).toBeNull();
    expect(errors('A'.repeat(101))).toEqual({ fundName: true });
  });

  it('tên bình thường hợp lệ', () => {
    expect(errors('Quỹ lớp 5A')).toBeNull();
  });
});
