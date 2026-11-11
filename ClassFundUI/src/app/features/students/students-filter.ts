import { Student } from './students.models';

/** Bộ lọc nhanh của G-02 (Canvas): "Chưa kích hoạt" gồm chưa cấp và chờ kích hoạt. */
export type StudentFilter = 'all' | 'treasurer' | 'notActivated' | 'noParent';

export const STUDENT_FILTERS: readonly { key: StudentFilter; label: string }[] = [
  { key: 'all', label: 'Tất cả' },
  { key: 'treasurer', label: 'Thủ quỹ' },
  { key: 'notActivated', label: 'Chưa kích hoạt' },
  { key: 'noParent', label: 'Chưa có phụ huynh' },
];

/** Bỏ dấu tiếng Việt và chuyển chữ thường (đ → d) để tìm không phân biệt dấu. */
export function foldText(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase();
}

function matchesFilter(student: Student, filter: StudentFilter): boolean {
  switch (filter) {
    case 'all':
      return true;
    case 'treasurer':
      return student.isTreasurer;
    case 'notActivated':
      return student.accountStatus === 'None' || student.accountStatus === 'PendingActivation';
    case 'noParent':
      return student.parent === null;
  }
}

/** Lọc theo bộ lọc nhanh và ô tìm (họ tên hoặc tên đăng nhập, không phân biệt dấu / hoa thường). */
export function filterStudents(students: readonly Student[], query: string, filter: StudentFilter): Student[] {
  const needle = foldText(query.trim());
  return students.filter(
    (student) =>
      matchesFilter(student, filter) &&
      (needle === '' || foldText(student.fullName).includes(needle) || foldText(student.username ?? '').includes(needle)),
  );
}

export function countByFilter(students: readonly Student[]): Record<StudentFilter, number> {
  return {
    all: students.length,
    treasurer: students.filter((student) => matchesFilter(student, 'treasurer')).length,
    notActivated: students.filter((student) => matchesFilter(student, 'notActivated')).length,
    noParent: students.filter((student) => matchesFilter(student, 'noParent')).length,
  };
}
