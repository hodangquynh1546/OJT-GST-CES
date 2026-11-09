/**
 * Thông báo theo `code` trong ProblemDetails; không hiện thông tin kỹ thuật (AppState trên Canvas).
 * Mã chung khai báo ở đây; mỗi module khai báo mã riêng trong file `features/<module>/<module>.errors.ts`
 * rồi gọi `registerErrorMessages(...)` – không sửa file này khi làm module.
 */
const MESSAGES: Record<string, string> = {
  network_error: 'Không kết nối được máy chủ. Dữ liệu của bạn không bị thay đổi.',
  unknown_error: 'Đã có lỗi xảy ra. Vui lòng thử lại.',
  internal_error: 'Đã có lỗi xảy ra. Vui lòng thử lại.',
  antiforgery_failed: 'Phiên làm việc không hợp lệ. Vui lòng tải lại trang.',
  unauthorized: 'Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.',
  forbidden: 'Bạn không có quyền thực hiện thao tác này.',
  not_found: 'Không tìm thấy dữ liệu.',
  'auth.rate_limited': 'Bạn đã thử quá nhiều lần. Vui lòng chờ 1 phút rồi thử lại.',
  'password.change_required': 'Bạn cần đặt mật khẩu mới trước khi tiếp tục.',
};

/** Đăng ký thông báo cho mã lỗi của một module, ví dụ `{ 'treasurer.last': '…' }`. Mã đã có thì không ghi đè. */
export function registerErrorMessages(messages: Readonly<Record<string, string>>): void {
  for (const [code, message] of Object.entries(messages)) {
    MESSAGES[code] ??= message;
  }
}

export function messageForCode(code: string, fallback?: string): string {
  return MESSAGES[code] ?? fallback ?? MESSAGES['unknown_error'];
}
