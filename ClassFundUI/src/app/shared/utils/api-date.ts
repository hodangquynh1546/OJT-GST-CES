/**
 * Ngày nghiệp vụ trao đổi với API dạng `yyyy-MM-dd` (không giờ, không múi giờ) – docs/api/api-conventions.md.
 * Thời điểm (createdAt, updatedAt…) là ISO 8601 UTC, hiển thị bằng pipe `vnDate`.
 */
const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})$/;

const vietnamParts = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Ho_Chi_Minh',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

/** Date của DatePicker (giờ máy) → `yyyy-MM-dd` theo đúng ngày người dùng chọn. */
export function toApiDate(value: Date): string {
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${value.getFullYear()}-${month}-${day}`;
}

/** `yyyy-MM-dd` → Date 00:00 giờ máy cho DatePicker; chuỗi sai trả null. */
export function fromApiDate(value: string | null | undefined): Date | null {
  const match = value ? DATE_ONLY.exec(value) : null;
  return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null;
}

/** Hôm nay theo giờ Việt Nam, dạng `yyyy-MM-dd` (mặc định ô ngày, giới hạn `max` – không cho ngày tương lai). */
export function todayApiDate(now: Date = new Date()): string {
  return vietnamParts.format(now);
}
