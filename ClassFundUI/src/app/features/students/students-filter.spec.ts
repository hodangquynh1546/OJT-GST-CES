import { countByFilter, filterStudents, foldText } from './students-filter';
import { Student, StudentAccountStatus } from './students.models';

const student = (
  fullName: string,
  accountStatus: StudentAccountStatus,
  extra: Partial<Student> = {},
): Student => ({
  id: fullName,
  fullName,
  username: null,
  accountStatus,
  isTreasurer: false,
  parent: null,
  createdAt: '2026-09-05T01:00:00Z',
  ...extra,
});

const PARENT = { fullName: 'Lê Thu Hà', email: 'ha.le@example.com', pendingActivation: false };

const LIST: Student[] = [
  student('Nguyễn Minh Khang', 'Active', { username: 'khang.nguyen.204', isTreasurer: true, parent: PARENT }),
  student('Lê Gia Bảo', 'Active', { username: 'bao.le.517', parent: PARENT }),
  student('Trần Quốc Việt', 'PendingActivation', { username: 'viet.tran.642' }),
  student('Đỗ Hà My', 'Disabled', { username: 'my.do.155' }),
  student('Phạm Đức Huy', 'None'),
];

describe('students filter', () => {
  it('folds Vietnamese diacritics and đ', () => {
    expect(foldText('Đỗ Hà My')).toBe('do ha my');
  });

  it('searches name or username without caring about diacritics or case', () => {
    expect(filterStudents(LIST, 'bao', 'all').map((s) => s.fullName)).toEqual(['Lê Gia Bảo']);
    expect(filterStudents(LIST, 'ĐỖ', 'all').map((s) => s.fullName)).toEqual(['Đỗ Hà My']);
    expect(filterStudents(LIST, 'viet.tran', 'all').map((s) => s.fullName)).toEqual(['Trần Quốc Việt']);
    expect(filterStudents(LIST, '  ', 'all')).toHaveSize(5);
  });

  it('"Chưa kích hoạt" covers both not issued and pending accounts', () => {
    expect(filterStudents(LIST, '', 'notActivated').map((s) => s.fullName)).toEqual(['Trần Quốc Việt', 'Phạm Đức Huy']);
  });

  it('filters treasurers and students without a parent, combined with the search box', () => {
    expect(filterStudents(LIST, '', 'treasurer').map((s) => s.fullName)).toEqual(['Nguyễn Minh Khang']);
    expect(filterStudents(LIST, 'huy', 'noParent').map((s) => s.fullName)).toEqual(['Phạm Đức Huy']);
    expect(filterStudents(LIST, 'khang', 'noParent')).toEqual([]);
  });

  it('counts each filter over the whole list', () => {
    expect(countByFilter(LIST)).toEqual({ all: 5, treasurer: 1, notActivated: 2, noParent: 3 });
  });
});
