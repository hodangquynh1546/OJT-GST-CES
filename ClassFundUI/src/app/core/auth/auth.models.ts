import { Permission } from './permissions';

/** Owner = thủ quỹ đầu tiên (toàn quyền); Treasurer = thủ quỹ được phân quyền; Student = học sinh. */
export type MemberRole = 'Owner' | 'Treasurer' | 'Student';

export interface FundMembership {
  fundId: string;
  fundName: string;
  role: MemberRole;
  permissions: Permission[];
}

/** Response của GET /auth/me. */
export interface CurrentUser {
  id: string;
  email: string;
  fullName: string;
  mustChangePassword: boolean;
  /** Quyền cấp tài khoản (ví dụ Fund.Create – chỉ tài khoản tự đăng ký). */
  accountPermissions: Permission[];
  memberships: FundMembership[];
}
