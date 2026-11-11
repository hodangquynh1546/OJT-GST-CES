/** `accountStatus` của `GET /funds/{fundId}/students` (backend-api.md mục Students; BR-31). */
export type StudentAccountStatus = 'None' | 'PendingActivation' | 'Active' | 'Disabled';

/** Phụ huynh đang liên kết; `pendingActivation` = chưa đổi mật khẩu tạm. */
export interface StudentParent {
  fullName: string;
  email: string | null;
  pendingActivation: boolean;
}

/** Một mục của `GET /funds/{fundId}/students` – khớp `StudentResponse` (Contracts/snapshots/Students.json). */
export interface Student {
  id: string;
  fullName: string;
  username: string | null;
  accountStatus: StudentAccountStatus;
  isTreasurer: boolean;
  parent: StudentParent | null;
  createdAt: string;
}

/** Thông tin đăng nhập chỉ trả về một lần (`Cache-Control: no-store`). Không lưu ở đâu ngoài bộ nhớ của hộp thoại. */
export interface Credential {
  login: string;
  temporaryPassword: string;
}

/** Response của `POST …/parent`. Hệ thống không gửi email (D-33) nên `emailSent` luôn false. */
export interface ParentLinkResult {
  accountCreated: boolean;
  credential: Credential | null;
  emailSent: boolean;
}

/** Response 201 của `POST /students`; `parent` chỉ có khi gửi kèm email phụ huynh. */
export interface StudentCreated {
  id: string;
  parent?: ParentLinkResult;
}

export interface CreateStudentRequest {
  fullName: string;
  parentEmail?: string;
  parentFullName?: string;
}

export const STUDENT_NAME_MAX_LENGTH = 100;
export const PARENT_NAME_MAX_LENGTH = 200;
export const USERNAME_MAX_LENGTH = 60;

/** Nhãn + màu thẻ trạng thái tài khoản (Canvas G-02). */
export const ACCOUNT_STATUS: Record<
  StudentAccountStatus,
  { label: string; tone: '' | 'success' | 'warning' | 'danger'; hint: string }
> = {
  None: {
    label: 'Chưa cấp',
    tone: '',
    hint: 'Chưa có tài khoản. Cấp tên đăng nhập + mật khẩu tạm; học sinh không cần email.',
  },
  PendingActivation: {
    label: 'Chờ kích hoạt',
    tone: 'warning',
    hint: 'Đã cấp nhưng học sinh chưa đổi mật khẩu tạm → chưa phân công thủ quỹ được.',
  },
  Active: { label: 'Đang hoạt động', tone: 'success', hint: 'Học sinh đã kích hoạt tài khoản.' },
  Disabled: { label: 'Bị khóa', tone: 'danger', hint: 'Tài khoản bị khóa (chỉ mở bằng lệnh quản trị).' },
};
