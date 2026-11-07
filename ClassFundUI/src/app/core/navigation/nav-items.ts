import { AccountKind, FundRole } from '../auth/auth.models';
import { Permission, Permissions } from '../auth/permissions';

export interface NavItem {
  id: string;
  label: string;
  link: string;
  /** Đường path của icon SVG 24x24 (lấy từ AppNav trên Canvas). */
  icon: string;
  exact: boolean;
}

const ICONS = {
  funds: 'M3 7h18v13H3zM3 7l3-3h12l3 3M8 12h8',
  overview: 'M3 11 12 4l9 7v9h-6v-6H9v6H3z',
  ledger: 'M5 4h14v16H5zM9 9h6M9 13h6M9 17h3',
  reports: 'M4 20V10M10 20V4M16 20v-7M22 20H2',
  people:
    'M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM2 21a7 7 0 0 1 14 0M17 11a3 3 0 1 0 0-6M22 21a6 6 0 0 0-4-5.6',
  treasurers: 'M12 2 4 6v6c0 5 3.5 8.5 8 10 4.5-1.5 8-5 8-10V6zM9 12l2 2 4-4',
  categories: 'M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z',
  settings: 'M4 7h16M4 12h16M4 17h16M8 5v4M16 10v4M10 15v4',
};

export interface NavContext {
  kind: AccountKind;
  fundId: string | null;
  role: FundRole | null;
  permissions: readonly Permission[];
}

/** Menu sinh từ permission của quỹ đang mở (sitemap mục 2). */
export function buildNavItems({ kind, fundId, role, permissions }: NavContext): NavItem[] {
  const items: NavItem[] = [];

  if (kind !== 'Student') {
    items.push({
      id: 'funds',
      label: kind === 'Parent' ? 'Quỹ của con' : 'Quỹ của tôi',
      link: '/funds',
      icon: ICONS.funds,
      exact: true,
    });
  }

  if (!fundId) {
    return items;
  }

  const base = `/funds/${fundId}`;
  const can = (permission: Permission) => permissions.includes(permission);
  const add = (
    permission: Permission,
    id: string,
    label: string,
    path: string,
    icon: string,
  ) => {
    if (can(permission)) {
      items.push({ id, label, link: base + path, icon, exact: path === '' });
    }
  };

  const ledgerLabel = can(Permissions.LedgerViewPublic) ? 'Thu chi công khai' : 'Thu chi';
  add(Permissions.FundView, 'overview', 'Tổng quan', '', ICONS.overview);
  add(Permissions.FundView, 'ledger', ledgerLabel, '/transactions', ICONS.ledger);
  add(Permissions.FundView, 'reports', 'Báo cáo', '/reports', ICONS.reports);
  add(Permissions.StudentManage, 'students', 'Học sinh & phụ huynh', '/students', ICONS.people);
  add(Permissions.TreasurerAssign, 'treasurers', 'Phân công thủ quỹ', '/treasurers', ICONS.treasurers);
  add(Permissions.FundManage, 'settings', 'Thông tin quỹ', '/settings', ICONS.settings);
  add(Permissions.CategoryManage, 'categories', 'Danh mục', '/categories', ICONS.categories);
  if (role === 'Treasurer') {
    add(Permissions.MemberView, 'members', 'Thành viên', '/members', ICONS.people);
  }

  return items;
}
