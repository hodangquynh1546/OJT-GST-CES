/** Vai trò trong danh sách thành viên (phụ huynh không có trong danh sách – FR-12). */
export type MemberRole = 'Teacher' | 'Treasurer' | 'Student';

/** Một mục của `GET /funds/{fundId}/members` (`Member.View`): chỉ họ tên và vai trò, không email / tên đăng nhập (BR-23). */
export interface Member {
  fullName: string;
  role: MemberRole;
}

export const MEMBER_ROLE_LABEL: Readonly<Record<MemberRole, string>> = {
  Teacher: 'GVCN',
  Treasurer: 'Thủ quỹ',
  Student: 'Học sinh',
};
