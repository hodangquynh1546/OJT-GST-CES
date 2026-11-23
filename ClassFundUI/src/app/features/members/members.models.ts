import { MemberRole } from '../../core/auth/auth.models';

export type MemberStatus = 'Active' | 'Removed';

/** Một thành viên của quỹ (khớp `MemberDto` ở backend). `id` là mã FundMember, không phải mã người dùng. */
export interface Member {
  id: string;
  userId: string;
  fullName: string;
  email: string;
  role: MemberRole;
  status: MemberStatus;
  mustChangePassword: boolean;
  joinedAt: string;
  removedAt: string | null;
  /** AZ-07: được sửa họ tên / cấp lại mật khẩu (tài khoản do thủ quỹ cấp và chỉ ở quỹ này). */
  canManageAccount: boolean;
}

export interface MemberQuery {
  search?: string;
  role?: MemberRole;
  status?: MemberStatus;
  page: number;
  pageSize: number;
}

export type AddMemberOutcome = 'Created' | 'ExistingAdded' | 'Reactivated' | 'AlreadyExists' | 'Invalid';

export interface AddMemberResult {
  email: string;
  outcome: AddMemberOutcome;
  member: Member | null;
  /** Chỉ có khi `Created` và chỉ trả MỘT lần. */
  temporaryPassword: string | null;
  message: string | null;
}

export interface BulkAddResult {
  results: AddMemberResult[];
}

export interface TemporaryPasswordResult {
  temporaryPassword: string;
}

export interface RemovalPreview {
  unpaidCount: number;
  balance: number;
  canRemove: boolean;
  blockedReason: string | null;
}

export interface RemoveMemberResult {
  cancelledAssignments: number;
}

export const MAX_BULK_EMAILS = 50;

export const OUTCOME_LABELS: Record<AddMemberOutcome, string> = {
  Created: 'Đã tạo tài khoản',
  ExistingAdded: 'Đã thêm vào lớp',
  Reactivated: 'Đã kích hoạt lại',
  AlreadyExists: 'Đã có trong lớp',
  Invalid: 'Email không hợp lệ',
};
