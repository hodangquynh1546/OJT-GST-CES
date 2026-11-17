import { HttpParams } from '@angular/common/http';

export type QueryValue = string | number | boolean | null | undefined;

/** Bộ lọc → query string; bỏ giá trị null / undefined / chuỗi rỗng để không gửi tham số thừa. */
export function toHttpParams(query: Readonly<Record<string, QueryValue>>): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== null && value !== undefined && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}
