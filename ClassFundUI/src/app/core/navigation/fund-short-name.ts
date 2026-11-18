/** Mã ngắn trên logo thẻ quỹ: "Lớp 10A1" → "10A1", "CLB Tiếng Anh" → "CTA". */
export function fundShortName(name: string): string {
  // Bỏ phần chú thích trong ngoặc, ví dụ "Lớp 10A1 (demo)".
  const words = name.replace(/\([^)]*\)/g, ' ').trim().split(/\s+/).filter(Boolean);
  const last = words[words.length - 1] ?? '';
  if (words.length > 1 && /\d/.test(last)) {
    return last.slice(0, 4).toUpperCase();
  }
  return words
    .map((word) => word.charAt(0))
    .join('')
    .slice(0, 3)
    .toUpperCase();
}
