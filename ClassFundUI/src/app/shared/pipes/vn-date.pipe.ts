import { Pipe, PipeTransform } from '@angular/core';

const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})$/;
const vietnamDate = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Ho_Chi_Minh',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

/** Hiện ngày dd/MM/yyyy. Ngày nghiệp vụ giữ nguyên; thời điểm UTC đổi sang giờ Việt Nam. */
@Pipe({ name: 'vnDate' })
export class VnDatePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }
    const dateOnly = DATE_ONLY.exec(value);
    if (dateOnly) {
      return `${dateOnly[3]}/${dateOnly[2]}/${dateOnly[1]}`;
    }
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? '' : vietnamDate.format(parsed);
  }
}
