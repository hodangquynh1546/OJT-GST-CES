import { Page } from '@playwright/test';
import { FUND } from './mock-session';

/**
 * Dữ liệu giả cho `GET /funds/{id}/students` (hợp đồng: docs/api/backend-api.md – Students).
 * Đủ 4 `accountStatus` để module khác (M4 – chọn thủ quỹ) test giao diện không chờ M3.
 */
export type AccountStatus = 'None' | 'PendingActivation' | 'Active' | 'Disabled';

export interface MockStudent {
  id: string;
  fullName: string;
  username: string | null;
  accountStatus: AccountStatus;
  isTreasurer: boolean;
  parent: { fullName: string; email: string; pendingActivation: boolean } | null;
  createdAt: string;
}

const student = (n: number, fullName: string, username: string | null, accountStatus: AccountStatus, extra: Partial<MockStudent> = {}): MockStudent => ({
  id: `019a0000-0000-7000-8000-0000000001${String(n).padStart(2, '0')}`,
  fullName,
  username,
  accountStatus,
  isTreasurer: false,
  parent: null,
  createdAt: '2026-09-05T01:00:00Z',
  ...extra,
});

export const STUDENT_NONE = student(1, 'Lê Gia Bảo', null, 'None');
export const STUDENT_PENDING = student(2, 'Phạm Gia Huy', 'huy.pham.482', 'PendingActivation', {
  parent: { fullName: 'Phạm Văn Long', email: 'long.pham@example.vn', pendingActivation: true },
});
export const STUDENT_ACTIVE = student(3, 'Nguyễn Minh Khang', 'khang.nguyen.317', 'Active');
export const STUDENT_DISABLED = student(4, 'Trần Quốc Việt', 'viet.tran.095', 'Disabled');

/** Bốn học sinh, mỗi trạng thái một người: chỉ `STUDENT_ACTIVE` chọn được làm thủ quỹ (BR-31). */
export const STUDENTS_4_STATUS: MockStudent[] = [STUDENT_NONE, STUDENT_PENDING, STUDENT_ACTIVE, STUDENT_DISABLED];

/** Giả lập GET /funds/{fundId}/students. */
export async function mockStudents(page: Page, students: MockStudent[] = STUDENTS_4_STATUS, fundId = FUND): Promise<void> {
  await page.route(`**/api/v1/funds/${fundId}/students`, (route) =>
    route.request().method() === 'GET' ? route.fulfill({ json: students }) : route.fallback(),
  );
}
