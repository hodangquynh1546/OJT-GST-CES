/** Thông báo theo code trong ProblemDetails; không hiện thông tin kỹ thuật (AppState trên Canvas). */
const MESSAGES: Record<string, string> = {
  network_error: 'Không kết nối được máy chủ. Dữ liệu của bạn không bị thay đổi.',
  unknown_error: 'Đã có lỗi xảy ra. Vui lòng thử lại.',
  internal_error: 'Đã có lỗi xảy ra. Vui lòng thử lại.',
  unauthorized: 'Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.',
  forbidden: 'Bạn không có quyền thực hiện thao tác này.',
  not_found: 'Không tìm thấy dữ liệu.',
  'password.change_required': 'Bạn cần đặt mật khẩu mới trước khi tiếp tục.',
};

export function messageForCode(code: string, fallback?: string): string {
  return MESSAGES[code] ?? fallback ?? MESSAGES['unknown_error'];
}
