import { Permission, Permissions } from '../auth/permissions';
import { buildNavItems } from './nav-items';

const STUDENT: Permission[] = [
  Permissions.FundView,
  Permissions.EventParticipate,
  Permissions.PaymentSubmit,
  Permissions.BalanceViewOwn,
  Permissions.BalanceWithdraw,
];
const TREASURER: Permission[] = [
  ...STUDENT,
  Permissions.FundManage,
  Permissions.MemberManage,
  Permissions.CategoryManage,
  Permissions.EventManage,
  Permissions.ChargeManage,
  Permissions.LedgerManage,
  Permissions.PaymentConfirm,
  Permissions.SettlementExecute,
  Permissions.BalanceHandle,
  Permissions.ReportView,
  Permissions.AuditView,
];

const ids = (permissions: Permission[]) =>
  buildNavItems({ fundId: 'f1', role: null, permissions }).map((item) => item.id);

describe('buildNavItems', () => {
  it('outside a fund shows no menu items (switching funds is the fund card, not a menu item)', () => {
    expect(buildNavItems({ fundId: null, role: null, permissions: [] })).toEqual([]);
  });

  it('groups items as Quỹ chung · Cá nhân · Quản lý, students have no management group', () => {
    const groupsOf = (permissions: Permission[]) => [
      ...new Set(buildNavItems({ fundId: 'f1', role: null, permissions }).map((item) => item.group)),
    ];
    expect(groupsOf(STUDENT)).toEqual(['Quỹ chung', 'Cá nhân']);
    expect(groupsOf(TREASURER)).toEqual(['Quỹ chung', 'Cá nhân', 'Quản lý']);
  });

  it('calls the ledger "Chi tiêu" for people who cannot manage it', () => {
    const labels = buildNavItems({ fundId: 'f1', role: null, permissions: STUDENT }).map((item) => item.label);
    expect(labels).toContain('Chi tiêu');
    expect(labels).not.toContain('Thu chi chung');
  });

  it('uses the labels of the design canvas', () => {
    const labels = buildNavItems({ fundId: 'f1', role: null, permissions: TREASURER }).map((item) => item.label);
    expect(labels).toEqual([
      'Tổng quan',
      'Thu chi chung',
      'Khoản thu chung',
      'Sự kiện',
      'Khoản cần đóng',
      'Số dư cá nhân',
      'Thông báo',
      'Xác nhận thanh toán',
      'Yêu cầu rút tiền',
      'Báo cáo',
      'Học sinh',
      'Danh mục',
      'Nhật ký hoạt động',
      'Thông tin quỹ',
    ]);
  });

  it('student sees general fund, events and personal items but no management', () => {
    const list = ids(STUDENT);
    expect(list).toContain('overview');
    expect(list).toContain('events');
    expect(list).toContain('my-payments');
    expect(list).toContain('balance');
    expect(list).not.toContain('payments');
    expect(list).not.toContain('audit');
    expect(list).not.toContain('treasurers');
  });

  it('granted treasurer manages the fund but not other treasurers', () => {
    const list = ids(TREASURER);
    expect(list).toContain('payments');
    expect(list).toContain('audit');
    expect(list).toContain('my-payments');
    expect(list).not.toContain('treasurers');
  });

  it('owner also sees treasurer management', () => {
    expect(ids([...TREASURER, Permissions.TreasurerManage])).toContain('treasurers');
  });

  it('links are scoped to the current fund', () => {
    const ledger = buildNavItems({ fundId: 'f1', role: null, permissions: STUDENT }).find((i) => i.id === 'ledger');
    expect(ledger?.link).toBe('/funds/f1/ledger');
  });
});
