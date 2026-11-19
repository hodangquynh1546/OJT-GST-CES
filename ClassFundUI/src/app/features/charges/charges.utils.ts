import { AssignmentStatus } from '../../shared/models/labels';

/** Phần trăm (0–100, làm tròn) của `part` trên `total`; 0 khi `total` bằng 0. */
export function percentOf(part: number, total: number): number {
  return total > 0 ? Math.min(100, Math.round((part / total) * 100)) : 0;
}

/** Số ngày từ `today` tới ngày hạn (dương = còn, âm = quá hạn). Cả hai dạng `yyyy-MM-dd`. */
export function daysUntil(dueDate: string, today: string): number {
  const toUtc = (value: string): number => {
    const [year, month, day] = value.split('-').map(Number);
    return Date.UTC(year, month - 1, day);
  };
  return Math.round((toUtc(dueDate) - toUtc(today)) / 86_400_000);
}

/** Hôm nay theo giờ Việt Nam, dạng `yyyy-MM-dd`. */
export function todayInVietnam(now: Date = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(now);
}

export function hasPaymentToHandle(status: AssignmentStatus): boolean {
  return status === 'AwaitingConfirmation';
}

/** Mô tả hạn: "còn 3 ngày", "hôm nay", "quá hạn 2 ngày". */
export function dueText(days: number): string {
  if (days === 0) {
    return 'hết hạn hôm nay';
  }
  return days > 0 ? `còn ${days} ngày` : `quá hạn ${-days} ngày`;
}

const dateTime = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Ho_Chi_Minh',
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

/** `16/11 20:14` theo giờ Việt Nam. */
export function formatDateTime(value: string): string {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? '' : dateTime.format(parsed).replace(',', '');
}
