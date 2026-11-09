import { messageForCode, registerErrorMessages } from './error-messages';

describe('error-messages', () => {
  it('mã chung có sẵn, mã lạ dùng title từ server rồi mới tới thông báo chung', () => {
    expect(messageForCode('not_found')).toBe('Không tìm thấy dữ liệu.');
    expect(messageForCode('x.unknown', 'Từ server')).toBe('Từ server');
    expect(messageForCode('x.unknown')).toBe('Đã có lỗi xảy ra. Vui lòng thử lại.');
  });

  it('module đăng ký mã riêng, không ghi đè mã đã có', () => {
    registerErrorMessages({ 'spec.module_rule': 'Quy tắc module', not_found: 'Ghi đè' });
    expect(messageForCode('spec.module_rule')).toBe('Quy tắc module');
    expect(messageForCode('not_found')).toBe('Không tìm thấy dữ liệu.');
  });
});
