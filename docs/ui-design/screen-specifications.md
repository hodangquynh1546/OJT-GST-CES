# Đặc tả màn hình

> v4.0 · 2026-11-07 · **Desktop Web only**. Mỗi mã khớp artboard trên Canvas (xem [ui-design-overview.md](ui-design-overview.md) mục 2) và route ở [sitemap-and-navigation.md](sitemap-and-navigation.md). API tính từ `/api/v1`; trường hiển thị lấy đúng tên trong DTO backend. Cột "Khác theo vai trò" ghi phần thay đổi; phần còn lại dùng chung. Token / Kendo: [design-system.md](design-system.md).

## 1. Đăng ký, đăng nhập, tài khoản

| Mã | Thành phần | Hành động & quy tắc | Trạng thái / lỗi | API |
|---|---|---|---|---|
| A-01 Đăng ký giáo viên | Họ tên*, Email*, Mật khẩu*, Xác nhận*; ghi chú "tài khoản luôn là Giáo viên"; link Đăng nhập | Thành công → đăng nhập luôn → G-01 trống ("Tạo quỹ đầu tiên") | 400 xác nhận không khớp; 422 `auth.password_policy` (8–128, chữ + số); 422 `user.email_taken` | `POST /auth/register-teacher` |
| A-02 Đăng nhập | **Một form chung**: "Email hoặc tên đăng nhập", Mật khẩu, Đăng nhập; link "Đăng ký tài khoản giáo viên"; ghi chú học sinh / phụ huynh do GVCN cấp. **Không có lựa chọn vai trò** | Điều hướng theo `/auth/me` (xem sitemap) | Loading (nút khóa); sai → "Email / tên đăng nhập hoặc mật khẩu không đúng"; mật khẩu đúng nhưng bị khóa → "liên hệ giáo viên chủ nhiệm"; quá số lần → chờ 1 phút | `POST /auth/login { login, password }` |
| A-03 Đặt mật khẩu mới | Mật khẩu tạm, mới, nhập lại; thông báo "mật khẩu tạm do GVCN cấp"; Đăng xuất | Lưu → vào trang chính theo vai trò | 422 sai mật khẩu tạm / không đạt chính sách / trùng mật khẩu cũ | `POST /auth/change-password` |
| A-04 Tài khoản | Họ tên (Sửa họ tên), Email hoặc Tên đăng nhập, Loại tài khoản, Quỹ / Các quỹ / Con được liên kết; Đổi mật khẩu; Đăng xuất | Đổi mật khẩu → phiên khác bị đăng xuất | Khác theo vai trò: ghi chú quyền (thủ quỹ do GVCN phân công; phụ huynh không có quyền thủ quỹ) | `GET/PUT /auth/me`, `POST /auth/change-password` |

## 2. GVCN

| Mã | Thành phần | Hành động & quy tắc | Trạng thái / lỗi | API |
|---|---|---|---|---|
| G-01 Quỹ của tôi | Lưới thẻ quỹ: `name`, `currentBalance`, nhãn theo `treasurerStatus` ("Chưa phân công thủ quỹ" / "Đã có thủ quỹ"), `studentCount` học sinh, `treasurerNames`, `createdAt` (Ngày tạo); "+ Tạo quỹ" (dialog: Tên quỹ*) | Quỹ chưa phân công → mở G-03; quỹ có thủ quỹ → S-01 | Rỗng sau đăng ký; tạo thành công → thông báo "thêm học sinh rồi phân công thủ quỹ" | `GET/POST /funds` |
| G-01 Quỹ của con (phụ huynh) | Thẻ quỹ: `name`, `currentBalance`, "Con: " + `linkedStudents`, `treasurerNames`. **Không** có tên GVCN (API không trả) | Mở bản công khai | Ghi chú "con là thủ quỹ không làm phụ huynh có quyền thủ quỹ" | `GET /funds` |
| G-02 Học sinh & phụ huynh | Bảng: họ tên + tên đăng nhập, Tài khoản (Chưa cấp / Chờ kích hoạt / Đang hoạt động / Bị khóa), Vai trò (Học sinh / Thủ quỹ), Phụ huynh; tìm; lọc (Tất cả, Thủ quỹ, Chưa kích hoạt, Chưa có phụ huynh); "+ Thêm học sinh" | Dialog thêm: Họ tên*, Email phụ huynh, Họ tên phụ huynh. Drawer chi tiết: sửa họ tên; **Cấp tài khoản** (tên đăng nhập gợi ý); **Cấp lại mật khẩu tạm** (xác nhận); **Liên kết / Gỡ liên kết phụ huynh** (xác nhận); link G-03 | Hộp thoại **mật khẩu tạm một lần** (tên đăng nhập / email + mật khẩu tạm; tick xác nhận mới đóng; "không gửi email"). Email phụ huynh đã có → "Đã liên kết với tài khoản hiện có" (không cấp mật khẩu); email giáo viên / học sinh → lỗi; đã có phụ huynh → lỗi; tên đăng nhập trùng / sai định dạng | `/funds/{id}/students…` |
| G-03 Phân công thủ quỹ | "Đang là thủ quỹ (n)": họ tên, thời điểm + người phân công, Thu hồi; hướng dẫn đổi thủ quỹ; "Lịch sử phân công" (học sinh, phân công, thu hồi, trạng thái); "+ Phân công thủ quỹ" (dialog radio, chỉ học sinh đã kích hoạt chọn được) | Thu hồi có xác nhận; đổi người = phân công mới trước, thu hồi sau | Thủ quỹ cuối: nút khóa + "phân công người mới trước"; quỹ chưa có thủ quỹ: banner vàng; chưa chọn học sinh → lỗi | `/funds/{id}/treasurers…` |
| G-04 Thông tin quỹ | Tên quỹ*; thông tin từ `GET /funds` (mục của quỹ đang mở): Ngày tạo (`createdAt`), GVCN (bạn), Thủ quỹ (`treasurerNames`), `studentCount` học sinh + link G-02 / G-03; ghi chú "Danh mục do thủ quỹ quản lý" | Lưu, Hoàn tác | 400 tên trống / > 100 | `PUT /funds/{id}` |

## 3. Thu chi (dùng chung, theo vai trò)

| Mã | Thành phần | Khác theo vai trò | Trạng thái | API |
|---|---|---|---|---|
| S-01 Tổng quan | KPI: `currentBalance`, `totalIncome`, `totalExpense`, `currentMonth` (`income`, `expense`, `net`); giao dịch gần đây (`recentTransactions`, 5); biểu đồ "Thu chi 3 tháng gần nhất" (cột Thu / Chi, dữ liệu `GET …/reports/yearly?year=` lấy 3 tháng cuối tới tháng hiện tại; đầu năm thì gọi thêm năm trước); Thông tin quỹ: GVCN, thủ quỹ từ `managers` (họ tên + vai trò), chênh lệch tháng | GVCN: banner quản lý lớp + nút Học sinh / Phân công, link Đổi tên quỹ. Thủ quỹ: + Thêm khoản thu / chi, link Danh mục / Thành viên. Học sinh / phụ huynh: ghi chú bản công khai, chỉ khoản công khai | Loading, Empty, Error, số dư âm, **Chưa phân công thủ quỹ** | `GET /funds/{id}` |
| S-02 Thu chi | Bộ lọc: `type`, `categoryId`, `from` – `to` (mặc định tháng hiện tại), `search`, `status` (Active mặc định / Cancelled / All); Kendo Grid phân trang server (`page`, `pageSize` 20); panel chi tiết bên phải | GVCN, thủ quỹ: ghi chú, người ghi / sửa / hủy, lý do hủy, lịch sử thay đổi; thủ quỹ thêm Sửa / Hủy. Học sinh, phụ huynh: chỉ khoản công khai, đã hủy chỉ nhãn + ngày, ô "Khoản khác" | Empty theo bộ lọc, 404 khoản không công khai, 409 | `/funds/{id}/transactions…` |
| S-03 Báo cáo | Tab Tháng (`monthly`): `openingBalance`, `totalIncome`, `totalExpense`, `netChange`, `closingBalance`, `byCategory`, `transactions`; Tab Năm (`yearly`): 12 dòng tháng cùng 5 cột | Học sinh, phụ huynh: dòng "Khoản khác" theo loại, không số lượng | Tháng không có giao dịch | `/funds/{id}/reports/*` |
| T-01 Dialog giao dịch (thủ quỹ) | Thêm: loại Thu / Chi, nội dung*, số tiền*, ngày*, danh mục*, ghi chú nội bộ, "Công khai cho học sinh và phụ huynh" (mặc định bật; tắt → cảnh báo P1). Sửa: không đổi loại. Hủy: tóm tắt, lý do* | Chỉ thủ quỹ | Lỗi từng ô (`transaction.future_date`, `transaction.category_invalid`, `transaction.cancel_reason_required`); `isNegativeBalance` → cảnh báo vàng; chống lưu trùng (`replayed`); 409 `transaction.cancelled` | `POST/PUT …/transactions`, `…/cancel` |
| T-02 Danh mục (thủ quỹ) | 2 nhóm Thu / Chi; đổi tên, Ẩn / Hiện lại, thêm | Chỉ thủ quỹ | 422 trùng tên | `/funds/{id}/categories` |
| T-03 Thành viên (thủ quỹ) | Lọc GVCN / Thủ quỹ / Học sinh; dòng: họ tên + vai trò | Không email, tên đăng nhập, liên hệ, không thao tác | – | `GET /funds/{id}/members` |

## 4. Thành phần chung
| Thành phần | Nội dung |
|---|---|
| AppHeader | Logo; tên quỹ (GVCN / phụ huynh: "Đổi quỹ ▾" về G-01); nhãn vai trò; avatar → A-04 đúng vai trò |
| AppNav | Sidebar 232 px cố định, luôn hiện nhãn; menu theo permission (sitemap mục 2); chân menu mô tả quyền |
| AppState | Loading, Empty (thủ quỹ / người xem), Empty lọc, Success, Error tải, Error form, Warning số dư âm, Warning không công khai, Quyền thay đổi (403 khi đang mở), 403 trang, **Mật khẩu tạm (403 `password.change_required`)**, **Quỹ chưa phân công thủ quỹ**, 404, 401, bị khóa, 409 |

Định dạng: tiền `#.###.### đ` căn phải, chữ số tabular, dấu +/−; ngày `dd/MM/yyyy`; giới hạn: nội dung 200, ghi chú 1000, lý do hủy 500, tên quỹ / danh mục / họ tên học sinh 100, họ tên tài khoản 200, tên đăng nhập 3–60 (`a-z 0-9 . _ -`).


## 5. Mã lỗi backend cần hiển thị
| Mã | Màn hình | Thông điệp gợi ý |
|---|---|---|
| `auth.invalid_credentials` | A-02 | Email / tên đăng nhập hoặc mật khẩu không đúng |
| `auth.account_disabled` | A-02 | Tài khoản đã bị khóa – liên hệ giáo viên chủ nhiệm |
| `auth.rate_limited` | A-02 | Đăng nhập sai nhiều lần, thử lại sau 1 phút |
| `auth.password_policy` / `auth.password_reused` / `auth.wrong_password` | A-01, A-03, A-04 | Gắn vào ô mật khẩu tương ứng |
| `password.change_required` (403) | mọi trang | Chuyển tới A-03 |
| `student.account_exists` / `student.no_account` | G-02 | Đã có tài khoản / chưa cấp tài khoản |
| `parent.email_conflict` / `parent.email_in_use` | G-02 | Email thuộc giáo viên / học sinh; học sinh đã có phụ huynh |
| `treasurer.no_account` / `treasurer.account_not_activated` / `treasurer.account_disabled` / `treasurer.already_assigned` | G-03 | Học sinh không chọn được (lý do cạnh tên) |
| `treasurer.last` / `treasurer.already_revoked` | G-03 | Phân công người mới trước / đã thu hồi |
| `category.name_taken` | T-02 | Tên danh mục đã có |
| `transaction.*` | T-01 | Xem dòng T-01 |
