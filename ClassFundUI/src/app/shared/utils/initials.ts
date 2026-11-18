/** Chữ cái đầu của hai từ cuối (tên Việt: "Nguyễn Minh Anh" → "MA"). */
export function initialsOf(fullName: string | null | undefined): string {
  return (fullName ?? '')
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(-2)
    .map((word) => word.charAt(0))
    .join('')
    .toUpperCase();
}
