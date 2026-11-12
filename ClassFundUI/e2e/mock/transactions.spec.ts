import { Page, Route, expect, test } from '@playwright/test';

import {
  CATEGORIES_FULL,
  CATEGORIES_PUBLIC,
  CATEGORY_THU_KHAC,
  CATEGORY_VAN_PHONG_PHAM,
  mockCategories,
} from '../support/mock-categories';
import { FUND, PARENT, PUBLIC_PERMISSIONS, TEACHER, TEACHER_PERMISSIONS, TREASURER, TREASURER_PERMISSIONS, signInAs, user } from '../support/mock-session';

/** M6 – S-02 Thu chi + T-01 thêm / sửa / hủy (API giả lập; Canvas board `Ledger`, `LedgerTeacher`, `TxDialog`). */

interface MockTx {
  id: string;
  type: 'Income' | 'Expense';
  amount: number;
  occurredOn: string;
  categoryId: string;
  categoryName: string;
  description: string;
  isCancelled: boolean;
  cancelledOn?: string;
  note?: string | null;
  isPublic?: boolean;
  cancelReason?: string;
  cancelledAt?: string;
  createdAt?: string;
  createdByName?: string;
  updatedByName?: string;
  updatedAt?: string;
  cancelledByName?: string;
}


/** Hôm nay theo giờ Việt Nam – khoản mẫu nằm trong tháng mặc định của bộ lọc. */
const TODAY = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date());
const DMY = TODAY.split('-').reverse().join('/');

const sample = (): MockTx[] => [
  {
    id: 't2', type: 'Expense', amount: 180_000, occurredOn: TODAY, categoryId: CATEGORY_VAN_PHONG_PHAM.id, categoryName: 'Văn phòng phẩm',
    description: 'Mua giấy A4, bút dạ', isCancelled: false, note: 'Hóa đơn lưu tại thủ quỹ', isPublic: true,
    createdAt: `${TODAY}T01:00:00Z`, createdByName: 'Nguyễn Minh Khang',
  },
  {
    id: 't1', type: 'Income', amount: 300_000, occurredOn: TODAY, categoryId: CATEGORY_THU_KHAC.id, categoryName: 'Thu khác',
    description: 'Ủng hộ của phụ huynh', isCancelled: false, note: 'Xin không công khai tên', isPublic: false,
    createdAt: `${TODAY}T00:30:00Z`, createdByName: 'Nguyễn Minh Khang',
  },
];

/** Backend giả có trạng thái cho `/transactions…`, `/categories`, `/funds`. Ghi lại body các request ghi. */
async function mockLedger(page: Page, me: ReturnType<typeof user>, permissions: string[], items = sample(), balance = 120_000) {
  const posts: { type: string; amount: number; idempotencyKey: string; isPublic: boolean }[] = [];
  const queries: URLSearchParams[] = [];
  const full = permissions.includes('Ledger.ViewFull');
  const visible = () => items.filter((tx) => full || tx.isPublic);
  const publicView = (tx: MockTx) =>
    full
      ? tx
      : {
          id: tx.id, type: tx.type, amount: tx.amount, occurredOn: tx.occurredOn, categoryId: tx.categoryId,
          categoryName: tx.categoryName, description: tx.description, isCancelled: tx.isCancelled,
          ...(tx.cancelledOn ? { cancelledOn: tx.cancelledOn } : {}),
        };

  await signInAs(page, me);
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ json: { antiforgeryToken: 'token' } }));
  await page.route('**/api/v1/funds', (route) =>
    route.fulfill({ json: [{ id: FUND, name: 'Quỹ lớp 5A', currentBalance: balance, createdAt: '2026-09-01T01:00:00Z', permissions }] }),
  );
  await mockCategories(page, full ? CATEGORIES_FULL : CATEGORIES_PUBLIC);
  await page.route(new RegExp(`/api/v1/funds/${FUND}/transactions(\\?.*)?$`), async (route) => {
    const request = route.request();
    if (request.method() === 'POST') {
      const body = request.postDataJSON();
      posts.push(body);
      const replay = items.find((tx) => (tx as MockTx & { key?: string }).key === body.idempotencyKey);
      if (replay) {
        return route.fulfill({ status: 200, json: { ...replay, replayed: true } });
      }
      const category = CATEGORIES_FULL.find((item) => item.id === body.categoryId)!;
      const created: MockTx & { key: string } = {
        id: `n${items.length + 1}`, key: body.idempotencyKey, type: body.type, amount: body.amount, occurredOn: body.occurredOn,
        categoryId: body.categoryId, categoryName: category.name, description: body.description, note: body.note,
        isPublic: body.isPublic, isCancelled: false, createdAt: new Date().toISOString(), createdByName: 'Nguyễn Minh Khang',
      };
      items.unshift(created);
      balance += body.type === 'Income' ? body.amount : -body.amount;
      return route.fulfill({ status: 201, json: { ...created, replayed: false, isNegativeBalance: balance < 0 } });
    }
    const query = new URL(request.url()).searchParams;
    queries.push(query);
    const status = query.get('status') ?? 'Active';
    const search = query.get('search')?.toLowerCase();
    const rows = visible().filter(
      (tx) =>
        (status === 'All' || (status === 'Cancelled') === tx.isCancelled) &&
        (!query.get('type') || tx.type === query.get('type')) &&
        (!query.get('categoryId') || tx.categoryId === query.get('categoryId')) &&
        (!search || tx.description.toLowerCase().includes(search)),
    );
    return route.fulfill({ json: { items: rows.map(publicView), total: rows.length, page: 1, pageSize: 20 } });
  });
  await page.route(new RegExp(`/api/v1/funds/${FUND}/transactions/[^/]+(/history|/cancel)?$`), async (route: Route) => {
    const request = route.request();
    const [, id, action] = /transactions\/([^/]+)(?:\/(history|cancel))?$/.exec(new URL(request.url()).pathname)!;
    const tx = visible().find((item) => item.id === id);
    if (!tx) {
      return route.fulfill({ status: 404, contentType: 'application/problem+json', json: { status: 404, code: 'not_found' } });
    }
    if (action === 'history') {
      return full
        ? route.fulfill({ json: [{ occurredAt: tx.createdAt, action: 'Transaction.Create', actorName: 'Nguyễn Minh Khang', oldValues: null, newValues: { amount: tx.amount, categoryId: tx.categoryId } }] })
        : route.fulfill({ status: 403, contentType: 'application/problem+json', json: { status: 403, code: 'forbidden' } });
    }
    if (action === 'cancel') {
      Object.assign(tx, { isCancelled: true, cancelReason: request.postDataJSON().reason, cancelledByName: 'Nguyễn Minh Khang', cancelledAt: new Date().toISOString(), cancelledOn: TODAY });
      return route.fulfill({ json: { ...tx, isNegativeBalance: false } });
    }
    if (request.method() === 'PUT') {
      const body = request.postDataJSON();
      Object.assign(tx, body, { updatedByName: 'Nguyễn Minh Khang', updatedAt: new Date().toISOString() });
      return route.fulfill({ json: { ...tx, isNegativeBalance: false } });
    }
    return route.fulfill({ json: publicView(tx) });
  });
  return { posts, queries, items };
}

const dialog = (page: Page, name: string | RegExp) => page.getByRole('dialog', { name });

test('thủ quỹ thêm khoản chi ≤ 3 thao tác: lỗi sau khi Lưu, cảnh báo số dư âm, bấm Lưu nhiều lần không ghi trùng', async ({ page }) => {
  const { posts } = await mockLedger(page, TREASURER, TREASURER_PERMISSIONS);
  await page.goto(`/funds/${FUND}/transactions`);
  await expect(page.getByRole('heading', { name: 'Thu chi', level: 1 })).toBeVisible();
  await expect(page.getByText('2 giao dịch · 20 / trang')).toBeVisible();

  await page.getByRole('button', { name: '+ Khoản chi' }).click();
  const add = dialog(page, 'Thêm khoản chi');
  await add.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(add.getByText('Kiểm tra 3 ô được đánh dấu.')).toBeVisible();

  await add.getByRole('textbox', { name: 'Nội dung *' }).fill('Mua bóng đá');
  await add.getByRole('spinbutton', { name: 'Số tiền (đ) *' }).fill('250000');
  await add.getByRole('combobox', { name: 'Danh mục *' }).click();
  await page.getByRole('option', { name: 'Chi khác' }).click();
  await expect(add.getByText('Sau khoản chi này, số dư quỹ sẽ là −130.000 đ')).toBeVisible();

  await add.getByRole('button', { name: 'Lưu', exact: true }).dblclick();
  await expect(add).toBeHidden();
  expect(posts.length).toBeGreaterThanOrEqual(1);
  expect(new Set(posts.map((post) => post.idempotencyKey)).size).toBe(1);
  expect(posts[0]).toMatchObject({ type: 'Expense', amount: 250_000, isPublic: true });

  await expect(page.getByText(/Đã lưu khoản chi 250\.000 đ\./)).toBeVisible();
  await expect(page.getByText('3 giao dịch · 20 / trang')).toBeVisible();
  const detail = page.getByRole('complementary', { name: 'Chi tiết giao dịch' });
  await expect(detail.getByRole('heading', { name: 'Mua bóng đá' })).toBeVisible();
  await expect(detail).toContainText('−250.000 đ');
  await expect(page).toHaveURL(new RegExp(`/funds/${FUND}/transactions/n3$`));
});

test('thủ quỹ sửa (không đổi loại) rồi hủy có lý do: khoản rời bộ lọc "Đang hiệu lực", vẫn xem được ở "Đã hủy"', async ({ page }) => {
  await mockLedger(page, TREASURER, TREASURER_PERMISSIONS);
  await page.goto(`/funds/${FUND}/transactions/t2`);
  const detail = page.getByRole('complementary', { name: 'Chi tiết giao dịch' });
  await expect(detail).toContainText('Hóa đơn lưu tại thủ quỹ');
  await expect(detail).toContainText(`Tạo · ${DMY}`);

  await detail.getByRole('button', { name: 'Sửa' }).click();
  const edit = dialog(page, 'Sửa khoản chi');
  await expect(edit.getByText('Không đổi được loại thu ↔ chi')).toBeVisible();
  await edit.getByRole('spinbutton', { name: 'Số tiền (đ) *' }).fill('200000');
  await edit.getByRole('button', { name: 'Lưu', exact: true }).click();
  await expect(edit).toBeHidden();
  await expect(page.getByText('Đã lưu thay đổi. Lịch sử thay đổi ghi giá trị trước / sau.')).toBeVisible();
  await expect(detail).toContainText('−200.000 đ');

  await detail.getByRole('button', { name: 'Hủy giao dịch' }).click();
  const cancel = dialog(page, 'Hủy giao dịch?');
  await cancel.getByRole('button', { name: 'Hủy giao dịch' }).click();
  await expect(cancel.getByText('Nhập lý do hủy (bắt buộc, tối đa 500 ký tự).')).toBeVisible();
  await cancel.getByRole('textbox', { name: 'Lý do hủy *' }).fill('Ghi trùng');
  await cancel.getByRole('button', { name: 'Hủy giao dịch' }).click();
  await expect(cancel).toBeHidden();

  await expect(page.getByText('Đã hủy giao dịch. Khoản này không còn tính vào số dư.')).toBeVisible();
  await expect(detail).toContainText('Lý do: Ghi trùng');
  await expect(detail.getByRole('button', { name: 'Sửa' })).toBeHidden();
  await expect(page.getByText('1 giao dịch · 20 / trang')).toBeVisible();

  await page.getByRole('combobox', { name: 'Trạng thái' }).click();
  await page.getByRole('option', { name: 'Đã hủy' }).click();
  await expect(page.getByRole('row', { name: /Mua giấy A4/ })).toContainText('Đã hủy');
});

test('bộ lọc gửi đúng tham số và "Xóa bộ lọc" về mặc định', async ({ page }) => {
  const { queries } = await mockLedger(page, TREASURER, TREASURER_PERMISSIONS);
  await page.goto(`/funds/${FUND}/transactions`);
  await expect(page.getByText('2 giao dịch · 20 / trang')).toBeVisible();

  await page.getByRole('group', { name: 'Loại' }).getByRole('button', { name: 'Thu' }).click();
  await expect(page.getByText('1 giao dịch · 20 / trang')).toBeVisible();
  expect(queries.at(-1)!.get('type')).toBe('Income');

  await page.getByRole('textbox', { name: 'Tìm theo nội dung' }).fill('không có');
  await expect(page.getByText('Không có giao dịch phù hợp')).toBeVisible();
  expect(queries.at(-1)!.get('search')).toBe('không có');

  await page.getByRole('button', { name: 'Xóa bộ lọc' }).first().click();
  await expect(page.getByText('2 giao dịch · 20 / trang')).toBeVisible();
  const last = queries.at(-1)!;
  expect(last.has('type')).toBe(false);
  expect(last.has('search')).toBe(false);
  expect(last.get('from')).toBe(`${TODAY.slice(0, 7)}-01`);
});

test('ngày tương lai bị server từ chối → lỗi gắn vào ô Ngày', async ({ page }) => {
  await mockLedger(page, TREASURER, TREASURER_PERMISSIONS);
  await page.route(new RegExp(`/api/v1/funds/${FUND}/transactions$`), (route) =>
    route.request().method() === 'POST'
      ? route.fulfill({ status: 422, contentType: 'application/problem+json', json: { status: 422, code: 'transaction.future_date' } })
      : route.fallback(),
  );
  await page.goto(`/funds/${FUND}/transactions`);

  await page.getByRole('button', { name: '+ Khoản thu' }).click();
  const add = dialog(page, 'Thêm khoản thu');
  await add.getByRole('textbox', { name: 'Nội dung *' }).fill('Đóng quỹ');
  await add.getByRole('spinbutton', { name: 'Số tiền (đ) *' }).fill('100000');
  await add.getByRole('combobox', { name: 'Danh mục *' }).click();
  await page.getByRole('option', { name: 'Đóng quỹ' }).click();
  await add.getByRole('button', { name: 'Lưu', exact: true }).click();

  await expect(add.getByText('Ngày giao dịch không được ở tương lai.')).toBeVisible();
  await expect(add).toBeVisible();
});

test('GVCN: xem đủ chi tiết, ghi chú, lịch sử nhưng không có nút ghi / sửa / hủy', async ({ page }) => {
  await mockLedger(page, TEACHER, TEACHER_PERMISSIONS);
  await page.goto(`/funds/${FUND}/transactions/t1`);

  await expect(page.getByText('GVCN chỉ xem (không ghi thu chi)')).toBeVisible();
  await expect(page.getByRole('button', { name: '+ Khoản chi' })).toBeHidden();
  const detail = page.getByRole('complementary', { name: 'Chi tiết giao dịch' });
  await expect(detail).toContainText('Xin không công khai tên');
  await expect(detail).toContainText('Không công khai: phụ huynh không thấy');
  await expect(detail).toContainText('Lịch sử thay đổi');
  await expect(detail.getByRole('button', { name: 'Sửa' })).toBeHidden();
  await expect(page.getByRole('row', { name: /Ủng hộ của phụ huynh/ })).toContainText('Không công khai');
});

test('phụ huynh: "Thu chi công khai", không thấy khoản không công khai, chi tiết không có ghi chú / lịch sử', async ({ page }) => {
  await mockLedger(page, PARENT, PUBLIC_PERMISSIONS);
  await page.goto(`/funds/${FUND}/transactions`);

  await expect(page.getByRole('heading', { name: 'Thu chi công khai', level: 1 })).toBeVisible();
  await expect(page.getByText('1 giao dịch công khai · 20 / trang')).toBeVisible();
  await expect(page.getByText('Ủng hộ của phụ huynh')).toBeHidden();

  await page.getByRole('button', { name: 'Mua giấy A4, bút dạ' }).click();
  const detail = page.getByRole('complementary', { name: 'Chi tiết giao dịch' });
  await expect(detail.getByRole('heading', { name: 'Mua giấy A4, bút dạ' })).toBeVisible();
  await expect(detail).not.toContainText('Hóa đơn lưu tại thủ quỹ');
  await expect(detail).not.toContainText('Lịch sử thay đổi');
  await expect(detail).toContainText('Phụ huynh và học sinh không xem được ghi chú nội bộ');

  // Mở thẳng khoản không công khai → 404.
  await page.goto(`/funds/${FUND}/transactions/t1`);
  await expect(page.getByText('Không tìm thấy giao dịch')).toBeVisible();
});

test('lỗi tải danh sách → AppState lỗi, "Thử lại" tải lại', async ({ page }) => {
  await mockLedger(page, TREASURER, TREASURER_PERMISSIONS);
  let fail = true;
  await page.route(new RegExp(`/api/v1/funds/${FUND}/transactions\\?`), (route) =>
    fail ? route.fulfill({ status: 500, contentType: 'application/problem+json', json: { status: 500, code: 'internal_error' } }) : route.fallback(),
  );
  await page.goto(`/funds/${FUND}/transactions`);

  await expect(page.getByText('Không tải được dữ liệu')).toBeVisible();
  fail = false;
  await page.getByRole('button', { name: 'Thử lại' }).click();
  await expect(page.getByText('2 giao dịch · 20 / trang')).toBeVisible();
});
