import { Page, expect, test } from '@playwright/test';

import { AN, FakeAuthApi, FakeUser } from '../support/fake-auth-api';

/** M1-T3: luồng đăng nhập / đăng ký / đổi mật khẩu / tài khoản với API giả lập (docs/testing/test-strategy.md). */

const TEMP_USER: FakeUser = {
  ...AN,
  id: 'u2',
  email: 'hs@example.com',
  fullName: 'Học Sinh',
  mustChangePassword: true,
  accountPermissions: [],
};

async function setup(page: Page, signedIn: FakeUser | null = null): Promise<FakeAuthApi> {
  const api = new FakeAuthApi(page);
  api.addAccount(AN, 'Passw0rd1');
  api.addAccount(TEMP_USER, 'Temp-pass-1234');
  if (signedIn) {
    api.signIn(signedIn);
  }
  await api.install();
  return api;
}

test.describe('A-01 Đăng nhập', () => {
  test('khách vào / được đưa tới trang đăng nhập với đủ ô và liên kết', async ({ page }) => {
    await setup(page);
    await page.goto('/');

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: 'Đăng nhập' })).toBeVisible();
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.getByLabel('Mật khẩu', { exact: true })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Đăng ký làm thủ quỹ' })).toHaveAttribute('href', '/register');
  });

  test('bấm đăng nhập khi để trống hiện lỗi từng ô và không gọi API', async ({ page }) => {
    const api = await setup(page);
    await page.goto('/login');

    await page.getByRole('button', { name: 'Đăng nhập' }).click();

    await expect(page.getByText('Vui lòng nhập thông tin này.')).toHaveCount(2);
    expect(api.last('POST', '/login')).toBeUndefined();
  });

  test('email sai định dạng bị chặn ngay tại form', async ({ page }) => {
    const api = await setup(page);
    await page.goto('/login');

    await page.getByLabel('Email').fill('khong-phai-email');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('Passw0rd1');
    await page.getByRole('button', { name: 'Đăng nhập' }).click();

    await expect(page.getByText('Email không hợp lệ.')).toBeVisible();
    expect(api.last('POST', '/login')).toBeUndefined();
  });

  test('sai mật khẩu: báo lỗi chung, ở lại trang, không lộ email có tồn tại hay không', async ({ page }) => {
    await setup(page);
    await page.goto('/login');

    await page.getByLabel('Email').fill('an@example.com');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('Sai-mat-khau-1');
    await page.getByRole('button', { name: 'Đăng nhập' }).click();

    await expect(page.getByRole('alert').filter({ hasText: 'Email hoặc mật khẩu không đúng.' })).toBeVisible();
    await expect(page).toHaveURL(/\/login$/);
  });

  test('đăng nhập đúng: chuẩn hóa email, gửi token antiforgery, vào danh sách quỹ và thấy ảnh đại diện', async ({ page }) => {
    const api = await setup(page);
    await page.goto('/login');

    await page.getByLabel('Email').fill('  AN@Example.com ');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('Passw0rd1');
    await page.getByLabel('Mật khẩu', { exact: true }).press('Enter');

    await expect(page).toHaveURL(/\/funds$/);
    await expect(page.getByRole('link', { name: 'Tài khoản của Nguyễn An' })).toBeVisible();
    const login = api.last('POST', '/login');
    expect(login?.body).toEqual({ email: 'AN@Example.com', password: 'Passw0rd1' });
    expect(login?.csrf).toBe('anon-token');
  });

  test('bố cục chia đôi theo thiết kế: slogan bên trái, liên hệ thủ quỹ khi quên mật khẩu, nút đăng ký làm thủ quỹ', async ({ page }) => {
    await setup(page);
    await page.goto('/login');

    await expect(page.getByText('Quỹ lớp minh bạch, tách bạch quỹ chung và quỹ sự kiện.')).toBeAttached();
    await expect(page.getByText('Quên mật khẩu? Liên hệ thủ quỹ')).toBeVisible();
    await expect(page.getByText('CHƯA CÓ TÀI KHOẢN')).toBeVisible();
    await expect(page.getByLabel('Mật khẩu', { exact: true })).toHaveAttribute('type', 'password');
    await page.getByRole('link', { name: 'Đăng ký làm thủ quỹ' }).click();
    await expect(page).toHaveURL(/\/register$/);
  });

  test('người đã đăng nhập vào /login bị đưa về trang chính', async ({ page }) => {
    await setup(page, AN);
    await page.goto('/login');

    await expect(page).toHaveURL(/\/funds$/);
  });

  test('nút đăng nhập bị khóa trong lúc chờ để không gửi trùng', async ({ page }) => {
    const api = await setup(page);
    await page.route('**/api/v1/auth/login', async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 400));
      await route.fallback();
    });
    await page.goto('/login');
    await page.getByLabel('Email').fill('an@example.com');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('Passw0rd1');

    await page.getByRole('button', { name: 'Đăng nhập' }).click();
    await expect(page.getByRole('button', { name: 'Đang đăng nhập…' })).toBeDisabled();
    await expect(page).toHaveURL(/\/funds$/);
    expect(api.requests.filter((item) => item.path === '/login')).toHaveLength(1);
  });
});

test.describe('A-02 Đăng ký thủ quỹ', () => {
  test('kiểm tra tại form: tên trống, mật khẩu yếu, nhập lại không khớp', async ({ page }) => {
    const api = await setup(page);
    await page.goto('/register');

    await page.getByLabel('Họ và tên').fill('   ');
    await page.getByLabel('Email').fill('moi@example.com');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('abc');
    await page.getByLabel('Nhập lại mật khẩu', { exact: true }).fill('abd');
    await page.getByRole('button', { name: 'Tạo tài khoản' }).click();

    await expect(page.getByText('Vui lòng nhập thông tin này.')).toBeVisible();
    await expect(page.getByLabel('Mật khẩu', { exact: true })).toHaveAttribute('aria-invalid', 'true');
    await expect(page.getByText('○ Ít nhất 8 ký tự')).toBeVisible();
    await expect(page.getByText('Mật khẩu nhập lại chưa khớp.')).toBeVisible();
    await expect(page.getByText('Vui lòng xác nhận để tiếp tục.')).toBeVisible();
    expect(api.last('POST', '/register')).toBeUndefined();
  });

  test('danh sách yêu cầu mật khẩu đổi theo lúc gõ', async ({ page }) => {
    await setup(page);
    await page.goto('/register');

    await expect(page.getByText('○ Ít nhất 8 ký tự')).toBeVisible();
    await expect(page.getByText('○ Có chữ và số')).toBeVisible();
    await page.getByLabel('Mật khẩu', { exact: true }).fill('abcdefgh');
    await expect(page.getByText('✓ Ít nhất 8 ký tự')).toBeVisible();
    await expect(page.getByText('○ Có chữ và số')).toBeVisible();
    await page.getByLabel('Mật khẩu', { exact: true }).fill('abcdefg1');
    await expect(page.getByText('✓ Có chữ và số')).toBeVisible();
  });

  test('có dấu * cho ô bắt buộc và liên kết quay lại đăng nhập', async ({ page }) => {
    await setup(page);
    await page.goto('/register');

    await expect(page.getByText('*')).toHaveCount(4);
    await page.getByRole('link', { name: '← Quay lại đăng nhập' }).click();
    await expect(page).toHaveURL(/\/login$/);
  });

  test('email đã tồn tại: lỗi 409 hiện ngay ở ô Email', async ({ page }) => {
    await setup(page);
    await page.goto('/register');

    await page.getByLabel('Họ và tên').fill('Nguyễn An');
    await page.getByLabel('Email').fill('an@example.com');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('Passw0rd1');
    await page.getByLabel('Nhập lại mật khẩu', { exact: true }).fill('Passw0rd1');
    await page.getByRole('checkbox').check();
    await page.getByRole('button', { name: 'Tạo tài khoản' }).click();

    await expect(page.getByText('Email này đã được đăng ký.')).toBeVisible();
    await expect(page.getByLabel('Email')).toHaveAttribute('aria-invalid', 'true');
    await expect(page).toHaveURL(/\/register$/);
  });

  test('đăng ký thành công: đăng nhập luôn, vào danh sách quỹ, gửi dữ liệu đã cắt khoảng trắng', async ({ page }) => {
    const api = await setup(page);
    await page.goto('/register');

    await page.getByLabel('Họ và tên').fill('  Trần Bình ');
    await page.getByLabel('Email').fill(' binh@example.com ');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('Passw0rd1');
    await page.getByLabel('Nhập lại mật khẩu', { exact: true }).fill('Passw0rd1');
    await page.getByRole('checkbox').check();
    await page.getByRole('button', { name: 'Tạo tài khoản' }).click();

    await expect(page).toHaveURL(/\/funds$/);
    await expect(page.getByRole('link', { name: 'Tài khoản của Trần Bình' })).toBeVisible();
    expect(api.last('POST', '/register')?.body).toEqual({
      fullName: 'Trần Bình',
      email: 'binh@example.com',
      password: 'Passw0rd1',
    });
  });

  test('liên kết sang trang đăng nhập', async ({ page }) => {
    await setup(page);
    await page.goto('/register');

    await page.getByRole('link', { name: 'Đăng nhập', exact: true }).click();
    await expect(page).toHaveURL(/\/login$/);
  });
});

test.describe('A-03 Đặt mật khẩu mới (mật khẩu tạm)', () => {
  test('đăng nhập bằng mật khẩu tạm bị đưa tới /change-password và không thoát ra được', async ({ page }) => {
    await setup(page);
    await page.goto('/login');
    await page.getByLabel('Email').fill('hs@example.com');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('Temp-pass-1234');
    await page.getByRole('button', { name: 'Đăng nhập' }).click();

    await expect(page).toHaveURL(/\/change-password$/);
    await expect(page.getByRole('heading', { name: 'Đặt mật khẩu mới' })).toBeVisible();
    await expect(page.getByText('Trước khi đổi, bạn chỉ có thể đổi mật khẩu hoặc đăng xuất.')).toBeVisible();
    await expect(page.getByText('Xin chào Học Sinh.')).toBeVisible();
    await expect(page.getByLabel('Mật khẩu tạm', { exact: true })).toBeVisible();

    await page.goto('/account');
    await expect(page).toHaveURL(/\/change-password$/);
    await page.goto('/funds');
    await expect(page).toHaveURL(/\/change-password$/);
  });

  test('kiểm tra tại form: mật khẩu mới yếu, trùng mật khẩu cũ, nhập lại không khớp', async ({ page }) => {
    const api = await setup(page, TEMP_USER);
    await page.goto('/change-password');

    await page.getByLabel('Mật khẩu tạm', { exact: true }).fill('Temp-pass-1234');
    await page.getByLabel('Mật khẩu mới', { exact: true }).fill('abc');
    await page.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill('abd');
    await page.getByRole('button', { name: 'Lưu và vào lớp' }).click();
    await expect(page.getByLabel('Mật khẩu mới', { exact: true })).toHaveAttribute('aria-invalid', 'true');
    await expect(page.getByText('Mật khẩu nhập lại chưa khớp.')).toBeVisible();

    await page.getByLabel('Mật khẩu mới', { exact: true }).fill('Temp-pass-1234');
    await page.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill('Temp-pass-1234');
    await page.getByRole('button', { name: 'Lưu và vào lớp' }).click();
    await expect(page.getByText('Mật khẩu mới phải khác mật khẩu hiện tại.')).toBeVisible();
    expect(api.last('POST', '/change-password')).toBeUndefined();
  });

  test('sai mật khẩu hiện tại: lỗi hiện ở ô đó và vẫn ở lại trang (không bị đá ra đăng nhập)', async ({ page }) => {
    await setup(page, TEMP_USER);
    await page.goto('/change-password');

    await page.getByLabel('Mật khẩu tạm', { exact: true }).fill('Sai-mat-khau-1');
    await page.getByLabel('Mật khẩu mới', { exact: true }).fill('MatKhauMoi1');
    await page.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill('MatKhauMoi1');
    await page.getByRole('button', { name: 'Lưu và vào lớp' }).click();

    await expect(page.getByText('Mật khẩu hiện tại không đúng.')).toBeVisible();
    await expect(page).toHaveURL(/\/change-password$/);
  });

  test('đổi thành công: vào hệ thống, hiện thông báo, các trang khác dùng được', async ({ page }) => {
    const api = await setup(page, TEMP_USER);
    await page.goto('/change-password');

    await page.getByLabel('Mật khẩu tạm', { exact: true }).fill('Temp-pass-1234');
    await page.getByLabel('Mật khẩu mới', { exact: true }).fill('MatKhauMoi1');
    await page.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill('MatKhauMoi1');
    await page.getByRole('button', { name: 'Lưu và vào lớp' }).click();

    await expect(page).toHaveURL(/\/funds$/);
    await expect(page.getByText('Đã đổi mật khẩu.')).toBeVisible();
    expect(api.last('POST', '/change-password')?.body).toEqual({
      currentPassword: 'Temp-pass-1234',
      newPassword: 'MatKhauMoi1',
    });

    await page.goto('/account');
    await expect(page).toHaveURL(/\/account$/);
  });

  test('có thể đăng xuất ngay trên trang đổi mật khẩu bắt buộc', async ({ page }) => {
    const api = await setup(page, TEMP_USER);
    await page.goto('/change-password');

    await page.getByRole('button', { name: 'Đăng xuất' }).click();

    await expect(page).toHaveURL(/\/login$/);
    expect(api.session).toBeNull();
  });

  test('đổi mật khẩu tự nguyện (không bị ép) có nút Hủy về trang tài khoản', async ({ page }) => {
    await setup(page, AN);
    await page.goto('/change-password');

    await expect(page.getByText('Mật khẩu tạm chỉ dùng được một lần.')).toHaveCount(0);
    await expect(page.getByLabel('Mật khẩu hiện tại', { exact: true })).toBeVisible();
    await page.getByRole('link', { name: 'Hủy' }).click();
    await expect(page).toHaveURL(/\/account$/);
  });
});

test.describe('A-04 Tài khoản của tôi', () => {
  test('không có menu quỹ: chỉ thanh trên với Quỹ của tôi và Đăng xuất (theo thiết kế)', async ({ page }) => {
    await setup(page, AN);
    await page.goto('/account');

    await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible();
    await expect(page.getByText('Thông tin cá nhân dùng chung cho mọi quỹ bạn tham gia.')).toBeVisible();
    await expect(page.getByRole('navigation', { name: 'Điều hướng chính' })).toHaveCount(0);
    const bar = page.getByRole('navigation', { name: 'Tài khoản' });
    await expect(bar.getByRole('link', { name: 'Quỹ của tôi' })).toBeVisible();
    await expect(bar.getByRole('button', { name: 'Đăng xuất' })).toBeVisible();
  });

  test('hiện chữ viết tắt, họ tên, email chỉ đọc và nút đổi ảnh đại diện', async ({ page }) => {
    await setup(page, { ...AN, fullName: 'Nguyễn Minh Anh' });
    await page.goto('/account');

    await expect(page.getByText('MA', { exact: true })).toBeVisible();
    await expect(page.getByLabel('Họ và tên')).toHaveValue('Nguyễn Minh Anh');
    await expect(page.getByLabel('Email')).toHaveValue('an@example.com');
    await expect(page.getByLabel('Email')).toHaveAttribute('readonly', '');
    await expect(page.getByText('Email dùng để đăng nhập, không đổi được.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Đổi ảnh đại diện' })).toBeEnabled();
  });

  test('đổi ảnh đại diện: gửi file lên API và tải lại ảnh (TD-11)', async ({ page }) => {
    await setup(page, { ...AN, fullName: 'Nguyễn Minh Anh' });
    let uploaded = false;
    let hasImage = false;
    await page.route('**/api/v1/auth/me/avatar', (route) => {
      uploaded = true;
      hasImage = true;
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ attachmentId: 'a1' }) });
    });
    const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==', 'base64');
    await page.route('**/api/v1/users/*/avatar', (route) =>
      hasImage ? route.fulfill({ status: 200, contentType: 'image/png', body: png }) : route.fulfill({ status: 404, contentType: 'application/problem+json', body: '{"code":"not_found"}' }),
    );
    await page.goto('/account');

    await expect(page.getByText('MA', { exact: true })).toBeVisible();
    await page.locator('input[type=file]').setInputFiles({ name: 'avatar.png', mimeType: 'image/png', buffer: png });

    await expect(page.getByRole('img', { name: 'Ảnh đại diện của Nguyễn Minh Anh' })).toBeVisible();
    expect(uploaded).toBe(true);
  });

  test('Quỹ của tôi đưa về trang chính theo phiên', async ({ page }) => {
    await setup(page, AN);
    await page.goto('/account');

    await page.getByRole('link', { name: 'Quỹ của tôi' }).click();
    await expect(page).toHaveURL(/\/funds$/);
  });

  test('nút Lưu chỉ bật khi có thay đổi; lưu xong hiện thông báo và dữ liệu mới', async ({ page }) => {
    const api = await setup(page, AN);
    await page.goto('/account');
    const save = page.getByRole('button', { name: 'Lưu thay đổi' });

    await expect(save).toBeDisabled();
    await page.getByLabel('Họ và tên').fill('  Trần Bình ');
    await expect(save).toBeEnabled();
    await save.click();

    await expect(page.getByText('Đã lưu thông tin cá nhân.')).toBeVisible();
    await expect(page.getByLabel('Họ và tên')).toHaveValue('Trần Bình');
    await expect(page.getByText('BÌ', { exact: true }).or(page.getByText('TB', { exact: true }))).toBeVisible();
    expect(api.last('PUT', '/me')?.body).toEqual({ fullName: 'Trần Bình' });
    await expect(save).toBeDisabled();
  });

  test('xóa trống họ tên hiện lỗi và không gọi API', async ({ page }) => {
    const api = await setup(page, AN);
    await page.goto('/account');

    await page.getByLabel('Họ và tên').fill('');
    await page.getByRole('button', { name: 'Lưu thay đổi' }).click();

    await expect(page.getByText('Vui lòng nhập thông tin này.')).toBeVisible();
    expect(api.last('PUT', '/me')).toBeUndefined();
  });

  test('đổi mật khẩu ngay trên trang: kiểm tra tại form rồi gửi, xong thì xóa trắng form và báo thành công', async ({ page }) => {
    const api = await setup(page, AN);
    await page.goto('/account');
    const section = page.getByRole('region', { name: 'Đổi mật khẩu' });

    await section.getByLabel('Mật khẩu hiện tại').fill('Passw0rd1');
    await section.getByLabel('Mật khẩu mới').fill('abc');
    await section.getByLabel('Nhập lại', { exact: true }).fill('abd');
    await section.getByRole('button', { name: 'Đổi mật khẩu' }).click();
    await expect(section.getByText('Mật khẩu nhập lại chưa khớp.')).toBeVisible();
    expect(api.last('POST', '/change-password')).toBeUndefined();

    await section.getByLabel('Mật khẩu mới').fill('MatKhauMoi1');
    await expect(section.getByText('✓ Đạt yêu cầu: ít nhất 8 ký tự, có chữ và số')).toBeVisible();
    await section.getByLabel('Nhập lại', { exact: true }).fill('MatKhauMoi1');
    await section.getByRole('button', { name: 'Đổi mật khẩu' }).click();

    await expect(page.getByText('Đã đổi mật khẩu. Các thiết bị khác đã bị đăng xuất.')).toBeVisible();
    expect(api.last('POST', '/change-password')?.body).toEqual({ currentPassword: 'Passw0rd1', newPassword: 'MatKhauMoi1' });
    await expect(section.getByLabel('Mật khẩu hiện tại')).toHaveValue('');
    await expect(page).toHaveURL(/\/account$/);
  });

  test('đổi mật khẩu sai mật khẩu hiện tại: lỗi hiện ở ô đó và vẫn ở lại trang', async ({ page }) => {
    await setup(page, AN);
    await page.goto('/account');
    const section = page.getByRole('region', { name: 'Đổi mật khẩu' });

    await section.getByLabel('Mật khẩu hiện tại').fill('Sai-mat-khau-1');
    await section.getByLabel('Mật khẩu mới').fill('MatKhauMoi1');
    await section.getByLabel('Nhập lại', { exact: true }).fill('MatKhauMoi1');
    await section.getByRole('button', { name: 'Đổi mật khẩu' }).click();

    await expect(section.getByText('Mật khẩu hiện tại không đúng.')).toBeVisible();
    await expect(page).toHaveURL(/\/account$/);
  });

  test('đăng xuất từ thanh trên: về trang đăng nhập, không vào lại được, đăng nhập lại được ngay', async ({ page }) => {
    const api = await setup(page, AN);
    await page.goto('/account');

    await page.getByRole('navigation', { name: 'Tài khoản' }).getByRole('button', { name: 'Đăng xuất' }).click();
    await expect(page).toHaveURL(/\/login$/);
    expect(api.session).toBeNull();
    await page.goto('/account');
    await expect(page).toHaveURL(/\/login$/);

    await page.getByLabel('Email').fill('an@example.com');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('Passw0rd1');
    await page.getByRole('button', { name: 'Đăng nhập' }).click();
    await expect(page).toHaveURL(/\/funds$/);
  });

  test('phiên hết hạn giữa chừng (401) đưa người dùng về đăng nhập', async ({ page }) => {
    const api = await setup(page, AN);
    await page.goto('/account');
    await expect(page.getByLabel('Họ và tên')).toHaveValue('Nguyễn An');
    api.session = null;

    await page.getByLabel('Họ và tên').fill('Tên Mới');
    await page.getByRole('button', { name: 'Lưu thay đổi' }).click();

    await expect(page).toHaveURL(/\/login$/);
  });
});

test.describe('F-01 Quỹ của tôi (màn đầu sau đăng nhập)', () => {
  const MEMBERSHIPS: FakeUser['memberships'] = [
    { fundId: 'f1', fundName: 'Lớp 10A1', role: 'Owner', permissions: ['Fund.View'] },
    { fundId: 'f2', fundName: 'CLB Tiếng Anh', role: 'Student', permissions: ['Fund.View'] },
    { fundId: 'f3', fundName: 'Lớp 9A1', role: 'Treasurer', permissions: ['Fund.View'] },
  ];

  test('đăng nhập xong luôn vào Quỹ của tôi, không có menu', async ({ page }) => {
    await setup(page);
    await page.goto('/login');
    await page.getByLabel('Email').fill('an@example.com');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('Passw0rd1');
    await page.getByRole('button', { name: 'Đăng nhập' }).click();

    await expect(page).toHaveURL(/\/funds$/);
    await expect(page.getByRole('heading', { name: 'Quỹ của tôi' })).toBeVisible();
    await expect(page.getByRole('navigation', { name: 'Điều hướng chính' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Đăng xuất' })).toBeVisible();
  });

  test('chỉ có một quỹ vẫn vào danh sách, chọn quỹ mới mở menu', async ({ page }) => {
    await setup(page, { ...AN, memberships: [MEMBERSHIPS[0]] });
    await page.goto('/');

    await expect(page).toHaveURL(/\/funds$/);
    await page.getByRole('link', { name: 'Mở quỹ Lớp 10A1' }).click();
    await expect(page).toHaveURL(/\/funds\/f1$/);
    await expect(page.getByRole('navigation', { name: 'Điều hướng chính' })).toBeVisible();
  });

  test('thẻ quỹ hiện tên và vai trò; chọn quỹ khác vào đúng quỹ đó', async ({ page }) => {
    await setup(page, { ...AN, memberships: MEMBERSHIPS });
    await page.goto('/funds');

    const list = page.getByRole('list', { name: 'Danh sách quỹ' });
    await expect(list.getByRole('link')).toHaveCount(3);
    await expect(list.getByRole('link', { name: 'Mở quỹ Lớp 10A1' })).toContainText('Thủ quỹ chính');
    await expect(list.getByRole('link', { name: 'Mở quỹ CLB Tiếng Anh' })).toContainText('Học sinh');
    await expect(list.getByRole('link', { name: 'Mở quỹ Lớp 9A1' })).toContainText('Thủ quỹ');
    await list.getByRole('link', { name: 'Mở quỹ CLB Tiếng Anh' }).click();
    await expect(page).toHaveURL(/\/funds\/f2$/);
  });

  test('chưa thuộc quỹ nào: trạng thái rỗng và nút Tạo quỹ cho tài khoản tự đăng ký', async ({ page }) => {
    await setup(page, AN);
    await page.goto('/funds');

    await expect(page.getByText('Bạn chưa thuộc quỹ nào')).toBeVisible();
    await expect(page.getByRole('link', { name: '+ Tạo quỹ' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Tạo quỹ đầu tiên' })).toBeVisible();
  });

  test('tài khoản không có quyền tạo quỹ không thấy nút Tạo quỹ', async ({ page }) => {
    await setup(page, { ...AN, accountPermissions: [] });
    await page.goto('/funds');

    await expect(page.getByText('Bạn chưa thuộc quỹ nào')).toBeVisible();
    await expect(page.getByRole('link', { name: /Tạo quỹ/ })).toHaveCount(0);
  });

  test('ảnh đại diện dẫn tới Tài khoản; Quỹ của tôi ở Tài khoản dẫn về danh sách quỹ', async ({ page }) => {
    await setup(page, { ...AN, fullName: 'Nguyễn Minh Anh' });
    await page.goto('/funds');

    await page.getByRole('link', { name: 'Tài khoản của Nguyễn Minh Anh' }).click();
    await expect(page).toHaveURL(/\/account$/);
    await page.getByRole('link', { name: 'Quỹ của tôi' }).click();
    await expect(page).toHaveURL(/\/funds$/);
  });

  test('mật khẩu tạm: không vào được danh sách quỹ cho tới khi đổi mật khẩu', async ({ page }) => {
    await setup(page, TEMP_USER);
    await page.goto('/funds');

    await expect(page).toHaveURL(/\/change-password$/);
  });
});
