import { CurrentUser } from '../../core/auth/auth.models';
import { describeAccount } from './account-info';

const base: CurrentUser = {
  id: 'u1',
  kind: 'Teacher',
  fullName: 'Trần Văn Hùng',
  email: 'hung.tran@example.com',
  username: null,
  mustChangePassword: false,
  accountPermissions: ['Fund.Create'],
  memberships: [],
};

describe('describeAccount (A-04)', () => {
  it('giáo viên: email, các quỹ', () => {
    const info = describeAccount({
      ...base,
      memberships: [
        { fundId: 'a', fundName: 'Quỹ lớp 5A', role: 'Teacher', permissions: [] },
        { fundId: 'b', fundName: 'Quỹ lớp 6B', role: 'Teacher', permissions: [] },
      ],
    });
    expect(info.loginLabel).toBe('Email');
    expect(info.login).toBe('hung.tran@example.com');
    expect(info.funds).toBe('2 quỹ: Quỹ lớp 5A, Quỹ lớp 6B');
    expect(describeAccount(base).funds).toBe('Chưa có quỹ nào');
  });

  it('học sinh được phân công thủ quỹ: tên đăng nhập, ghi chú quyền do GVCN', () => {
    const info = describeAccount({
      ...base,
      kind: 'Student',
      email: null,
      username: 'khang.nguyen.204',
      memberships: [{ fundId: 'a', fundName: 'Quỹ lớp 5A', role: 'Treasurer', permissions: [] }],
    });
    expect(info.loginLabel).toBe('Tên đăng nhập');
    expect(info.login).toBe('khang.nguyen.204');
    expect(info.kindLabel).toBe('Học sinh – đang được phân công Thủ quỹ');
    expect(info.funds).toBe('Quỹ lớp 5A');
  });

  it('phụ huynh: con được liên kết kèm tên quỹ', () => {
    const info = describeAccount({
      ...base,
      kind: 'Parent',
      memberships: [
        { fundId: 'a', fundName: 'Quỹ lớp 5A', role: 'Parent', permissions: [], linkedStudents: ['Lê Gia Bảo'] },
        { fundId: 'b', fundName: 'Quỹ lớp 2B', role: 'Parent', permissions: [], linkedStudents: ['Lê Gia Hân'] },
      ],
    });
    expect(info.fundsLabel).toBe('Con được liên kết');
    expect(info.funds).toBe('Lê Gia Bảo (Quỹ lớp 5A) · Lê Gia Hân (Quỹ lớp 2B)');
  });
});
