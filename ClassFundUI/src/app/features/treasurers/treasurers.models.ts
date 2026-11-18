import { MemberRole } from '../../core/auth/auth.models';

/** Một người trong đội thủ quỹ (khớp `TreasurerDto` ở backend). `memberId` là mã FundMember. */
export interface Treasurer {
  memberId: string;
  userId: string;
  fullName: string;
  email: string;
  role: Extract<MemberRole, 'Owner' | 'Treasurer'>;
  joinedAt: string;
  /** Lúc được phân quyền gần nhất; null với thủ quỹ chính hoặc khi chưa có nhật ký. */
  grantedAt: string | null;
}

/** Học sinh có thể được phân quyền (từ danh sách thành viên). */
export interface Candidate {
  memberId: string;
  label: string;
  fullName: string;
}
