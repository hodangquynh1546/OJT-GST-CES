import { fundShortName } from './fund-short-name';

describe('fundShortName', () => {
  it('lấy mã lớp ở từ cuối nếu có chữ số; bỏ chú thích trong ngoặc', () => {
    expect(fundShortName('Lớp 10A1')).toBe('10A1');
    expect(fundShortName('Lớp 10A1 (demo)')).toBe('10A1');
    expect(fundShortName('Lớp 9A1')).toBe('9A1');
  });

  it('không có chữ số thì lấy chữ cái đầu mỗi từ, tối đa 3', () => {
    expect(fundShortName('CLB Tiếng Anh')).toBe('CTA');
    expect(fundShortName('Quỹ')).toBe('Q');
    expect(fundShortName('  ')).toBe('');
  });
});
