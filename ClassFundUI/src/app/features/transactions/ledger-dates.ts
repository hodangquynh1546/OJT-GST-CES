const vietnamParts = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Ho_Chi_Minh',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

/** Ngày hiện tại theo giờ Việt Nam `yyyy-MM-dd` (BR-07: không ghi ngày tương lai). */
export function vietnamToday(now: Date = new Date()): string {
  return vietnamParts.format(now);
}

/** `yyyy-MM` → khoảng ngày của tháng đó (dùng cho bộ lọc Tháng → `from` / `to`). */
export function monthRange(month: string): { from: string; to: string } {
  const [year, value] = month.split('-').map(Number);
  const last = new Date(Date.UTC(year, value, 0)).getUTCDate();
  return { from: `${month}-01`, to: `${month}-${String(last).padStart(2, '0')}` };
}

export interface MonthOption {
  value: string;
  label: string;
}

/** "Tất cả các tháng" + các tháng từ tháng tạo quỹ tới tháng hiện tại, mới nhất trước (Canvas `Ledger`). */
export function monthOptions(fundCreatedAt: string | null, today: string): MonthOption[] {
  const current = today.slice(0, 7);
  const first = fundCreatedAt ? vietnamToday(new Date(fundCreatedAt)).slice(0, 7) : current;
  const options: MonthOption[] = [];
  let [year, month] = current.split('-').map(Number);
  for (let guard = 0; guard < 120; guard++) {
    const value = `${year}-${String(month).padStart(2, '0')}`;
    options.push({ value, label: `Tháng ${value.slice(5)}/${year}` });
    if (value <= first) {
      break;
    }
    month -= 1;
    if (month === 0) {
      month = 12;
      year -= 1;
    }
  }
  return [{ value: '', label: 'Tất cả các tháng' }, ...options];
}

/** `yyyy-MM-dd` → `dd/MM/yyyy`. */
export function dmy(date: string | null | undefined): string {
  return date ? date.split('-').reverse().join('/') : '';
}

/** Ngày nghiệp vụ ↔ `Date` cục bộ cho `kendo-datepicker` (không lệch múi giờ). */
export function toDate(date: string): Date {
  const [year, month, day] = date.split('-').map(Number);
  return new Date(year, month - 1, day);
}

export function fromDate(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}
