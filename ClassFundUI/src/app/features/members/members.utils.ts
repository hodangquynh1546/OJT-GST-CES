import { isValidEmail } from '../auth/auth-validators';

/** Một dòng khi thêm nhiều học sinh: email ở đầu dòng, phần còn lại là họ tên (có thể trống). */
export interface StudentLine {
  email: string;
  fullName: string;
}

/**
 * Tách ô dán thành từng học sinh: mỗi dòng "email Họ tên" (ngăn cách bằng khoảng trắng, tab, dấu phẩy hoặc chấm phẩy).
 * Ví dụ `hodangquynh2k4@gmail.com Hồ Đăng Quỳnh`. Dòng trống bị bỏ.
 */
export function parseStudentLines(text: string): StudentLine[] {
  return text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .map((line) => {
      const match = /^([^\s,;]+)[\s,;]*(.*)$/.exec(line);
      return { email: match?.[1] ?? line, fullName: (match?.[2] ?? '').trim() };
    });
}

/** Bỏ dòng trùng email (không phân biệt hoa thường), giữ dòng xuất hiện đầu tiên. */
export function uniqueStudents(lines: readonly StudentLine[]): StudentLine[] {
  const seen = new Set<string>();
  return lines.filter((line) => {
    const key = line.email.trim().toLowerCase();
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

export function countInvalidStudents(lines: readonly StudentLine[]): number {
  return lines.filter((line) => !isValidEmail(line.email)).length;
}

/** Nội dung sao chép cho thủ quỹ gửi học sinh: mỗi dòng "email<TAB>mật khẩu". */
export function credentialsText(rows: readonly { email: string; password: string }[]): string {
  return rows.map((row) => `${row.email}\t${row.password}`).join('\n');
}
