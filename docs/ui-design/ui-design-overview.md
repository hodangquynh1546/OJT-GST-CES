# UI/UX – Tổng quan

> Phiên bản: **4.0 · 2026-11-07** · **Desktop Web only** · Mô hình **Giáo viên · Học sinh · Phụ huynh** · Angular 20.3 + Kendo UI for Angular 23.3.0 (Theme Default, tùy biến bằng token `--cf-*`).
> Thiết kế chuẩn (Source of Truth cho UI): Design Canvas **"ClassFund – UI/UX v4.0 Desktop Web"** – https://claude.ai/artifact/D5JVqKkBFqQhmcp5spfw6Y (version `1794024869-668f`).
> Trạng thái triển khai: **Backend đã có** Auth, Funds, Students, Treasurers, Categories, Transactions, Overview, Reports, Members. **Frontend**: mới có shell (header, nav, guard, route); màn hình nghiệp vụ chưa làm. Xem [implementation-status.md](../implementation/implementation-status.md).
> **v4.0 thay thế toàn bộ v3.x.** Phạm vi Mobile App / Mobile Responsive **đã bỏ** (D-36). Mọi nhắc tới "Mobile", "360 / 390 / 430", "thanh dưới", "nút nổi (FAB)", "bottom sheet", "sidebar icon 768–1023" trong Git history là lỗi thời.

## 0. Cách đọc (dành cho dev / AI triển khai Frontend)

1. **Chỉ một phiên bản: v4.0.** Canvas và các file trong `docs/ui-design/` cùng là v4.0. Nếu thấy mâu thuẫn: **API backend** (`ClassFundAPI`, DTO thật) > **Canvas** > **tài liệu này**. Báo lại mâu thuẫn, không tự đoán.
2. Thứ tự đọc: file này → [design-system.md](design-system.md) → [sitemap-and-navigation.md](sitemap-and-navigation.md) → [screen-specifications.md](screen-specifications.md) → [user-flows.md](user-flows.md) → [wireframes.md](wireframes.md).
3. **Mã màn hình** (A-, G-, S-, T-, X-, DS-) giống nhau ở Canvas, tài liệu, và nên dùng làm tên thư mục / component Angular trong `src/app/features/`.
4. Board `XxxTeacher` / `XxxStudent` / `XxxParent` là **cùng một màn hình** (`userRole` khác nhau) → **một** component Angular, hiển thị theo `permissions` của quỹ đang mở.
5. Dữ liệu trên Canvas là **dữ liệu mẫu**. Trường hiển thị phải có trong DTO backend; trường không có trong API thì không hiển thị.
6. Màu, chữ, khoảng cách: chỉ dùng token `--cf-*`; thành phần: dùng Kendo theo bảng ánh xạ ở [design-system.md](design-system.md).

## 1. Vai trò trên giao diện

| Vai trò | Màu nhận diện | Có tài khoản bằng | Màn hình chính |
|---|---|---|---|
| GVCN (giáo viên) | tím `#5b2a86` | Tự đăng ký (A-01) | G-01 Quỹ của tôi, G-02 Học sinh & phụ huynh, G-03 Phân công thủ quỹ, G-04 Thông tin quỹ, S-01 → S-03 xem đầy đủ (không ghi) |
| Thủ quỹ (học sinh được phân công) | xanh `#1d5fa8` | GVCN cấp | S-01 → S-03 + T-01 thêm / sửa / hủy, T-02 Danh mục, T-03 Thành viên (chỉ xem) |
| Học sinh | nâu `#7a4b00` | GVCN cấp | S-01 → S-03 bản công khai |
| Phụ huynh | xanh lá `#1e5c33` | GVCN liên kết email | G-01 Quỹ của con, S-01 → S-03 bản công khai |

## 2. Cấu trúc Canvas v4.0 (37 artboard, 8 trang)

| Trang | Artboard |
|---|---|
| 00 · Đọc trước | D-01 `Main` (cách đọc, vai trò, ma trận quyền, **danh mục màn hình có route + API**, quyết định, trạng thái) · D-02 `Sitemap` (4 tab vai trò, cột API) · D-03 `Flows` (13 luồng) |
| 01 · Design System v4 | DS-01 `DSFoundations` (màu, chữ, khoảng cách, bo góc, đổ bóng, layout, icon, trợ năng) · DS-02 `DSComponents` (nút, form, tag, thông báo, KPI, bảng, ánh xạ Kendo) |
| 02 · Thành phần ghép & trạng thái | `AppHeader` · `AppNav` (sidebar 232 px) · `UiSelect` · `States` · `TxDialog` |
| 03 · Đăng ký · Đăng nhập · Tài khoản | A-01 `TeacherRegister` · A-02 `Login` · A-03 `ChangePasswordRequired` · A-04 `AccountTeacher`, `Account` (thủ quỹ), `AccountStudent`, `AccountParent` |
| 04 · GVCN | G-01 `FundList` · S-01 `OverviewTeacher` · G-02 `Students` · G-03 `Treasurers` · G-04 `FundSettingsTeacher` · S-02 `LedgerTeacher` · S-03 `ReportsTeacher` |
| 05 · Học sinh – Thủ quỹ | S-01 `Overview` · S-02 `Ledger` · S-03 `Reports` · T-01 `TransactionForm` · T-02 `FundSettings` (danh mục) · T-03 `Members` |
| 06 · Học sinh (công khai) | `OverviewStudent` · `LedgerStudent` · `ReportsStudent` |
| 07 · Phụ huynh (công khai) | G-01 `FundListParent` · `OverviewParent` · `LedgerParent` · `ReportsParent` |

## 3. Nguyên tắc
1. **Desktop Web only**: khung chuẩn 1440 × 900, tối thiểu **1280 px** (`body { min-width: 1280px }`; hẹp hơn thì cuộn ngang). Không có breakpoint / `@media` responsive. Header 56 px + sidebar 232 px cố định.
2. **Đăng nhập không chọn vai trò**: một ô "Email hoặc tên đăng nhập" + mật khẩu. Sau đăng nhập, điều hướng theo `/auth/me`. Chỉ giáo viên có link đăng ký; học sinh, phụ huynh do GVCN cấp tài khoản.
3. **Vai trò theo từng quỹ, do hệ thống xác định**. Giao diện không gửi vai trò; chỉ vẽ nút theo `permissions` của quỹ đang mở.
4. **Không có quyền thì không vẽ nút**. Backend vẫn chặn (403 / 404). 403 lúc trang đang mở → trạng thái "Quyền của bạn vừa thay đổi".
5. **Nhãn vai trò luôn hiện** trên header: "GVCN" / "Thủ quỹ" / "Học sinh · Công khai" / "Phụ huynh · Công khai". GVCN và phụ huynh có "Đổi quỹ".
6. **GVCN không ghi thu chi** và không quản lý danh mục; quản lý học sinh, phụ huynh, phân công thủ quỹ, tên quỹ.
7. **Mật khẩu tạm hiển thị một lần**: hộp thoại phải tick "Tôi đã lưu / chuyển thông tin này" mới đóng được; luôn ghi "Hệ thống không gửi email".
8. **Thủ quỹ cuối cùng**: nút "Thu hồi" bị khóa kèm giải thích "phân công người mới trước" (`treasurer.last`). Quỹ chưa có thủ quỹ có banner vàng "Chưa phân công thủ quỹ".
9. **Ghi một khoản ≤ 3 thao tác** (thủ quỹ): "+ Thêm khoản thu / chi" trên header trang → dialog → "Lưu".
10. **Số dư nổi bật**; số dư âm có cảnh báo vàng, không chặn lưu (`isNegativeBalance`). Thu / chi phân biệt bằng dấu + chữ + màu (+ `#1e7a3c` / − `#b3261e`).
11. **Khoản không công khai**: nhãn "Không công khai" (chỉ GVCN, thủ quỹ thấy). Học sinh, phụ huynh thấy dòng gộp "Khoản khác" không có số lượng; form cảnh báo P1 (số tiền có thể suy ra).
12. **Hủy cần lý do** và xác nhận; không có nút "Xóa" ở bất kỳ đâu.
13. **Mật khẩu**: 8–128 ký tự, có cả chữ và số (đăng ký, đổi mật khẩu, mật khẩu tạm).
14. **Accessibility**: nút / liên kết thật, label cho mọi ô nhập, tương phản ≥ 4.5:1, focus nhìn thấy, `aria-current` cho menu, `role="alert"` cho lỗi, `role="radiogroup"` cho lựa chọn.

## 4. Kendo component chính
Grid (server paging) · Dialog · Drawer (chi tiết học sinh, mở bên phải) · DatePicker · NumericTextBox (VND, `format="n0"`) · DropDownList · TextArea · Switch · Checkbox · RadioButton · TabStrip · Chart · Notification · Loader / Skeleton · Chip. Chi tiết: [design-system.md](design-system.md) mục 6.

## 5. Tài liệu
[design-system.md](design-system.md) · [sitemap-and-navigation.md](sitemap-and-navigation.md) · [screen-specifications.md](screen-specifications.md) · [user-flows.md](user-flows.md) · [wireframes.md](wireframes.md) · [ui-open-questions.md](ui-open-questions.md)

## 6. Lịch sử thay đổi
| Ngày | Phiên bản | Thay đổi |
|---|---|---|
| 2026-11-04 | 2.0 / 2.1 | Thiết kế 3 vai trò Thủ quỹ / GVCN / Phụ huynh (đã thay thế – Git history) |
| 2026-11-05 | 3.0 | Mô hình Giáo viên · Học sinh · Phụ huynh: thêm A-01, G-01 → G-04, màn hình học sinh, mobile (đã bỏ ở v4.0) |
| 2026-11-05 | 3.1 | Đăng nhập không chọn vai trò; trạng thái mật khẩu tạm / quỹ chưa phân công |
| 2026-11-07 | **4.0** | **Bỏ toàn bộ Mobile** (16 artboard, trang 07 Mobile, mọi `@media`); Desktop ≥ 1280 px. Thêm Design System (DS-01, DS-02, [design-system.md](design-system.md)). Đổi tên thành **ClassFund**. Khớp màn hình với API thật: G-01 bỏ "GVCN" ở thẻ phụ huynh và "số phụ huynh đã liên kết" (API chỉ có `studentCount`), thêm "Ngày tạo"; G-04 bỏ "Bắt đầu ghi", số phụ huynh; S-01 bỏ "Bắt đầu ghi", biểu đồ 3 tháng lấy từ `reports/yearly`. D-01 có route + API cho từng màn hình. |
