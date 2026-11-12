import { CategoryType } from '../categories/categories.models';

/** Hợp đồng M6 (docs/api/backend-api.md – Transactions; snapshot Contracts/snapshots/Transactions.json). */
export type TransactionType = CategoryType;
export type TransactionStatus = 'Active' | 'Cancelled' | 'All';

/** Bản công khai chỉ có các trường bắt buộc + `cancelledOn`; trường nội bộ chỉ có ở bản đầy đủ (GVCN, thủ quỹ). */
export interface Transaction {
  id: string;
  type: TransactionType;
  amount: number;
  /** Ngày nghiệp vụ `yyyy-MM-dd`. */
  occurredOn: string;
  categoryId: string;
  categoryName: string | null;
  description: string;
  isCancelled: boolean;
  cancelledOn?: string;
  note?: string;
  isPublic?: boolean;
  cancelReason?: string;
  cancelledAt?: string;
  createdAt?: string;
  updatedAt?: string;
  createdByName?: string;
  updatedByName?: string;
  cancelledByName?: string;
  /** Chỉ ở kết quả thêm. */
  replayed?: boolean;
  /** Chỉ ở kết quả ghi. */
  isNegativeBalance?: boolean;
}

export interface TransactionPage {
  items: Transaction[];
  total: number;
  page: number;
  pageSize: number;
}

export interface TransactionQuery {
  type?: TransactionType;
  categoryId?: string;
  from?: string;
  to?: string;
  search?: string;
  status: TransactionStatus;
  page: number;
  pageSize: number;
}

/** Giá trị gửi khi thêm / sửa (sửa không có `type`). */
export interface TransactionValues {
  amount: number;
  occurredOn: string;
  categoryId: string;
  description: string;
  note: string | null;
  isPublic: boolean;
}

export interface TransactionHistoryItem {
  occurredAt: string;
  action: 'Transaction.Create' | 'Transaction.Update' | 'Transaction.Cancel' | string;
  actorName: string | null;
  oldValues: Record<string, unknown> | null;
  newValues: Record<string, unknown> | null;
}

export const PAGE_SIZE = 20;
export const DESCRIPTION_MAX = 200;
export const NOTE_MAX = 1000;
export const CANCEL_REASON_MAX = 500;
