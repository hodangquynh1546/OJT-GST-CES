import { isValidEmail } from '../auth/auth-validators';

/** Tách danh sách email từ ô dán: phân cách bằng xuống dòng, dấu phẩy, chấm phẩy hoặc khoảng trắng. */
export function parseEmails(text: string): string[] {
  return text
    .split(/[\s,;]+/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

/** Bỏ email trùng (không phân biệt hoa thường), giữ thứ tự xuất hiện đầu tiên. */
export function uniqueEmails(emails: readonly string[]): string[] {
  const seen = new Set<string>();
  return emails.filter((email) => {
    const key = email.trim().toLowerCase();
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

export function countInvalidEmails(emails: readonly string[]): number {
  return emails.filter((email) => !isValidEmail(email)).length;
}

/** Nội dung sao chép cho thủ quỹ gửi học sinh: mỗi dòng "email<TAB>mật khẩu". */
export function credentialsText(rows: readonly { email: string; password: string }[]): string {
  return rows.map((row) => `${row.email}\t${row.password}`).join('\n');
}
