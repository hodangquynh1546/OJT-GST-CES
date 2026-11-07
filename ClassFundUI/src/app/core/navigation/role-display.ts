import { FundRole } from '../auth/auth.models';

export interface RoleDisplay {
  badge: string;
  color: string;
  tint: string;
  footnote: string;
}

/** Nhãn và màu vai trò theo AppHeader, AppNav trên Canvas. */
export const ROLE_DISPLAY: Record<FundRole, RoleDisplay> = {
  Teacher: {
    badge: 'GVCN',
    color: 'var(--cf-role-teacher)',
    tint: 'var(--cf-role-teacher-tint)',
    footnote: 'GVCN · xem toàn bộ, quản lý học sinh, phụ huynh, phân công thủ quỹ. Không ghi thu chi.',
  },
  Treasurer: {
    badge: 'Thủ quỹ',
    color: 'var(--cf-role-treasurer)',
    tint: 'var(--cf-role-treasurer-tint)',
    footnote: 'Thủ quỹ · ghi thu chi, quản lý danh mục; chỉ xem danh sách thành viên.',
  },
  Student: {
    badge: 'Học sinh · Công khai',
    color: 'var(--cf-role-student)',
    tint: 'var(--cf-role-student-tint)',
    footnote: 'Học sinh · chỉ xem dữ liệu công khai.',
  },
  Parent: {
    badge: 'Phụ huynh · Công khai',
    color: 'var(--cf-role-parent)',
    tint: 'var(--cf-role-parent-tint)',
    footnote: 'Phụ huynh · chỉ xem dữ liệu công khai.',
  },
};
