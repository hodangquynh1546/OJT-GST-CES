import { Page } from '@playwright/test';

/**
 * Chế độ **mock** (mặc định): giả lập API bằng `page.route` để test giao diện độc lập với backend.
 * Mỗi module đặt test ở `e2e/mock/<module>.spec.ts`; e2e "đúng nghĩa" (BE + DB thật) ở `e2e/full/`.
 */
export const FUND = '019a0000-0000-7000-8000-000000000001';

export const TEACHER_PERMISSIONS = ['Fund.View', 'Ledger.ViewFull', 'Fund.Manage', 'Student.Manage', 'Treasurer.Assign', 'Member.View'];
export const TREASURER_PERMISSIONS = ['Fund.View', 'Ledger.ViewFull', 'Member.View', 'Transaction.Manage', 'Category.Manage'];
export const PUBLIC_PERMISSIONS = ['Fund.View', 'Ledger.ViewPublic'];

export interface MockMembership {
  fundId: string;
  fundName: string;
  role: string;
  permissions: string[];
  linkedStudents?: string[];
}

/** Người dùng giả cho GET /auth/me (hợp đồng: docs/api/backend-api.md – Auth). Mặc định là thành viên của quỹ `FUND`. */
export function user(
  kind: string,
  role: string,
  permissions: string[],
  mustChangePassword = false,
  memberships: MockMembership[] = [{ fundId: FUND, fundName: 'Quỹ lớp 5A', role, permissions }],
) {
  return {
    id: 'u1',
    kind,
    fullName: 'Nguyễn Minh Khang',
    mustChangePassword,
    accountPermissions: kind === 'Teacher' ? ['Fund.Create'] : [],
    memberships,
  };
}

export const TEACHER = user('Teacher', 'Teacher', TEACHER_PERMISSIONS);
/** GVCN vừa đăng ký, chưa có quỹ nào (G-01 trống, chỉ có "+ Tạo quỹ"). */
export const TEACHER_NO_FUND = user('Teacher', 'Teacher', TEACHER_PERMISSIONS, false, []);
export const TREASURER = user('Student', 'Treasurer', TREASURER_PERMISSIONS);
export const STUDENT = user('Student', 'Student', PUBLIC_PERMISSIONS);
/** Phụ huynh có con trong quỹ `FUND` ("Quỹ của con"). */
export const PARENT = user('Parent', 'Parent', PUBLIC_PERMISSIONS, false, [
  { fundId: FUND, fundName: 'Quỹ lớp 5A', role: 'Parent', permissions: PUBLIC_PERMISSIONS, linkedStudents: ['Phạm Gia Huy'] },
]);

/** Giả lập GET /auth/me (null = chưa đăng nhập → 401). */
export async function signInAs(page: Page, me: object | null): Promise<void> {
  await page.route('**/api/v1/auth/me', (route) =>
    me
      ? route.fulfill({ json: me })
      : route.fulfill({ status: 401, contentType: 'application/problem+json', json: { code: 'unauthorized' } }),
  );
}

/** Trả lỗi ProblemDetails giống backend cho một route API. */
export async function mockProblem(page: Page, url: string, status: number, code: string, title = ''): Promise<void> {
  await page.route(url, (route) =>
    route.fulfill({ status, contentType: 'application/problem+json', json: { status, code, title } }),
  );
}
