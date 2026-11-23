import { MemberRole } from '../../core/auth/auth.models';

export type FundStatus = 'Active' | 'Archived';

export const FUND_STATUS_LABEL: Record<FundStatus, string> = {
  Active: 'Đang hoạt động',
  Archived: 'Đã tổng kết',
};

/** Thẻ quỹ ở F-01 – khớp `FundSummaryDto`. */
export interface FundSummary {
  id: string;
  name: string;
  schoolYear: string;
  status: FundStatus;
  myRole: MemberRole;
  memberCount: number;
  /** Số dư quỹ chung (không gồm tiền sự kiện). */
  generalBalance: number;
  /** Chỉ có với người xác nhận được thanh toán (thủ quỹ). */
  pendingPaymentCount: number | null;
  /** Số tiền mình còn phải đóng ở các khoản thu đang mở (chung và sự kiện) và số khoản. */
  myDueAmount: number;
  myDueCount: number;
  /** Số dư cá nhân của mình ở quỹ này. */
  myBalance: number;
}

/** Thông tin công khai của quỹ – khớp `FundDto`. */
export interface FundDetail {
  id: string;
  name: string;
  schoolYear: string;
  status: FundStatus;
  createdAt: string;
  ownerName: string;
  memberCount: number;
  myRole: MemberRole;
}

export interface FundBody {
  name: string;
  schoolYear: string;
}

/** Năm học dạng `2026-2027` (khớp `SchoolYearFormat` ở backend). */
export function isValidSchoolYear(value: string | null | undefined): boolean {
  const match = /^(\d{4})\s*[-–—]\s*(\d{4})$/.exec((value ?? '').trim());
  if (!match) {
    return false;
  }
  const start = Number(match[1]);
  return Number(match[2]) === start + 1 && start >= 2000 && start <= 2100;
}

/** Hiển thị năm học với gạch nối dài như bản thiết kế: `2026–2027`. */
export function displaySchoolYear(value: string): string {
  return value.replace('-', '–');
}

/** Năm học hiện tại theo lịch Việt Nam: từ tháng 8 tính sang năm học mới. */
export function currentSchoolYear(today = new Date()): string {
  const start = today.getMonth() >= 7 ? today.getFullYear() : today.getFullYear() - 1;
  return `${start}-${start + 1}`;
}

/** Danh sách lựa chọn năm học: năm trước, năm nay, năm sau. */
export function schoolYearOptions(today = new Date()): string[] {
  const start = Number(currentSchoolYear(today).slice(0, 4));
  return [start - 1, start, start + 1].map((year) => `${year}-${year + 1}`);
}

