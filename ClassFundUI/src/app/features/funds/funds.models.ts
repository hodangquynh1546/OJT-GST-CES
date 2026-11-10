import { FundRole } from '../../core/auth/auth.models';
import { Permission } from '../../core/auth/permissions';

/** `treasurerStatus` của `GET /funds` (backend-api.md mục Funds; BR-36). */
export type TreasurerStatus = 'Unassigned' | 'Assigned';

/**
 * Một mục của `GET /api/v1/funds` – khớp `FundSummaryResponse` (Contracts/snapshots/Funds.json).
 * `studentCount` chỉ có với GVCN, `linkedStudents` chỉ có với phụ huynh.
 */
export interface FundSummary {
  id: string;
  name: string;
  role: FundRole;
  permissions: Permission[];
  currentBalance: number;
  createdAt: string;
  treasurerStatus: TreasurerStatus;
  treasurerNames: string[];
  studentCount?: number;
  linkedStudents?: string[];
}

/** Body của `POST /funds` và `PUT /funds/{fundId}`. */
export interface FundNameRequest {
  name: string;
}

/** Response 201 của `POST /funds`. */
export interface FundCreated {
  id: string;
}

/** Tên quỹ 1–100 ký tự sau khi cắt khoảng trắng (module-plan.md M2, `Fund.Name` varchar(100)). */
export const FUND_NAME_MAX_LENGTH = 100;
