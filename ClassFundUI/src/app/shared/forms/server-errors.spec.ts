import { FormControl, FormGroup } from '@angular/forms';

import { ApiError } from '../models/api-error';
import { applyServerErrors, serverError } from './server-errors';

describe('applyServerErrors', () => {
  const newForm = () => new FormGroup({ name: new FormControl(''), amount: new FormControl(0) });

  it('gắn lỗi 400 vào từng ô, không còn thông báo chung', () => {
    const form = newForm();
    const error = new ApiError(400, 'validation_failed', 'Dữ liệu chưa hợp lệ.', { name: ['Bắt buộc.'] });
    expect(applyServerErrors(form, error)).toBeNull();
    expect(serverError(form.get('name'))).toBe('Bắt buộc.');
    expect(form.get('name')!.touched).toBeTrue();
  });

  it('lỗi nghiệp vụ gắn vào ô theo bảng code → field', () => {
    const form = newForm();
    const error = new ApiError(422, 'category.name_taken', 'Tên đã tồn tại.');
    expect(applyServerErrors(form, error, { 'category.name_taken': 'name' })).toBeNull();
    expect(serverError(form.get('name'))).toBe('Tên đã tồn tại.');
  });

  it('lỗi không gắn được vào ô trả về để hiện ở hộp tóm tắt', () => {
    const form = newForm();
    expect(applyServerErrors(form, new ApiError(422, 'treasurer.last', 'Cần thủ quỹ khác.'))).toBe('Cần thủ quỹ khác.');
    expect(applyServerErrors(form, new ApiError(400, 'validation_failed', 'Sai.', { other: ['x'] }))).toBe('Sai.');
    expect(applyServerErrors(form, new Error('x'))).toBe('Đã có lỗi xảy ra. Vui lòng thử lại.');
  });
});
