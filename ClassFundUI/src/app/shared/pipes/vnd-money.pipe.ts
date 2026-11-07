import { Pipe, PipeTransform } from '@angular/core';

const formatter = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });
const MINUS = '−';

/** Định dạng tiền `#.###.### đ`; signed = true luôn hiện dấu + / −. */
@Pipe({ name: 'vndMoney' })
export class VndMoneyPipe implements PipeTransform {
  transform(value: number | null | undefined, signed = false): string {
    if (value === null || value === undefined) {
      return '';
    }
    const sign = value < 0 ? MINUS : signed && value > 0 ? '+' : '';
    return `${sign}${formatter.format(Math.abs(value))} đ`;
  }
}
