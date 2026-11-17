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
  it('outside a fund shows only the fund list', () => {
    expect(buildNavItems({ fundId: null, role: null, permissions: [] }).map((i) => i.id)).toEqual(['funds']);
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
