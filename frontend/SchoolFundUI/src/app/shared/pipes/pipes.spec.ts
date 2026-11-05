import { VnDatePipe } from './vn-date.pipe';
import { VndMoneyPipe } from './vnd-money.pipe';

describe('VndMoneyPipe', () => {
  const pipe = new VndMoneyPipe();

  it('định dạng số tiền với dấu chấm ngăn cách', () => {
    expect(pipe.transform(3720000)).toBe('3.720.000 đ');
  });

  it('dùng dấu trừ cho số âm và dấu cộng khi signed', () => {
    expect(pipe.transform(-250000)).toBe('−250.000 đ');
    expect(pipe.transform(65000, true)).toBe('+65.000 đ');
    expect(pipe.transform(0, true)).toBe('0 đ');
  });

  it('trả chuỗi rỗng khi không có giá trị', () => {
    expect(pipe.transform(null)).toBe('');
  });
});

describe('VnDatePipe', () => {
  const pipe = new VnDatePipe();

  it('giữ nguyên ngày nghiệp vụ', () => {
    expect(pipe.transform('2026-11-05')).toBe('05/11/2026');
  });

  it('đổi thời điểm UTC sang ngày Việt Nam', () => {
    expect(pipe.transform('2026-11-04T18:30:00Z')).toBe('05/11/2026');
  });

  it('bỏ qua giá trị không hợp lệ', () => {
    expect(pipe.transform('abc')).toBe('');
  });
});
