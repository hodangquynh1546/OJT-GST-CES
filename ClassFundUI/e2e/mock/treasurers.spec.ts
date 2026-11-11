import { Page, expect, test } from '@playwright/test';

import { FUND, TEACHER, TREASURER, mockProblem, signInAs } from '../support/mock-session';
import { STUDENTS_4_STATUS, STUDENT_ACTIVE, STUDENT_DISABLED, STUDENT_NONE, STUDENT_PENDING, mockStudents } from '../support/mock-students';

/** M4 – G-03 Phân công thủ quỹ (API giả lập; Canvas board `Treasurers`; FR-27, BR-31, BR-32, BR-33). */

interface MockAssignment {
  id: string;
  studentId: string;
  studentName: string;
  isActive: boolean;
  assignedAt: string;
  assignedByName: string;
  revokedAt: string | null;
  revokedByName: string | null;
}

const TRANG_ID = '019a0000-0000-7000-8000-000000000199';

const assignment = (id: string, studentId: string, studentName: string, extra: Partial<MockAssignment> = {}): MockAssignment => ({
  id,
  studentId,
  studentName,
  isActive: true,
  assignedAt: '2026-09-05T01:00:00Z',
  assignedByName: 'Trần Văn Hùng',
  revokedAt: null,
  revokedByName: null,
  ...extra,
});

/** GET / POST /treasurers và POST …/revoke có trạng thái: phân công / thu hồi cập nhật danh sách như backend. */
async function mockTreasurers(page: Page, assignments: MockAssignment[]): Promise<{ assigned: unknown[] }> {
  const assigned: unknown[] = [];
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ json: { antiforgeryToken: 'token' } }));
  await page.route(`**/api/v1/funds/${FUND}/treasurers`, async (route) => {
    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON() as { studentId: string };
      assigned.push(body);
      const student = STUDENTS_4_STATUS.find((item) => item.id === body.studentId)!;
      assignments.unshift(assignment(`a-${assignments.length + 1}`, student.id, student.fullName, { assignedAt: '2026-11-05T02:30:00Z' }));
      return route.fulfill({ status: 201, json: { id: assignments[0].id } });
    }
    return route.fulfill({ json: [...assignments].sort((a, b) => Number(b.isActive) - Number(a.isActive)) });
  });
  await page.route(new RegExp(`/api/v1/funds/${FUND}/treasurers/[^/]+/revoke$`), async (route) => {
    const id = route.request().url().split('/').at(-2);
    const item = assignments.find((candidate) => candidate.id === id)!;
    Object.assign(item, { isActive: false, revokedAt: '2026-11-05T02:30:00Z', revokedByName: 'Trần Văn Hùng' });
    return route.fulfill({ status: 204 });
  });
  return { assigned };
}

test('quỹ chưa phân công: banner vàng → dialog chỉ chọn được học sinh Active → phân công → thủ quỹ cuối không thu hồi được', async ({ page }) => {
  await signInAs(page, TEACHER);
  await mockStudents(page);
  const { assigned } = await mockTreasurers(page, []);
  await page.goto(`/funds/${FUND}/treasurers`);

  await expect(page.getByRole('heading', { name: 'Phân công thủ quỹ', level: 1 })).toBeVisible();
  await expect(page.getByRole('status').filter({ hasText: 'Quỹ chưa phân công thủ quỹ.' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Đang là thủ quỹ (0)' })).toBeVisible();
  await expect(page.getByText('Chưa có lịch sử phân công.')).toBeVisible();

  await page.getByRole('button', { name: '+ Phân công thủ quỹ' }).click();
  const dialog = page.getByRole('dialog', { name: 'Phân công thủ quỹ' });
  const group = dialog.getByRole('radiogroup', { name: 'Chọn học sinh' });
  await expect(group.getByRole('radio', { name: STUDENT_ACTIVE.fullName })).toBeEnabled();
  for (const student of [STUDENT_NONE, STUDENT_PENDING, STUDENT_DISABLED]) {
    await expect(group.getByRole('radio', { name: student.fullName })).toBeDisabled();
  }
  await expect(dialog.getByText('Cấp tài khoản ở trang Học sinh trước')).toBeVisible();
  await expect(dialog.getByText(`${STUDENT_PENDING.username} · chưa đổi mật khẩu tạm`)).toBeVisible();
  await expect(dialog.getByText('Bị khóa', { exact: true })).toBeVisible();

  await dialog.getByRole('button', { name: 'Phân công', exact: true }).click();
  await expect(dialog.getByRole('alert')).toHaveText('Chọn một học sinh đủ điều kiện.');

  await group.getByRole('radio', { name: STUDENT_ACTIVE.fullName }).check();
  await dialog.getByRole('button', { name: 'Phân công', exact: true }).click();
  await expect(dialog).toBeHidden();
  expect(assigned).toEqual([{ studentId: STUDENT_ACTIVE.id }]);

  await expect(page.getByText(`Đã phân công ${STUDENT_ACTIVE.fullName} làm thủ quỹ.`)).toBeVisible();
  await expect(page.getByRole('status').filter({ hasText: 'Quỹ chưa phân công thủ quỹ.' })).toBeHidden();
  await expect(page.getByRole('heading', { name: 'Đang là thủ quỹ (1)' })).toBeVisible();
  await expect(page.getByText(`Phân công 05/11/2026 09:30 bởi Trần Văn Hùng · ${STUDENT_ACTIVE.username}`)).toBeVisible();
  const revoke = page.getByRole('button', { name: 'Thu hồi' });
  await expect(revoke).toBeDisabled();
  await expect(revoke).toHaveAccessibleDescription('Thủ quỹ cuối cùng – phân công người mới trước');
  await expect(page.getByRole('row', { name: new RegExp(`${STUDENT_ACTIVE.fullName}.*Đang hiệu lực`) })).toBeVisible();
});

test('đổi thủ quỹ: có 2 thủ quỹ → xác nhận thu hồi người cũ → người còn lại không thu hồi được, lịch sử ghi người thu hồi', async ({ page }) => {
  await signInAs(page, TEACHER);
  await mockStudents(page);
  await mockTreasurers(page, [
    assignment('a2', TRANG_ID, 'Phạm Thu Trang', { assignedAt: '2026-10-01T00:10:00Z' }),
    assignment('a1', STUDENT_ACTIVE.id, STUDENT_ACTIVE.fullName),
  ]);
  await page.goto(`/funds/${FUND}/treasurers`);

  await expect(page.getByRole('heading', { name: 'Đang là thủ quỹ (2)' })).toBeVisible();
  await page.getByRole('button', { name: `Thu hồi ${STUDENT_ACTIVE.fullName}` }).click();
  const confirm = page.getByRole('alert').filter({ hasText: `Thu hồi quyền thủ quỹ của ${STUDENT_ACTIVE.fullName}?` });
  await expect(confirm).toBeVisible();
  await confirm.getByRole('button', { name: 'Không' }).click();
  await expect(confirm).toBeHidden();

  await page.getByRole('button', { name: `Thu hồi ${STUDENT_ACTIVE.fullName}` }).click();
  await confirm.getByRole('button', { name: 'Thu hồi' }).click();

  await expect(page.getByText(`Đã thu hồi quyền thủ quỹ của ${STUDENT_ACTIVE.fullName}.`)).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Đang là thủ quỹ (1)' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Thu hồi' })).toBeDisabled();
  const revokedRow = page.getByRole('row', { name: new RegExp(STUDENT_ACTIVE.fullName) });
  await expect(revokedRow).toContainText('05/11/2026 09:30 · Trần Văn Hùng');
  await expect(revokedRow).toContainText('Đã thu hồi');
});

test('thu hồi đồng thời: server trả treasurer.last → hiện lý do "phân công người mới trước"', async ({ page }) => {
  await signInAs(page, TEACHER);
  await mockStudents(page);
  await mockTreasurers(page, [assignment('a2', TRANG_ID, 'Phạm Thu Trang'), assignment('a1', STUDENT_ACTIVE.id, STUDENT_ACTIVE.fullName)]);
  await mockProblem(page, `**/api/v1/funds/${FUND}/treasurers/a2/revoke`, 422, 'treasurer.last');
  await page.goto(`/funds/${FUND}/treasurers`);

  await page.getByRole('button', { name: 'Thu hồi Phạm Thu Trang' }).click();
  await page.getByRole('alert').getByRole('button', { name: 'Thu hồi' }).click();

  await expect(page.getByRole('alert')).toHaveText(
    'Không thể thu hồi thủ quỹ cuối cùng. Hãy phân công thủ quỹ mới trước rồi mới thu hồi người cũ.',
  );
});

test('phân công bị từ chối (422) → lý do hiện trong dialog, dialog vẫn mở', async ({ page }) => {
  await signInAs(page, TEACHER);
  await mockStudents(page);
  await mockTreasurers(page, []);
  await page.route(`**/api/v1/funds/${FUND}/treasurers`, (route) =>
    route.request().method() === 'POST'
      ? route.fulfill({
          status: 422,
          contentType: 'application/problem+json',
          json: { status: 422, code: 'treasurer.account_not_activated' },
        })
      : route.fallback(),
  );
  await page.goto(`/funds/${FUND}/treasurers`);
  await expect(page.getByRole('heading', { name: 'Đang là thủ quỹ (0)' })).toBeVisible();

  await page.getByRole('button', { name: '+ Phân công thủ quỹ' }).click();
  const dialog = page.getByRole('dialog', { name: 'Phân công thủ quỹ' });
  await dialog.getByRole('radio', { name: STUDENT_ACTIVE.fullName }).check();
  await dialog.getByRole('button', { name: 'Phân công', exact: true }).click();

  await expect(dialog.getByRole('alert')).toHaveText('Học sinh chưa kích hoạt tài khoản (chưa đổi mật khẩu tạm).');
  await dialog.getByRole('button', { name: 'Hủy bỏ' }).click();
  await expect(dialog).toBeHidden();
});

test('lỗi tải danh sách học sinh → AppState lỗi trong dialog, "Thử lại" tải lại', async ({ page }) => {
  await signInAs(page, TEACHER);
  await mockTreasurers(page, []);
  let fail = true;
  await page.route(`**/api/v1/funds/${FUND}/students`, (route) =>
    fail
      ? route.fulfill({ status: 500, contentType: 'application/problem+json', json: { status: 500, code: 'internal_error' } })
      : route.fulfill({ json: STUDENTS_4_STATUS }),
  );
  await page.goto(`/funds/${FUND}/treasurers`);

  await page.getByRole('button', { name: '+ Phân công thủ quỹ' }).click();
  const dialog = page.getByRole('dialog', { name: 'Phân công thủ quỹ' });
  await expect(dialog.getByRole('button', { name: 'Thử lại' })).toBeVisible();
  fail = false;
  await dialog.getByRole('button', { name: 'Thử lại' }).click();
  await expect(dialog.getByRole('radio', { name: STUDENT_ACTIVE.fullName })).toBeEnabled();
});

test('lỗi tải danh sách phân công → AppState lỗi, "Thử lại" tải lại', async ({ page }) => {
  await signInAs(page, TEACHER);
  await mockStudents(page);
  let fail = true;
  await page.route(`**/api/v1/funds/${FUND}/treasurers`, (route) =>
    fail
      ? route.fulfill({ status: 500, contentType: 'application/problem+json', json: { status: 500, code: 'internal_error' } })
      : route.fulfill({ json: [assignment('a1', STUDENT_ACTIVE.id, STUDENT_ACTIVE.fullName)] }),
  );
  await page.goto(`/funds/${FUND}/treasurers`);

  await expect(page.getByText('Không tải được dữ liệu')).toBeVisible();
  await expect(page.getByRole('button', { name: '+ Phân công thủ quỹ' })).toBeHidden();
  fail = false;
  await page.getByRole('button', { name: 'Thử lại' }).click();
  await expect(page.getByRole('heading', { name: 'Đang là thủ quỹ (1)' })).toBeVisible();
});

test('thủ quỹ (không có Treasurer.Assign) vào /treasurers → trang 403', async ({ page }) => {
  await signInAs(page, TREASURER);
  await page.goto(`/funds/${FUND}/treasurers`);
  await expect(page).toHaveURL(/\/forbidden$/);
});

test('GVCN mất quyền (403 từ server) → AppState 403', async ({ page }) => {
  await signInAs(page, TEACHER);
  await mockStudents(page);
  await mockProblem(page, `**/api/v1/funds/${FUND}/treasurers`, 403, 'forbidden');
  await page.goto(`/funds/${FUND}/treasurers`);
  await expect(page.getByText('Trang này không dành cho vai trò của bạn')).toBeVisible();
});
