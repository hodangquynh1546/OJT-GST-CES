import { Browser, Page, expect, test } from '@playwright/test';

/**
 * M6a-T4: khoản thu chung và thanh toán với ClassFund.Api thật (E2E_FULL=1) – CHỈ chạy trên PostgreSQL tạm trong Docker
 * (docs/testing/test-strategy.md), KHÔNG trỏ Neon dùng chung: các test tạo tài khoản, khoản thu, thanh toán mới.
 * Thủ quỹ chính là tài khoản seed thuquy.demo@classfund.local (mật khẩu Demo1234).
 */
test.skip(process.env['E2E_FULL'] !== '1', 'Chỉ chạy với E2E_FULL=1 (cần ClassFund.Api thật).');

const OWNER = 'thuquy.demo@classfund.local';
const OWNER_PASSWORD = 'Demo1234';
const FUND_NAME = 'Lớp 10A1 (demo)';
const NEW_PASSWORD = 'MatKhauMoi1';

const tag = () => Math.random().toString(36).slice(2, 6).toUpperCase();
const newEmail = () => `tt-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;
const PNG = Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), Buffer.alloc(256)]);

function dueInDays(days: number): string {
  const date = new Date(Date.now() + days * 86_400_000);
  return date.toISOString().slice(0, 10);
}

async function loginAs(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
}

const sidebar = (page: Page) => page.getByRole('navigation', { name: 'Điều hướng chính' });

async function openFund(page: Page): Promise<void> {
  await expect(page).toHaveURL(/\/funds$/);
  await page.getByRole('link', { name: `Mở quỹ ${FUND_NAME}` }).click();
  await expect(sidebar(page)).toBeVisible();
}

async function ownerInFund(page: Page): Promise<void> {
  await loginAs(page, OWNER, OWNER_PASSWORD);
  await openFund(page);
}

/** Thủ quỹ chính tạo học sinh qua API (cùng phiên cookie) rồi học sinh đăng nhập, đổi mật khẩu tạm và vào quỹ. */
async function createActiveStudent(page: Page, browser: Browser, name: string): Promise<Page> {
  const email = newEmail();
  const token = (await (await page.request.get('/api/v1/auth/antiforgery')).json()).antiforgeryToken;
  const fundId = /\/funds\/([0-9a-f-]{36})/.exec(page.url())?.[1];
  const created = await page.request.post(`/api/v1/funds/${fundId}/members`, { headers: { 'X-XSRF-TOKEN': token }, data: { email, fullName: name } });
  expect(created.status()).toBe(201);
  const temporaryPassword = (await created.json()).temporaryPassword as string;

  const student = await (await browser.newContext()).newPage();
  await loginAs(student, email, temporaryPassword);
  await expect(student).toHaveURL(/\/change-password$/);
  await student.getByLabel('Mật khẩu tạm', { exact: true }).fill(temporaryPassword);
  await student.getByLabel('Mật khẩu mới', { exact: true }).fill(NEW_PASSWORD);
  await student.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill(NEW_PASSWORD);
  await student.getByRole('button', { name: 'Lưu và vào lớp' }).click();
  await openFund(student);
  return student;
}

/** Thủ quỹ tạo khoản thu qua hộp thoại H-02. */
async function createCharge(page: Page, title: string, amount: string): Promise<void> {
  await sidebar(page).getByRole('link', { name: 'Khoản thu chung' }).click();
  await page.getByRole('button', { name: '+ Tạo khoản thu' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel(/Tên khoản thu/).fill(title);
  await dialog.getByLabel('Mục đích').fill('Kiểm thử tự động');
  await dialog.getByLabel(/Số tiền mỗi người/).fill(amount);
  await dialog.getByLabel(/Hạn đóng/).fill(dueInDays(30));
  await dialog.getByRole('button', { name: 'Tạo khoản thu' }).click();
  await expect(page.getByText(`Đã tạo khoản thu “${title}”.`)).toBeVisible();
  await expect(page.getByRole('article').filter({ hasText: title })).toBeVisible();
}

test('thủ quỹ tạo khoản thu, học sinh nộp tiền mặt, thủ quỹ xác nhận → học sinh thấy đã đóng', async ({ page, browser }) => {
  await ownerInFund(page);
  const code = tag();
  const student = await createActiveStudent(page, browser, `Học Sinh Nộp ${code}`);
  const title = `Quỹ kiểm thử ${code}`;
  await createCharge(page, title, '50000');

  // Học sinh thấy khoản cần đóng và nộp tiền mặt.
  await sidebar(student).getByRole('link', { name: 'Khoản cần đóng' }).click();
  const mine = student.getByRole('article').filter({ hasText: title });
  await expect(mine.getByText('Chưa đóng')).toBeVisible();
  await mine.getByRole('button', { name: 'Nộp tiền' }).click();
  await student.getByRole('dialog').getByRole('button', { name: 'Xác nhận đã nộp' }).click();
  await expect(student.getByText('Đã gửi. Thủ quỹ sẽ xác nhận khoản đóng của bạn.')).toBeVisible();
  await expect(mine.getByText('Chờ xác nhận')).toBeVisible();

  // Thủ quỹ xác nhận trong hàng chờ.
  await sidebar(page).getByRole('link', { name: 'Xác nhận thanh toán' }).click();
  await page.getByRole('button', { name: new RegExp(`Học Sinh Nộp ${code}`) }).click();
  await expect(page.getByRole('heading', { name: `Học Sinh Nộp ${code} · ${title}` })).toBeVisible();
  await page.getByRole('button', { name: 'Xác nhận đã nhận tiền' }).click();
  await expect(page.getByText(`Đã xác nhận khoản đóng của Học Sinh Nộp ${code}.`)).toBeVisible();
  await expect(page.getByRole('button', { name: new RegExp(`Học Sinh Nộp ${code}`) })).toHaveCount(0);

  // Học sinh tải lại: khoản đã nằm ở lịch sử đã đóng.
  await student.reload();
  await expect(student.getByRole('region', { name: 'Lịch sử đã đóng' }).getByText(title)).toBeVisible();
  await expect(student.getByRole('region', { name: 'Cần đóng' }).getByText(title)).toHaveCount(0);

  // Thủ quỹ theo dõi: đã đóng 1 người (học sinh), không còn chờ xác nhận.
  await sidebar(page).getByRole('link', { name: 'Khoản thu chung' }).click();
  await page.getByRole('article').filter({ hasText: title }).getByRole('link', { name: 'Theo dõi' }).click();
  await expect(page.getByRole('heading', { name: title })).toBeVisible();
  await page.getByRole('tab', { name: 'Đã đóng' }).click();
  await expect(page.getByRole('row').filter({ hasText: `Học Sinh Nộp ${code}` })).toContainText('Đã đóng');
});

test('chuyển khoản cần ảnh minh chứng; thủ quỹ xem ảnh rồi từ chối kèm lý do → học sinh nộp lại', async ({ page, browser }) => {
  await ownerInFund(page);
  const code = tag();
  const student = await createActiveStudent(page, browser, `Học Sinh CK ${code}`);
  const title = `Quỹ chuyển khoản ${code}`;
  await createCharge(page, title, '30000');

  await sidebar(student).getByRole('link', { name: 'Khoản cần đóng' }).click();
  await student.getByRole('article').filter({ hasText: title }).getByRole('button', { name: 'Nộp tiền' }).click();
  const dialog = student.getByRole('dialog');
  await dialog.getByText('Chuyển khoản', { exact: true }).click();
  await expect(dialog.getByRole('button', { name: 'Xác nhận đã nộp' })).toBeDisabled(); // chưa có minh chứng (BR-11)
  await dialog.locator('input[type=file]').setInputFiles({ name: 'bill.png', mimeType: 'image/png', buffer: PNG });
  await expect(dialog.getByText('bill.png')).toBeVisible();
  await dialog.getByLabel('Mã giao dịch (nếu có)').fill('FT12345');
  await dialog.getByRole('button', { name: 'Xác nhận đã nộp' }).click();
  await expect(student.getByText('Đã gửi. Thủ quỹ sẽ xác nhận khoản đóng của bạn.')).toBeVisible();

  // Thủ quỹ thấy mã giao dịch + ảnh, từ chối cần lý do.
  await sidebar(page).getByRole('link', { name: 'Xác nhận thanh toán' }).click();
  await page.getByRole('button', { name: new RegExp(`Học Sinh CK ${code}`) }).click();
  await expect(page.getByText('FT12345')).toBeVisible();
  await expect(page.getByRole('img', { name: 'Minh chứng bill.png' })).toBeVisible();
  await page.getByRole('button', { name: 'Từ chối', exact: true }).click();
  await expect(page.getByText('Vui lòng nhập lý do từ chối.')).toBeVisible();
  await page.getByLabel(/Lý do từ chối/).fill('Ảnh bill không thấy số tiền');
  await page.getByRole('button', { name: 'Từ chối', exact: true }).click();
  await expect(page.getByText(`Đã từ chối khoản đóng của Học Sinh CK ${code}.`)).toBeVisible();

  // Học sinh thấy lý do và nộp lại được.
  await student.reload();
  const mine = student.getByRole('article').filter({ hasText: title });
  await expect(mine.getByText('Bị từ chối: “Ảnh bill không thấy số tiền” – vui lòng gửi lại')).toBeVisible();
  await mine.getByRole('button', { name: 'Gửi lại' }).click();
  await student.getByRole('dialog').getByRole('button', { name: 'Xác nhận đã nộp' }).click();
  await expect(mine.getByText('Chờ xác nhận')).toBeVisible();
});

test('thủ quỹ tự nộp cho mình: tự ghi nhận ngay và gắn nhãn "Tự ghi nhận"', async ({ page }) => {
  await ownerInFund(page);
  const title = `Quỹ tự ghi nhận ${tag()}`;
  await createCharge(page, title, '20000');

  await sidebar(page).getByRole('link', { name: 'Khoản cần đóng' }).click();
  await page.getByRole('article').filter({ hasText: title }).getByRole('button', { name: 'Nộp tiền' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận đã nộp' }).click();
  await expect(page.getByText('Đã tự ghi nhận khoản đóng của bạn.')).toBeVisible();
  await expect(page.getByRole('region', { name: 'Lịch sử đã đóng' }).getByText(title)).toBeVisible();
  await expect(page.getByRole('region', { name: 'Lịch sử đã đóng' }).getByText('Tự ghi nhận').first()).toBeVisible();

  await sidebar(page).getByRole('link', { name: 'Khoản thu chung' }).click();
  await page.getByRole('article').filter({ hasText: title }).getByRole('link', { name: 'Theo dõi' }).click();
  await page.getByRole('searchbox', { name: 'Tìm học sinh' }).fill(OWNER); // danh sách phân trang: tìm trước
  await expect(page.getByRole('row').filter({ hasText: OWNER }).getByText('Tự ghi nhận', { exact: true })).toBeVisible();
});

test('hạn đóng trong quá khứ bị chặn ngay ở hộp thoại tạo khoản thu', async ({ page }) => {
  await ownerInFund(page);
  await sidebar(page).getByRole('link', { name: 'Khoản thu chung' }).click();
  await page.getByRole('button', { name: '+ Tạo khoản thu' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: 'Tạo khoản thu' }).click();
  await expect(dialog.getByText('Vui lòng nhập tên khoản thu.')).toBeVisible();
  await expect(dialog.getByText('Số tiền mỗi người phải lớn hơn 0.')).toBeVisible();
  await expect(dialog.getByText('Vui lòng chọn hạn đóng.')).toBeVisible();
});

test('thủ quỹ nhắc người chưa đóng và ghi nhận tiền mặt thay học sinh; học sinh vào quỹ sau vẫn có nghĩa vụ', async ({ page, browser }) => {
  await ownerInFund(page);
  const title = `Quỹ nhắc ${tag()}`;
  await createCharge(page, title, '40000');

  // Học sinh thêm vào quỹ SAU khi khoản thu đã tạo vẫn phải đóng.
  const code = tag();
  const student = await createActiveStudent(page, browser, `Vào Muộn ${code}`);
  await sidebar(student).getByRole('link', { name: 'Khoản cần đóng' }).click();
  await expect(student.getByRole('article').filter({ hasText: title })).toBeVisible();

  await sidebar(page).getByRole('link', { name: 'Khoản thu chung' }).click();
  await page.getByRole('article').filter({ hasText: title }).getByRole('link', { name: 'Theo dõi' }).click();
  await page.getByRole('button', { name: 'Gửi nhắc người chưa đóng' }).click();
  await expect(page.getByText(/Đã gửi nhắc cho \d+ người chưa đóng\./)).toBeVisible();
  await page.getByRole('button', { name: 'Gửi nhắc người chưa đóng' }).click();
  await expect(page.getByText('Vừa nhắc xong. Vui lòng chờ ít nhất 1 giờ rồi nhắc lại.')).toBeVisible();

  await page.getByRole('searchbox', { name: 'Tìm học sinh' }).fill(`Vào Muộn ${code}`); // danh sách phân trang: tìm trước
  const row = page.getByRole('row').filter({ hasText: `Vào Muộn ${code}` });
  await row.getByRole('button', { name: 'Ghi nhận tiền mặt' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Ghi nhận' }).click();
  await expect(page.getByText(`Đã ghi nhận tiền mặt của Vào Muộn ${code}.`)).toBeVisible();
  await expect(row).toContainText('Đã đóng');
  await expect(row.getByText('Tự ghi nhận')).toHaveCount(0);

  await student.reload();
  await expect(student.getByRole('region', { name: 'Lịch sử đã đóng' }).getByText(title)).toBeVisible();
});
