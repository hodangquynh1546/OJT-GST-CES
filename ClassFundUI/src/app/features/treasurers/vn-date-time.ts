const vietnamDateTime = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Ho_Chi_Minh',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

/** Thời điểm phân công / thu hồi theo giờ Việt Nam: `dd/MM/yyyy HH:mm` (Canvas G-03). */
export function vnDateTime(value: string | null | undefined): string {
  if (!value) {
    return '';
  }
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? '' : vietnamDateTime.format(parsed).replace(',', '');
}
