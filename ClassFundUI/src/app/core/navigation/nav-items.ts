import { MemberRole } from '../auth/auth.models';
import { Permission, Permissions } from '../auth/permissions';

export interface NavItem {
  id: string;
  label: string;
  link: string;
  /** Đường path của icon SVG 24x24. */
  icon: string;
  exact: boolean;
}

const ICONS = {
  funds: 'M3 7h18v13H3zM3 7l3-3h12l3 3M8 12h8',
  overview: 'M3 11 12 4l9 7v9h-6v-6H9v6H3z',
  ledger: 'M5 4h14v16H5zM9 9h6M9 13h6M9 17h3',
  charges: 'M12 2v20M17 6H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6',
  events: 'M4 5h16v15H4zM4 10h16M9 3v4M15 3v4',
  payments: 'M3 6h18v12H3zM3 10h18M7 15h3',
  balance: 'M4 7h16v12H4zM16 13h2M4 7l3-3h10',
  reports: 'M4 20V10M10 20V4M16 20v-7M22 20H2',
  people: 'M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM2 21a7 7 0 0 1 14 0M17 11a3 3 0 1 0 0-6M22 21a6 6 0 0 0-4-5.6',
  treasurers: 'M12 2 4 6v6c0 5 3.5 8.5 8 10 4.5-1.5 8-5 8-10V6zM9 12l2 2 4-4',
  categories: 'M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7h-7zM13 13h7v7h-7z',
  audit: 'M12 8v5l3 2M3 12a9 9 0 1 0 3-6.7M3 4v5h5',
  bell: 'M6 8a6 6 0 1 1 12 0c0 7 3 8 3 8H3s3-1 3-8zM10 20a2 2 0 0 0 4 0',
  settings: 'M4 7h16M4 12h16M4 17h16M8 5v4M16 10v4M10 15v4',
};

export interface NavContext {
  fundId: string | null;
  role: MemberRole | null;
  permissions: readonly Permission[];
}

/**
 * Menu sinh từ permission của quỹ đang mở. Quỹ chung và Sự kiện là hai mục tách biệt (yêu cầu mục 2).
 * Thứ tự: dùng chung → việc của học sinh → quản lý của thủ quỹ.
 */
export function buildNavItems({ fundId, permissions }: NavContext): NavItem[] {
  const items: NavItem[] = [
    { id: 'funds', label: 'Quỹ của tôi', link: '/funds', icon: ICONS.funds, exact: true },
  ];

  if (!fundId) {
    return items;
  }

  const base = `/funds/${fundId}`;
  const can = (permission: Permission) => permissions.includes(permission);
  const add = (permission: Permission, id: string, label: string, path: string, icon: string) => {
    if (can(permission)) {
      items.push({ id, label, link: base + path, icon, exact: path === '' });
    }
  };

  add(Permissions.FundView, 'overview', 'Tổng quan quỹ chung', '', ICONS.overview);
  add(Permissions.FundView, 'ledger', 'Thu chi chung', '/ledger', ICONS.ledger);
  add(Permissions.FundView, 'charges', 'Khoản thu chung', '/charges', ICONS.charges);
  add(Permissions.EventParticipate, 'events', 'Sự kiện', '/events', ICONS.events);
  add(Permissions.PaymentSubmit, 'my-payments', 'Khoản cần đóng của tôi', '/my-payments', ICONS.payments);
  add(Permissions.BalanceViewOwn, 'balance', 'Số dư cá nhân', '/balance', ICONS.balance);
  add(Permissions.FundView, 'notifications', 'Thông báo', '/notifications', ICONS.bell);
  add(Permissions.PaymentConfirm, 'payments', 'Xác nhận thanh toán', '/payments', ICONS.payments);
  add(Permissions.BalanceHandle, 'withdrawals', 'Yêu cầu rút tiền', '/withdrawals', ICONS.balance);
  add(Permissions.ReportView, 'reports', 'Báo cáo', '/reports', ICONS.reports);
  add(Permissions.MemberManage, 'members', 'Học sinh', '/members', ICONS.people);
  add(Permissions.TreasurerManage, 'treasurers', 'Thủ quỹ', '/treasurers', ICONS.treasurers);
  add(Permissions.CategoryManage, 'categories', 'Danh mục', '/categories', ICONS.categories);
  add(Permissions.AuditView, 'audit', 'Nhật ký hoạt động', '/audit', ICONS.audit);
  add(Permissions.FundManage, 'settings', 'Thông tin quỹ', '/settings', ICONS.settings);

  return items;
}
