import { fromApiDate, toApiDate, todayApiDate } from './api-date';

describe('api-date', () => {
  it('đổi Date ↔ yyyy-MM-dd không lệch ngày', () => {
    expect(toApiDate(new Date(2026, 0, 5))).toBe('2026-01-05');
    const date = fromApiDate('2026-11-09')!;
    expect([date.getFullYear(), date.getMonth(), date.getDate()]).toEqual([2026, 10, 9]);
    expect(toApiDate(date)).toBe('2026-11-09');
  });

  it('chuỗi sai trả null', () => {
    expect(fromApiDate('09/11/2026')).toBeNull();
    expect(fromApiDate(null)).toBeNull();
  });

  it('hôm nay theo giờ Việt Nam', () => {
    // 2026-11-08 18:30 UTC = 2026-11-09 01:30 giờ Việt Nam.
    expect(todayApiDate(new Date(Date.UTC(2026, 10, 8, 18, 30)))).toBe('2026-11-09');
  });
});
