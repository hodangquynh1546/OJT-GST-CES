import { MemberRole } from '../auth/auth.models';

export interface RoleDisplay {
  badge: string;
  /** Dòng phụ dưới tên người dùng ở AppHeader. */
  subtitle: string;
  color: string;
  tint: string;
  footnote: string;
}

export const ROLE_DISPLAY: Record<MemberRole, RoleDisplay> = {
  Owner: {
    subtitle: 'Thủ quỹ chính',
    badge: 'Thủ quỹ chính',
    color: 'var(--cf-role-owner)',
    tint: 'var(--cf-role-owner-tint)',
    footnote: 'Thủ quỹ chính · toàn quyền, gồm phân quyền thủ quỹ khác.',
  },
  Treasurer: {
    subtitle: 'Thủ quỹ',
    badge: 'Thủ quỹ',
    color: 'var(--cf-role-treasurer)',
    tint: 'var(--cf-role-treasurer-tint)',
    footnote: 'Thủ quỹ · quản lý quỹ; không thay đổi thủ quỹ khác.',
  },
  Student: {
    subtitle: 'Học sinh',
    badge: 'Học sinh',
    color: 'var(--cf-role-student)',
    tint: 'var(--cf-role-student-tint)',
    footnote: 'Học sinh · xem thông tin công khai và thông tin cá nhân của mình.',
  },
};
