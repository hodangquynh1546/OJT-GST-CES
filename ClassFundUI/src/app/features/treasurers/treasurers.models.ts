/** Hợp đồng M4 (docs/api/backend-api.md – Treasurers; snapshot Contracts/snapshots/Treasurers.json). */
export interface TreasurerAssignment {
  id: string;
  studentId: string;
  studentName: string;
  /** Chưa thu hồi. */
  isActive: boolean;
  assignedAt: string;
  assignedByName: string;
  revokedAt: string | null;
  revokedByName: string | null;
}

export interface TreasurerAssigned {
  id: string;
}

/** `accountStatus` của học sinh (Domain `StudentAccountStatus`, hợp đồng M3). */
export type StudentAccountStatus = 'None' | 'PendingActivation' | 'Active' | 'Disabled';

/**
 * Phần M4 dùng trong `GET /funds/{id}/students` (M3 – backend-api.md mục Students): đủ để chọn thủ quỹ.
 * Khai báo riêng ở đây để không phụ thuộc file của module Học sinh.
 */
export interface StudentOption {
  id: string;
  fullName: string;
  username: string | null;
  accountStatus: StudentAccountStatus;
  isTreasurer: boolean;
}
