import { Permission } from './permissions';

export type AccountKind = 'Teacher' | 'Student' | 'Parent';
export type FundRole = 'Teacher' | 'Treasurer' | 'Student' | 'Parent';

export interface FundMembership {
  fundId: string;
  fundName: string;
  role: FundRole;
  permissions: Permission[];
  linkedStudents?: string[];
}

/** Response của GET /auth/me. */
export interface CurrentUser {
  id: string;
  kind: AccountKind;
  fullName: string;
  email?: string | null;
  username?: string | null;
  mustChangePassword: boolean;
  accountPermissions: Permission[];
  memberships: FundMembership[];
}
