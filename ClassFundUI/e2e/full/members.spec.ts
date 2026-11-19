import { Browser, Page, expect, test } from '@playwright/test';

/**
 * M3-T3: quản lý học sinh với ClassFund.Api thật (E2E_FULL=1) – CHỈ chạy trên PostgreSQL tạm trong Docker
 * (docs/testing/test-strategy.md), KHÔNG trỏ Neon dùng chung: các test tạo tài khoản mới.
 * Thủ quỹ chính là tài khoản seed thuquy.demo@classfund.local (mật khẩu Demo1234, không bị đổi trong file này).
 */
test.skip(process.env['E2E_FULL'] !== '1', 'Chỉ chạy với E2E_FULL=1 (cần ClassFund.Api thật).');

const OWNER = 'thuquy.demo@classfund.local';
const OWNER_PASSWORD = 'Demo1234';
const FUND_NAME = 'Lớp 10A1 (demo)';
const NEW_PASSWORD = 'MatKhauMoi1';

const newEmail = () => `hs-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;

async function loginAs(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
}

async function openMembers(page: Page): Promise<void> {
  await loginAs(page, OWNER, OWNER_PASSWORD);
  await expect(page).toHaveURL(/\/funds$/);
  await page.getByRole('link', { name: `Mở quỹ ${FUND_NAME}` }).click();
  await page.getByRole('navigation', { name: 'Điều hướng chính' }).getByRole('link', { name: 'Học sinh' }).click();
  await expect(page.getByRole('heading', { name: 'Học sinh', exact: true })).toBeVisible();
}

const rowOf = (page: Page, text: string) => page.getByRole('row', { name: new RegExp(text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')) });

/** Danh sách phân trang 20 dòng và DB tạm tích lũy theo số lần chạy: lọc theo email để hàng cần thao tác luôn ở trang 1. */
async function rowFor(page: Page, email: string) {
  await page.getByRole('searchbox', { name: 'Tìm thành viên' }).fill(email);
  return rowOf(page, email);
}

async function addStudent(page: Page, email: string, name: string): Promise<string> {
  await page.getByLabel('Email học sinh').fill(email);
  await page.getByLabel('Họ tên').fill(name);
  await page.getByRole('button', { name: 'Thêm vào lớp' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText(`Mật khẩu tạm của ${name}`);
  const password = (await dialog.locator('code.secret').innerText()).trim();
  await dialog.getByRole('button', { name: 'Đã lưu, đóng' }).click();
  return password;
}

async function newSession(browser: Browser): Promise<Page> {
  return (await browser.newContext()).newPage();
}

/** Học sinh đăng nhập bằng mật khẩu tạm, đổi mật khẩu, vào danh sách quỹ. */
async function activate(browser: Browser, email: string, temporaryPassword: string): Promise<Page> {
  const page = await newSession(browser);
  await loginAs(page, email, temporaryPassword);
  await expect(page).toHaveURL(/\/change-password$/);
  await page.getByLabel('Mật khẩu tạm', { exact: true }).fill(temporaryPassword);
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill(NEW_PASSWORD);
  await page.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill(NEW_PASSWORD);
  await page.getByRole('button', { name: 'Lưu và vào lớp' }).click();
  await expect(page).toHaveURL(/\/funds$/);
  return page;
}

test('thủ quỹ thêm học sinh → học sinh vào bằng mật khẩu tạm, đổi mật khẩu, thấy quỹ nhưng không vào được trang Học sinh', async ({ page, browser }) => {
  const email = newEmail();
  await openMembers(page);

  await expect(rowOf(page, 'Thủ quỹ Demo')).toContainText('Thủ quỹ chính');
  const temporaryPassword = await addStudent(page, email, 'Trần Mới');
  await expect((await rowFor(page, email))).toContainText('Chưa đổi mật khẩu tạm');
  await expect(page.getByText('Tam')).toHaveCount(0); // mật khẩu không còn trên trang sau khi đóng hộp thoại

  const student = await activate(browser, email, temporaryPassword);
  await student.getByRole('link', { name: `Mở quỹ ${FUND_NAME}` }).click();
  await expect(student.getByRole('navigation', { name: 'Điều hướng chính' })).toBeVisible();
  await expect(student.getByRole('navigation', { name: 'Điều hướng chính' }).getByRole('link', { name: 'Học sinh' })).toHaveCount(0);

  // Gõ thẳng đường dẫn: bị chặn (route guard + backend 403).
  await student.goto(page.url());
  await expect(student).toHaveURL(/\/forbidden$/);

  await page.reload();
  await expect((await rowFor(page, email))).toContainText('Hoạt động');
});

test('thêm nhiều email: kết quả từng dòng; mọi tài khoản mới đăng nhập được bằng mật khẩu tạm của nó', async ({ page, browser }) => {
  const [a, b] = [newEmail(), newEmail()];
  await openMembers(page);

  await page.getByRole('button', { name: 'Thêm nhiều học sinh' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel(/Dán danh sách học sinh/).fill(`${a}\n${b}\n${OWNER}\nkhong-hop-le\n${a.toUpperCase()}`);
  await expect(dialog.locator('.bulk-count')).toContainText('4 học sinh');
  await dialog.getByRole('button', { name: 'Thêm 4 học sinh' }).click();

  await expect(dialog.getByText('Mật khẩu tạm chỉ hiện lúc này')).toBeVisible();
  const rows = dialog.getByRole('row');
  await expect(rows.filter({ hasText: a })).toContainText('Đã tạo tài khoản');
  await expect(rows.filter({ hasText: OWNER })).toContainText('Học sinh này đã ở trong quỹ.');
  await expect(rows.filter({ hasText: 'khong-hop-le' })).toContainText('Email không hợp lệ.');
  const passwordA = (await rows.filter({ hasText: a }).locator('code').innerText()).trim();
  const passwordB = (await rows.filter({ hasText: b }).locator('code').innerText()).trim();
  expect(passwordA).not.toBe(passwordB);
  await dialog.getByRole('button', { name: 'Đóng' }).click();

  for (const [email, password] of [[a, passwordA], [b, passwordB]]) {
    const student = await activate(browser, email, password);
    await expect(student.getByRole('link', { name: `Mở quỹ ${FUND_NAME}` })).toBeVisible();
  }
});

test('cấp lại mật khẩu tạm: mật khẩu cũ và phiên cũ hết hiệu lực, mật khẩu mới dùng được và buộc đổi', async ({ page, browser }) => {
  const email = newEmail();
  await openMembers(page);
  const first = await addStudent(page, email, 'Lê Quên Mật Khẩu');
  const session = await newSession(browser); // học sinh đã vào bằng mật khẩu tạm nhưng CHƯA đổi mật khẩu
  await loginAs(session, email, first);
  await expect(session).toHaveURL(/\/change-password$/);

  await (await rowFor(page, email)).getByRole('button', { name: 'Cấp mật khẩu tạm' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Cấp mật khẩu tạm' }).click();
  const second = (await page.getByRole('dialog').locator('code.secret').innerText()).trim();
  expect(second).not.toBe(first);
  await page.getByRole('dialog').getByRole('button', { name: 'Đã lưu, đóng' }).click();

  expect((await session.request.get('/api/v1/auth/me')).status()).toBe(401); // BR-29: phiên cũ bị đăng xuất

  const fresh = await newSession(browser);
  await loginAs(fresh, email, first); // mật khẩu tạm cũ không còn dùng được
  await expect(fresh.getByRole('alert').filter({ hasText: 'Email hoặc mật khẩu không đúng.' })).toBeVisible();
  await loginAs(fresh, email, second);
  await expect(fresh).toHaveURL(/\/change-password$/);

  // Khi học sinh đã tự đặt mật khẩu thì thủ quỹ không cấp lại mật khẩu tạm / sửa tên nữa.
  await fresh.getByLabel('Mật khẩu tạm', { exact: true }).fill(second);
  await fresh.getByLabel('Mật khẩu mới', { exact: true }).fill(NEW_PASSWORD);
  await fresh.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill(NEW_PASSWORD);
  await fresh.getByRole('button', { name: 'Lưu và vào lớp' }).click();
  await expect(fresh.getByRole('link', { name: `Mở quỹ ${FUND_NAME}` })).toBeVisible();
  await page.reload();
  const row = await rowFor(page, email);
  await expect(row.getByRole('button', { name: 'Cấp mật khẩu tạm' })).toBeDisabled();
  await expect(row.getByRole('button', { name: 'Sửa tên' })).toBeDisabled();
});

test('sửa họ tên được lưu vào database; tài khoản tự đăng ký (AZ-07) bị khóa nút sửa và cấp mật khẩu', async ({ page, browser }) => {
  const email = newEmail();
  await openMembers(page);
  await addStudent(page, email, 'Tên Cũ');

  await (await rowFor(page, email)).getByRole('button', { name: 'Sửa tên' }).click();
  await page.getByRole('dialog').getByLabel('Họ và tên').fill('Tên Đã Sửa');
  await page.getByRole('dialog').getByRole('button', { name: 'Lưu' }).click();
  await expect(page.getByText('Đã lưu họ tên.')).toBeVisible();
  await page.reload();
  await expect((await rowFor(page, email))).toContainText('Tên Đã Sửa');

  // Một người tự đăng ký tài khoản rồi được thêm vào quỹ: thủ quỹ không được sửa tên / cấp mật khẩu thay họ.
  const selfEmail = newEmail();
  const other = await newSession(browser);
  await other.goto('/register');
  await other.getByLabel('Họ và tên').fill('Người Tự Đăng Ký');
  await other.getByLabel('Email').fill(selfEmail);
  await other.getByLabel('Mật khẩu', { exact: true }).fill('Passw0rd1');
  await other.getByLabel('Nhập lại mật khẩu', { exact: true }).fill('Passw0rd1');
  await other.getByRole('checkbox').check();
  await other.getByRole('button', { name: 'Tạo tài khoản' }).click();
  await expect(other).toHaveURL(/\/funds$/);

  await page.getByLabel('Email học sinh').fill(selfEmail);
  await page.getByLabel('Họ tên').fill('Tên bị bỏ qua');
  await page.getByRole('button', { name: 'Thêm vào lớp' }).click();
  await expect(page.getByText('Đã thêm vào lớp: Người Tự Đăng Ký.')).toBeVisible();
  const row = await rowFor(page, selfEmail);
  await expect(row.getByRole('button', { name: 'Cấp mật khẩu tạm' })).toBeDisabled();
  await expect(row.getByRole('button', { name: 'Sửa tên' })).toBeDisabled();
});

test('ngừng thành viên: mất quyền truy cập quỹ nhưng tài khoản còn; thêm lại thì vào được quỹ trở lại (BR-30)', async ({ page, browser }) => {
  const email = newEmail();
  await openMembers(page);
  const temporaryPassword = await addStudent(page, email, 'Phạm Sẽ Nghỉ');
  const student = await activate(browser, email, temporaryPassword);
  await expect(student.getByRole('link', { name: `Mở quỹ ${FUND_NAME}` })).toBeVisible();

  await (await rowFor(page, email)).getByRole('button', { name: 'Ngừng' }).click();
  await expect(page.getByRole('dialog')).toContainText('dữ liệu lịch sử vẫn được giữ lại');
  await page.getByRole('dialog').getByRole('button', { name: 'Ngừng thành viên' }).click();
  await expect(page.getByText('Đã ngừng Phạm Sẽ Nghỉ.')).toBeVisible();
  await expect((await rowFor(page, email))).toHaveCount(0);

  await student.reload();
  await expect(student.getByText('Bạn chưa thuộc quỹ nào')).toBeVisible(); // tài khoản vẫn đăng nhập được, quỹ thì không còn

  await page.getByRole('tab', { name: 'Đã ngừng' }).click();
  await expect((await rowFor(page, email))).toContainText('Đã ngừng');
  await (await rowFor(page, email)).getByRole('button', { name: 'Thêm lại' }).click();
  await expect(page.getByText('Đã kích hoạt lại: Phạm Sẽ Nghỉ.')).toBeVisible();

  await student.reload();
  await expect(student.getByRole('link', { name: `Mở quỹ ${FUND_NAME}` })).toBeVisible();
});

test('tìm kiếm theo tên và email trên dữ liệu thật; thủ quỹ chính không có nút trên hàng của mình', async ({ page }) => {
  const email = newEmail();
  await openMembers(page);
  await addStudent(page, email, 'Nguyễn Tìm Thấy');

  await page.getByRole('searchbox', { name: 'Tìm thành viên' }).fill('tìm thấy');
  await expect((await rowFor(page, email))).toBeVisible();
  await expect(rowOf(page, 'Thủ quỹ Demo')).toHaveCount(0);

  await page.getByRole('searchbox', { name: 'Tìm thành viên' }).fill('khong-ton-tai-xyz');
  await expect(page.getByText('Không có thành viên phù hợp.')).toBeVisible();

  await page.getByRole('searchbox', { name: 'Tìm thành viên' }).fill('');
  await expect(rowOf(page, 'Thủ quỹ Demo').getByRole('button')).toHaveCount(0);
});
