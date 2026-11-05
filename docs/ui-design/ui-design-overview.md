# UI/UX – Tổng quan

> Phiên bản: **3.1 · 2026-11-05** · Mô hình **Giáo viên · Học sinh · Phụ huynh** (D-24 → D-33) · Angular 20.3 + Kendo UI 23.3.0 (Default Theme, tùy biến màu).
> Thiết kế chuẩn (Source of Truth cho UI): Design Canvas **"Quỹ Lớp – UI/UX Giáo viên · Học sinh · Phụ huynh"** – https://claude.ai/artifact/D5JVqKkBFqQhmcp5spfw6Y (version `1793847333-84a2`).
> Trạng thái triển khai: **Not Implemented** – chưa có frontend; mã nguồn backend / frontend cũ đã xóa ngày 2026-11-05 (xem [implementation-status.md](../implementation/implementation-status.md)). Canvas là prototype tương tác, không gọi API thật.
> Bản 2.x (3 vai trò Thủ quỹ người lớn / GVCN chỉ xem / Phụ huynh) đã bị thay thế, xem Git history.

## 1. Vai trò trên giao diện

| Vai trò | Màu nhận diện | Có tài khoản bằng | Màn hình chính |
|---|---|---|---|
| GVCN (giáo viên) | tím `#5b2a86` | Tự đăng ký (A-01) | G-01 Quỹ của tôi, G-02 Học sinh & phụ huynh, G-03 Phân công thủ quỹ, G-04 Thông tin quỹ, S-01 → S-03 xem đầy đủ (không ghi) |
| Thủ quỹ (học sinh được phân công) | xanh `#1d5fa8` | GVCN cấp | S-01 → S-03 + T-01 thêm / sửa / hủy, T-02 Danh mục, T-03 Thành viên (chỉ xem) |
| Học sinh | nâu `#7a4b00` | GVCN cấp | S-01 → S-03 bản công khai |
| Phụ huynh | xanh lá `#1e5c33` | GVCN liên kết email | G-01 Quỹ của con, S-01 → S-03 bản công khai |

## 2. Cấu trúc Canvas (51 artboard, 8 trang)

| Trang | Artboard |
|---|---|
| 00 · Thiết kế & phân quyền | D-01 `Main` (phạm vi, ma trận quyền, danh mục màn hình, quyết định, trạng thái triển khai) · D-02 `Sitemap` · D-03 `Flows` (13 luồng) |
| 01 · Thành phần & trạng thái | `AppHeader` (4 vai trò, "Đổi quỹ" cho GVCN / phụ huynh) · `AppNav` (menu theo vai trò) · `UiSelect` · `States` · `TxDialog` |
| 02 · Đăng ký · Đăng nhập · Tài khoản | A-01 `TeacherRegister` · A-02 `Login` · A-03 `ChangePasswordRequired` · A-04 `AccountTeacher`, `Account` (thủ quỹ), `AccountStudent`, `AccountParent` |
| 03 · GVCN | G-01 `FundList` · S-01 `OverviewTeacher` · G-02 `Students` · G-03 `Treasurers` · G-04 `FundSettingsTeacher` · S-02 `LedgerTeacher` · S-03 `ReportsTeacher` |
| 04 · Học sinh – Thủ quỹ | S-01 `Overview` · S-02 `Ledger` · S-03 `Reports` · T-01 `TransactionForm` · T-02 `FundSettings` (danh mục) · T-03 `Members` |
| 05 · Học sinh (công khai) | `OverviewStudent` · `LedgerStudent` · `ReportsStudent` |
| 06 · Phụ huynh (công khai) | G-01 `FundListParent` · `OverviewParent` · `LedgerParent` · `ReportsParent` |
| 07 · Mobile 360 / 390 / 430 | `MobileAuth`, `Mobile360Auth` · `MobileTeacherAdmin`, `MobileTeacherStudents`, `MobileTeacherTreasurers`, `Mobile360Teacher`, `Mobile430Teacher`, `MobileTeacher` · `Mobile`, `Mobile430Treasurer` · `MobileStudent`, `Mobile430Student` · `MobileParentFunds`, `MobileParent`, `Mobile360Parent` · `MobileStates` |

## 3. Nguyên tắc
1. **Đăng nhập không chọn vai trò**: một ô "Email hoặc tên đăng nhập" + mật khẩu. Sau đăng nhập, giao diện điều hướng theo `/auth/me`. Chỉ giáo viên có link đăng ký; học sinh, phụ huynh do GVCN cấp tài khoản.
2. **Vai trò theo từng quỹ, do hệ thống xác định**. Giao diện không gửi vai trò; chỉ vẽ nút theo `permissions` của quỹ đang mở.
3. **Không có quyền thì không vẽ nút**. Backend vẫn chặn (403 / 404). 403 lúc trang đang mở → trạng thái "Quyền của bạn vừa thay đổi".
4. **Nhãn vai trò luôn hiện** trên header: "GVCN" / "Thủ quỹ" / "Học sinh · Công khai" / "Phụ huynh · Công khai". GVCN và phụ huynh có "Đổi quỹ".
5. **GVCN không ghi thu chi** và không quản lý danh mục; quản lý học sinh, phụ huynh, phân công thủ quỹ, tên quỹ.
6. **Mật khẩu tạm hiển thị một lần**: hộp thoại phải tick "Tôi đã lưu / chuyển thông tin này" mới đóng được; luôn ghi "Hệ thống không gửi email".
7. **Thủ quỹ cuối cùng**: nút "Thu hồi" bị khóa kèm giải thích "phân công người mới trước". Quỹ chưa có thủ quỹ có banner vàng "Chưa phân công thủ quỹ".
8. **Ghi một khoản ≤ 3 thao tác** (thủ quỹ): "+ Thêm khoản thu / chi" (mobile: nút nổi) → dialog → "Lưu".
9. **Số dư nổi bật**; số dư âm có cảnh báo vàng, không chặn lưu. Thu / chi phân biệt bằng dấu + chữ + màu (+ `#1e7a3c` / − `#b3261e`).
10. **Khoản không công khai**: nhãn "Không công khai" (chỉ GVCN, thủ quỹ thấy). Học sinh, phụ huynh thấy dòng gộp "Khoản khác" không có số lượng; form cảnh báo P1 (số tiền có thể suy ra).
11. **Hủy cần lý do** và xác nhận; không có nút "Xóa" ở bất kỳ đâu.
12. **Mật khẩu**: 8–128 ký tự, có cả chữ và số (đăng ký, đổi mật khẩu, mật khẩu tạm).
13. **Responsive**: ≥ 1024 sidebar 232px · 768–1023 sidebar icon · < 768 thanh dưới, bảng → thẻ, dialog → bottom sheet, vùng chạm ≥ 44px. Thiết kế mobile ở 360 / 390 / 430.
14. **Accessibility**: nút / liên kết thật, label cho mọi ô nhập, tương phản ≥ 4.5:1, `aria-current` cho menu, `role="alert"` cho lỗi, `role="radiogroup"` cho lựa chọn.

## 4. Kendo component chính
Grid (desktop) / ListView (mobile) · Dialog / ActionSheet · Drawer (chi tiết học sinh) · DatePicker · NumericTextBox (VND, `format="n0"`) · DropDownList · Checkbox · RadioButton · TabStrip · Chart · Notification · Skeleton · Badge.

## 5. Tài liệu
[sitemap-and-navigation.md](sitemap-and-navigation.md) · [user-flows.md](user-flows.md) · [screen-specifications.md](screen-specifications.md) · [wireframes.md](wireframes.md) · [ui-open-questions.md](ui-open-questions.md)

## 6. Lịch sử thay đổi
| Ngày | Phiên bản | Thay đổi |
|---|---|---|
| 2026-11-04 | 2.0 / 2.1 | Thiết kế 3 vai trò Thủ quỹ / GVCN / Phụ huynh (đã thay thế – Git history) |
| 2026-11-05 | 3.0 | Mô hình Giáo viên · Học sinh · Phụ huynh: thêm A-01, G-01 → G-04, màn hình học sinh, mobile GVCN 360 / 390 / 430; T-02 chỉ còn danh mục, T-03 chỉ xem |
| 2026-11-05 | 3.1 | Đăng nhập không chọn vai trò (bỏ `LoginStudent`, `LoginParent`); trạng thái mật khẩu tạm / quỹ chưa phân công; viết lại tài liệu UI cho khớp Canvas; trạng thái triển khai = Not Implemented |
