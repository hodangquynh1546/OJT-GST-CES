/**
 * Nhãn tiếng Việt cho mã hành động audit (`<module>.<hành động>`, xem `*AuditActions.cs`).
 * Module mới thêm mã audit thì bổ sung nhãn ở đây; mã chưa có nhãn hiện nguyên mã.
 */
export const AUDIT_ACTION_LABEL: Readonly<Record<string, string>> = {
  // M1 – Tài khoản
  'auth.register': 'Đăng ký tài khoản',
  'auth.change_password': 'Đổi mật khẩu',
  'auth.update_profile': 'Sửa thông tin cá nhân',
  // M2 – Quỹ
  'fund.create': 'Tạo quỹ',
  'fund.update': 'Sửa thông tin quỹ',
  // M3 – Học sinh
  'member.add': 'Thêm học sinh',
  'member.reactivate': 'Thêm lại học sinh',
  'member.update': 'Sửa tên học sinh',
  'member.reset_password': 'Cấp lại mật khẩu tạm',
  'member.remove': 'Ngừng học sinh',
  // M4 – Thủ quỹ
  'treasurer.grant': 'Phân quyền thủ quỹ',
  'treasurer.revoke': 'Thu hồi quyền thủ quỹ',
  // M5 – Danh mục & file
  'category.create': 'Thêm danh mục',
  'category.update': 'Sửa danh mục',
  'attachment.upload': 'Tải lên chứng từ',
  'attachment.cleanup': 'Dọn file chưa dùng',
  'user.avatar_update': 'Đổi ảnh đại diện',
  // M7 – Thu chi
  'ledger.create': 'Ghi khoản chi',
  'ledger.reverse': 'Tạo bút toán đảo',
  // M8 – Sự kiện
  'event.create': 'Tạo sự kiện',
  'event.update': 'Sửa sự kiện',
  'event.cancel': 'Hủy sự kiện',
  'event.complete': 'Kết thúc sự kiện',
  'event.respond': 'Trả lời tham gia sự kiện',
  'event.invite': 'Mời vào sự kiện',
};

export function actionLabel(action: string): string {
  return AUDIT_ACTION_LABEL[action] ?? action;
}

const vietnamDateTime = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Ho_Chi_Minh',
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

/** Thời điểm UTC → "17/11 09:40" giờ Việt Nam (cột "Thời điểm" của U-01). */
export function formatAuditTime(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return '';
  }
  const parts = Object.fromEntries(vietnamDateTime.formatToParts(parsed).map((part) => [part.type, part.value]));
  return `${parts['day']}/${parts['month']} ${parts['hour']}:${parts['minute']}`;
}

/**
 * Nhãn tiếng Việt cho trường trong Before/After (khóa không phân biệt hoa thường).
 * Module mới ghi trường mới vào audit thì bổ sung nhãn ở đây; trường chưa có nhãn hiện nguyên tên.
 */
export const AUDIT_FIELD_LABEL: Readonly<Record<string, string>> = {
  name: 'Tên',
  fullname: 'Họ tên',
  email: 'Email',
  schoolyear: 'Năm học',
  isactive: 'Trạng thái',
  type: 'Loại',
  kind: 'Loại file',
  filename: 'Tên file',
  contenttype: 'Định dạng',
  sizebytes: 'Dung lượng',
  count: 'Số lượng',
  role: 'Vai trò',
  status: 'Trạng thái',
  outcome: 'Kết quả',
  cancelledassignments: 'Khoản thu chưa đóng đã hủy',
  mustchangepassword: 'Mật khẩu',
  avatarattachmentid: 'Ảnh đại diện',
  defaultcategories: 'Danh mục mặc định',
  title: 'Tiêu đề',
  eventname: 'Sự kiện',
  chargetitle: 'Khoản thu',
  payername: 'Người nộp',
  amount: 'Số tiền',
  amountdue: 'Số tiền phải đóng',
  amountperperson: 'Số tiền mỗi người',
  total: 'Tổng',
  surplus: 'Tiền thừa',
  balance: 'Số dư',
  method: 'Phương thức',
  mode: 'Cách xác nhận',
  response: 'Trả lời',
  reason: 'Lý do',
  reversedby: 'Bút toán đảo',
  duedate: 'Hạn đóng',
  note: 'Ghi chú',
  // M7 – Thu chi
  category: 'Danh mục',
  purpose: 'Mục đích',
  transactiondate: 'Ngày giao dịch',
  attachmentcount: 'Số chứng từ',
  // M8 – Sự kiện
  eventdate: 'Ngày diễn ra',
  description: 'Mô tả',
  invitedcount: 'Số người được mời',
  participantcount: 'Số người tham gia',
  excludedcount: 'Số người bị loại',
  remindedcount: 'Số người được nhắc đóng',
  excluded: 'Người bị loại',
  cancelledcharges: 'Khoản thu đã hủy',
};

/** Giá trị enum từ backend → tiếng Việt. */
const AUDIT_VALUE_LABEL: Readonly<Record<string, string>> = {
  Owner: 'Thủ quỹ chính',
  Treasurer: 'Thủ quỹ',
  Student: 'Học sinh',
  Income: 'Thu',
  Expense: 'Chi',
  Receipt: 'Hóa đơn',
  PaymentProof: 'Minh chứng thanh toán',
  Document: 'Tài liệu',
  Avatar: 'Ảnh đại diện',
  Cash: 'Tiền mặt',
  BankTransfer: 'Chuyển khoản',
  PersonalBalance: 'Số dư cá nhân',
  Accepted: 'Tham gia',
  Declined: 'Không tham gia',
  Pending: 'Chưa trả lời',
  Active: 'Đang hoạt động',
  Removed: 'Đã ngừng',
  Created: 'Tạo tài khoản mới',
  ExistingAdded: 'Thêm tài khoản có sẵn',
  Reactivated: 'Thêm lại',
  AlreadyExists: 'Đã là thành viên',
  Submitted: 'Chờ xác nhận',
  Confirmed: 'Đã xác nhận',
  Rejected: 'Bị từ chối',
  Unpaid: 'Chưa đóng',
  Paid: 'Đã đóng',
  Open: 'Đang mở',
  Closed: 'Đã đóng',
  Completed: 'Đã kết thúc',
  Settled: 'Đã quyết toán',
  Cancelled: 'Đã hủy',
  Verified: 'Đã đối chiếu',
  SelfDeclared: 'Tự ghi nhận',
};

const MONEY_FIELDS = new Set(['amount', 'amountdue', 'amountperperson', 'total', 'surplus', 'balance']);
const CONTENT_TYPE_LABEL: Readonly<Record<string, string>> = {
  'image/jpeg': 'Ảnh JPG',
  'image/png': 'Ảnh PNG',
  'image/webp': 'Ảnh WEBP',
  'application/pdf': 'PDF',
};
const vnNumber = new Intl.NumberFormat('vi-VN');
/** Ngày nghiệp vụ `yyyy-MM-dd` (ngày diễn ra, ngày giao dịch, hạn đóng) → `dd/MM/yyyy`. */
const ISO_DATE = /^(\d{4})-(\d{2})-(\d{2})$/;

/** Một dòng của bảng "Trước / sau". */
export interface AuditChange {
  label: string;
  before: string;
  after: string;
  changed: boolean;
}

/** Kết quả so sánh: có cột Trước / Sau hay không (tạo mới thì không có "Trước"). */
export interface AuditChanges {
  hasBefore: boolean;
  hasAfter: boolean;
  rows: AuditChange[];
}

/**
 * Before/After (JSON) → bảng tiếng Việt: mỗi trường một dòng, nhãn dễ đọc, giá trị đã dịch
 * (vai trò, trạng thái, tiền "650.000 đ", dung lượng…). Ẩn mã định danh kỹ thuật (`id`, `…Id`).
 */
export function auditChanges(beforeJson: string | null | undefined, afterJson: string | null | undefined): AuditChanges {
  const before = parseObject(beforeJson);
  const after = parseObject(afterJson);
  const keys = new Map<string, string>();
  for (const source of [before, after]) {
    for (const key of Object.keys(source ?? {})) {
      const normalized = key.toLowerCase();
      if (!keys.has(normalized) && !isTechnicalKey(key)) {
        keys.set(normalized, key);
      }
    }
  }

  const rows = [...keys.keys()].map((key) => {
    const oldValue = pick(before, key);
    const newValue = pick(after, key);
    const changed = before !== null && after !== null && JSON.stringify(oldValue ?? null) !== JSON.stringify(newValue ?? null);
    return {
      label: AUDIT_FIELD_LABEL[key] ?? keys.get(key) ?? key,
      before: formatField(key, oldValue, 'before', changed),
      after: formatField(key, newValue, 'after', changed),
      changed,
    };
  });
  return { hasBefore: before !== null, hasAfter: after !== null, rows };
}

function parseObject(value: string | null | undefined): Record<string, unknown> | null {
  if (!value || !value.trim()) {
    return null;
  }
  try {
    const parsed: unknown = JSON.parse(value);
    if (parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
    return { 'Nội dung': parsed };
  } catch {
    return { 'Nội dung': value };
  }
}

function pick(source: Record<string, unknown> | null, key: string): unknown {
  if (!source) {
    return undefined;
  }
  const match = Object.keys(source).find((candidate) => candidate.toLowerCase() === key);
  return match === undefined ? undefined : source[match];
}

/** Mã định danh (`id`, `ids`, `userId`, `EventIds`…) không có ý nghĩa với người đọc; trừ trường đã có nhãn. */
function isTechnicalKey(key: string): boolean {
  return (/^ids?$/i.test(key) || /(Id|ID|_id)s?$/.test(key)) && !(key.toLowerCase() in AUDIT_FIELD_LABEL);
}

function formatField(key: string, value: unknown, side: 'before' | 'after', changed: boolean): string {
  if (value === null || value === undefined || value === '') {
    return key === 'avatarattachmentid' ? 'Chưa có ảnh' : '–';
  }
  switch (key) {
    case 'isactive':
      return value === true ? 'Đang dùng' : 'Ngừng dùng';
    case 'mustchangepassword':
      return value === true ? 'Mật khẩu tạm (phải đổi khi đăng nhập)' : 'Đã đổi mật khẩu';
    case 'avatarattachmentid':
      return changed ? (side === 'before' ? 'Ảnh cũ' : 'Ảnh mới') : 'Có ảnh';
    case 'sizebytes':
      return typeof value === 'number' ? formatSize(value) : String(value);
    case 'contenttype':
      return CONTENT_TYPE_LABEL[String(value)] ?? String(value);
  }
  if (typeof value === 'number') {
    return MONEY_FIELDS.has(key) ? `${vnNumber.format(value)} đ` : vnNumber.format(value);
  }
  if (typeof value === 'boolean') {
    return value ? 'Có' : 'Không';
  }
  if (typeof value === 'string') {
    const date = ISO_DATE.exec(value);
    if (date && key.endsWith('date')) {
      return `${date[3]}/${date[2]}/${date[1]}`;
    }
    return AUDIT_VALUE_LABEL[value] ?? value;
  }
  if (Array.isArray(value)) {
    return value.map((item) => (typeof item === 'string' ? (AUDIT_VALUE_LABEL[item] ?? item) : JSON.stringify(item))).join(', ');
  }
  return JSON.stringify(value);
}

function formatSize(bytes: number): string {
  if (bytes >= 1024 * 1024) {
    return `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 }).format(bytes / (1024 * 1024))} MB`;
  }
  return `${vnNumber.format(Math.max(1, Math.round(bytes / 1024)))} KB`;
}

/** Ngày (giờ địa phương của máy, là ngày người dùng chọn) → "yyyy-MM-dd" cho API. */
export function toApiDate(value: Date | null | undefined): string | null {
  if (!value) {
    return null;
  }
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${value.getFullYear()}-${month}-${day}`;
}

/** Mặc định bộ lọc như canvas: từ ngày 1 của tháng tới hôm nay. */
export function defaultRange(today = new Date()): { from: Date; to: Date } {
  return { from: new Date(today.getFullYear(), today.getMonth(), 1), to: new Date(today.getFullYear(), today.getMonth(), today.getDate()) };
}
