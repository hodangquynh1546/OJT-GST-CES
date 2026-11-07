import { Permission, Permissions as P } from '../auth/permissions';
import { NavContext, buildNavItems } from './nav-items';

const FUND = 'f1';
const TEACHER: Permission[] = [
  P.FundView,
  P.LedgerViewFull,
  P.FundManage,
  P.StudentManage,
  P.TreasurerAssign,
  P.MemberView,
];
const TREASURER: Permission[] = [
  P.FundView,
  P.LedgerViewFull,
  P.MemberView,
  P.TransactionManage,
  P.CategoryManage,
];
const PUBLIC: Permission[] = [P.FundView, P.LedgerViewPublic];

const ids = (context: NavContext) => buildNavItems(context).map((item) => item.id);

describe('buildNavItems', () => {
  const teacher: NavContext = { kind: 'Teacher', fundId: FUND, role: 'Teacher', permissions: TEACHER };
  const treasurer: NavContext = { kind: 'Student', fundId: FUND, role: 'Treasurer', permissions: TREASURER };
  const student: NavContext = { kind: 'Student', fundId: FUND, role: 'Student', permissions: PUBLIC };
  const parent: NavContext = { kind: 'Parent', fundId: FUND, role: 'Parent', permissions: PUBLIC };

  it('GVCN có mục quản lý, không có Danh mục và Thành viên', () => {
    expect(ids(teacher)).toEqual([
      'funds',
      'overview',
      'ledger',
      'reports',
      'students',
      'treasurers',
      'settings',
    ]);
  });

  it('thủ quỹ có Danh mục, Thành viên và không có Quỹ của tôi', () => {
    expect(ids(treasurer)).toEqual(['overview', 'ledger', 'reports', 'categories', 'members']);
  });

  it('học sinh chỉ có các mục xem, nhãn Thu chi công khai', () => {
    expect(ids(student)).toEqual(['overview', 'ledger', 'reports']);
    expect(buildNavItems(student)[1].label).toBe('Thu chi công khai');
  });

  it('phụ huynh có Quỹ của con', () => {
    expect(buildNavItems(parent)[0].label).toBe('Quỹ của con');
    expect(ids(parent)).toEqual(['funds', 'overview', 'ledger', 'reports']);
  });

  it('mỗi mục có link dưới quỹ đang mở, chỉ Tổng quan khớp chính xác', () => {
    const items = buildNavItems(teacher).filter((item) => item.id !== 'funds');
    expect(items.every((item) => item.link.startsWith(`/funds/${FUND}`))).toBe(true);
    expect(items.filter((item) => item.exact).map((item) => item.id)).toEqual(['overview']);
  });

  it('chưa mở quỹ thì chỉ có danh sách quỹ', () => {
    expect(ids({ kind: 'Teacher', fundId: null, role: null, permissions: [] })).toEqual(['funds']);
  });
});
