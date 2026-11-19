import { daysUntil, dueText, formatDateTime, percentOf, todayInVietnam } from './charges.utils';

describe('charges.utils', () => {
  it('percentOf làm tròn và chịu được tổng bằng 0', () => {
    expect(percentOf(26, 32)).toBe(81);
    expect(percentOf(0, 0)).toBe(0);
    expect(percentOf(40, 32)).toBe(100);
  });

  it('daysUntil tính số ngày theo ngày nghiệp vụ', () => {
    expect(daysUntil('2026-11-30', '2026-11-27')).toBe(3);
    expect(daysUntil('2026-11-27', '2026-11-27')).toBe(0);
    expect(daysUntil('2026-11-25', '2026-11-27')).toBe(-2);
  });

  it('dueText mô tả hạn đóng', () => {
    expect(dueText(3)).toBe('còn 3 ngày');
    expect(dueText(0)).toBe('hết hạn hôm nay');
    expect(dueText(-2)).toBe('quá hạn 2 ngày');
  });

  it('todayInVietnam dùng giờ Việt Nam', () => {
    expect(todayInVietnam(new Date('2026-11-04T18:30:00Z'))).toBe('2026-11-05');
  });

  it('formatDateTime hiện ngày giờ Việt Nam', () => {
    expect(formatDateTime('2026-11-16T13:14:00Z')).toBe('16/11 20:14');
    expect(formatDateTime('abc')).toBe('');
  });
});
