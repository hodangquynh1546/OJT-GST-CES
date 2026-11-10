import { CurrentUser } from '../../core/auth/auth.models';

export interface AccountInfo {
  loginLabel: string;
  login: string;
  kindLabel: string;
  fundsLabel: string;
  funds: string;
  note: string;
}

/** Nội dung thẻ "Thông tin" của A-04 theo loại tài khoản (board Account trên Canvas). */
export function describeAccount(user: CurrentUser): AccountInfo {
  const fundNames = user.memberships.map((item) => item.fundName);
  switch (user.kind) {
    case 'Teacher':
      return {
        loginLabel: 'Email',
        login: user.email ?? '',
        kindLabel: 'Giáo viên (GVCN của quỹ mình tạo)',
        fundsLabel: 'Các quỹ',
        funds: fundNames.length === 0 ? 'Chưa có quỹ nào' : `${fundNames.length} quỹ: ${fundNames.join(', ')}`,
        note: 'Loại tài khoản cố định là Giáo viên. Bạn là GVCN của các quỹ mình tạo; không có quyền ghi thu chi.',
      };
    case 'Parent': {
      const children = user.memberships.flatMap((item) =>
        (item.linkedStudents ?? []).map((name) => `${name} (${item.fundName})`),
      );
      return {
        loginLabel: 'Email',
        login: user.email ?? '',
        kindLabel: 'Phụ huynh',
        fundsLabel: 'Con được liên kết',
        funds: children.length === 0 ? 'Chưa có' : children.join(' · '),
        note: 'Tài khoản phụ huynh được tạo khi GVCN liên kết email của bạn với học sinh. Con là thủ quỹ không làm bạn có quyền thủ quỹ.',
      };
    }
    default: {
      const treasurer = user.memberships.some((item) => item.role === 'Treasurer');
      return {
        loginLabel: 'Tên đăng nhập',
        login: user.username ?? '',
        kindLabel: treasurer ? 'Học sinh – đang được phân công Thủ quỹ' : 'Học sinh',
        fundsLabel: 'Quỹ',
        funds: fundNames.length === 0 ? 'Chưa có' : fundNames.join(', '),
        note: treasurer
          ? 'Quyền thủ quỹ do giáo viên chủ nhiệm phân công / thu hồi. Bạn không tự đổi vai trò; tên đăng nhập do GVCN cấp.'
          : 'Tên đăng nhập do giáo viên chủ nhiệm cấp. Quên mật khẩu: nhờ GVCN cấp lại mật khẩu tạm.',
      };
    }
  }
}
