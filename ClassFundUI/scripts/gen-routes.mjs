// Sinh file route giữ chỗ cho từng feature (chạy một lần khi dựng Foundation): node scripts/gen-routes.mjs
// Mỗi module sau đó tự sở hữu file routes của mình và thay comingSoon(...) bằng màn hình thật.
import { mkdirSync, writeFileSync } from 'node:fs';

const features = {
  auth: { guest: [['login', 'A-01', 'Đăng nhập'], ['register', 'A-02', 'Đăng ký thủ quỹ']], changePassword: [['', 'A-03', 'Đổi mật khẩu']] },
  account: { ACCOUNT: [['', 'A-04', 'Tài khoản của tôi']] },
  funds: { FUND_LIST: [['', 'F-01', 'Quỹ của tôi'], ['new', 'F-02', 'Tạo quỹ']], FUND_SETTINGS: [['', 'F-03', 'Thông tin quỹ']] },
  members: { MEMBER: [['', 'M-01', 'Học sinh']] },
  treasurers: { TREASURER: [['', 'T-01', 'Thủ quỹ']] },
  categories: { CATEGORY: [['', 'C-01', 'Danh mục thu chi']] },
  charges: {
    CHARGE: [['', 'H-01', 'Khoản thu chung'], ['new', 'H-02', 'Tạo khoản thu chung']],
    MY_PAYMENT: [['', 'H-03', 'Khoản cần đóng của tôi']],
    PAYMENT_CONFIRM: [['', 'H-04', 'Xác nhận thanh toán']],
  },
  ledger: { LEDGER: [['', 'L-01', 'Thu chi chung'], ['new', 'L-02', 'Ghi giao dịch'], [':entryId', 'L-03', 'Chi tiết giao dịch']] },
  events: { EVENT: [['', 'E-01', 'Sự kiện'], ['new', 'E-02', 'Tạo sự kiện'], [':eventId', 'E-03', 'Chi tiết sự kiện'], [':eventId/settlement', 'E-04', 'Quyết toán sự kiện']] },
  balance: { BALANCE: [['', 'B-01', 'Số dư cá nhân']], WITHDRAWAL: [['', 'B-02', 'Yêu cầu rút tiền']] },
  notifications: { NOTIFICATION: [['', 'N-01', 'Thông báo']] },
  audit: { AUDIT: [['', 'U-01', 'Nhật ký hoạt động']] },
  overview: { OVERVIEW: [['', 'S-01', 'Tổng quan quỹ chung']] },
  reports: { REPORT: [['', 'S-02', 'Báo cáo thu chi chung'], ['events/:eventId', 'S-03', 'Tổng quan sự kiện'], ['students', 'S-04', 'Báo cáo học sinh'], ['year-end', 'S-05', 'Tổng kết cuối năm']] },
};

const camel = (s) => s.toLowerCase().replace(/_(.)/g, (_, c) => c.toUpperCase());

for (const [feature, groups] of Object.entries(features)) {
  mkdirSync(`src/app/features/${feature}`, { recursive: true });
  let out = `import { Routes } from '@angular/router';\n\nimport { comingSoon } from '../../shared/components/coming-soon/coming-soon';\n`;
  for (const [group, screens] of Object.entries(groups)) {
    const name = feature === 'auth' ? (group === 'guest' ? 'GUEST_ROUTES' : 'CHANGE_PASSWORD_ROUTES') : `${group}_ROUTES`;
    const lines = screens.map(([path, code, title]) => `  { path: '${path}', ...comingSoon('${code}', '${title}') },`);
    out += `\n/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */\nexport const ${name}: Routes = [\n${lines.join('\n')}\n];\n`;
  }
  writeFileSync(`src/app/features/${feature}/${feature}.routes.ts`, out);
}
console.log('done', Object.keys(features).length);
