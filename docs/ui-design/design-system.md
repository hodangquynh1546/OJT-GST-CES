# Design System – ClassFund v4.0 (Desktop Web)

> v4.0 · 2026-11-07 · Canvas: trang "01 · Design System v4" → DS-01 `DSFoundations`, DS-02 `DSComponents`. Nền: **Kendo Theme Default** (Kendo UI for Angular 23.3.0), ghi đè bằng `--kendo-color-*`. Token khai báo ở `ClassFundUI/src/styles.scss`.
> Quy tắc: component **không** viết mã màu / cỡ chữ trực tiếp; chỉ dùng `var(--cf-*)` hoặc class dùng chung (`.cf-card`, `.cf-muted`, `.cf-money`).

## 1. Màu

### 1.1 Thương hiệu & bề mặt
| Token | Giá trị | Dùng cho |
|---|---|---|
| `--cf-primary` | `#1d5fa8` | Nút chính, link, mục nav đang chọn (= `--kendo-color-primary`) |
| `--cf-primary-dark` | `#164a83` | Hover / active; chữ trên nền tint |
| `--cf-primary-tint` | `#e3eefa` | Nền mục nav đang chọn, ghi chú thông tin |
| `--cf-text` | `#1f2328` | Chữ chính |
| `--cf-text-muted` | `#59636e` | Chữ phụ, nhãn cột, gợi ý |
| `--cf-border` | `#d8dee4` | Viền thẻ, đường kẻ bảng |
| `--cf-border-strong` | `#c4ccd4` | Viền ô nhập, nút phụ |
| `--cf-surface` | `#ffffff` | Thẻ, header, nav, dialog |
| `--cf-background` | `#f6f8fa` | Nền trang |
| `--cf-subtle` | `#eef1f4` | Tag trung tính, dải phân cách |

### 1.2 Ngữ nghĩa
| Token | Giá trị | Dùng cho |
|---|---|---|
| `--cf-income` | `#1e7a3c` | Số tiền thu (luôn kèm "+") |
| `--cf-expense` | `#b3261e` | Số tiền chi (luôn kèm "−"), số dư âm |
| `--cf-success-fg` / `--cf-success-bg` | `#1e5c33` / `#e6f4ea` | Thành công, "Đang hoạt động" |
| `--cf-warning-fg` / `--cf-warning-bg` | `#6b4500` / `#fff4d6` | "Chưa phân công thủ quỹ", mật khẩu tạm, số dư âm, "Chờ kích hoạt" |
| `--cf-danger-fg` / `--cf-danger-bg` | `#8f1d17` / `#fde8e7` | Lỗi, "Đã hủy", "Đã khóa" |

### 1.3 Vai trò (chỉ cho badge, avatar, viền thẻ vai trò – không cho nút)
| Vai trò | Chữ | Nền |
|---|---|---|
| GVCN | `--cf-role-teacher` `#5b2a86` | `--cf-role-teacher-tint` `#f1e8fb` |
| Thủ quỹ | `--cf-role-treasurer` `#1d5fa8` | `--cf-role-treasurer-tint` `#e3eefa` |
| Học sinh | `--cf-role-student` `#7a4b00` | `--cf-role-student-tint` `#fff1d6` |
| Phụ huynh | `--cf-role-parent` `#1e5c33` | `--cf-role-parent-tint` `#e6f4ea` |

Không dùng màu làm tín hiệu duy nhất: luôn kèm dấu, nhãn chữ hoặc icon.

## 2. Typography
Font: `system-ui, -apple-system, 'Segoe UI', Roboto, Arial, sans-serif` (`--kendo-font-family`). Body 14 / 20.

| Token | Cỡ / dòng · đậm | Dùng cho |
|---|---|---|
| `--cf-font-h1` | 24 / 32 · 700 | Tiêu đề trang |
| `--cf-font-h2` | 17 / 24 · 600 | Tiêu đề thẻ / khối |
| `--cf-font-kpi` | 24 / 32 · 700 · tabular | Số trên KPI card |
| `--cf-font-body` | 14 / 20 · 400 | Nội dung, ô bảng |
| `--cf-font-body-strong` | 14 / 20 · 600 | Tên người, số tiền trong bảng |
| `--cf-font-small` | 13 / 18 · 400 | Chú thích, nhãn ô nhập (600) |
| `--cf-font-caption` | 12 / 16 · 400–600 | Nhãn cột, tag, gợi ý dưới ô |

Định dạng: tiền `1.250.000 đ` (vi-VN, `font-variant-numeric: tabular-nums`, căn phải, không xuống dòng, dấu `+` / `−`); ngày `dd/MM/yyyy`; ngày giờ `dd/MM/yyyy HH:mm`.

## 3. Khoảng cách, bo góc, đổ bóng
- Lưới 4 px: `--cf-space-1` 4 · `-2` 8 · `-3` 12 · `-4` 16 · `-5` 20 · `-6` 24 · `-8` 32 · `-12` 48.
- Bo góc: `--cf-radius-sm` 4 (ô màu) · `-md` 6 (nút, ô nhập, thông báo) · `-lg` 8 (thẻ, dialog) · `-pill` 12 (tag, badge).
- Đổ bóng: thẻ không đổ bóng (chỉ viền 1 px) · `--cf-shadow-toast` `0 8px 24px rgba(15,23,42,.25)` (toast, dropdown) · `--cf-shadow-dialog` `0 16px 40px rgba(15,23,42,.3)` + lớp phủ `rgba(15,23,42,.45)`.

## 4. Layout (desktop)
| Vùng | Quy định |
|---|---|
| Khung | Chuẩn 1440 × 900; `body { min-width: 1280px }`; không `@media` responsive |
| AppHeader | Cao 56 px (`--cf-header-height`), cố định trên |
| AppNav | Sidebar trái 232 px (`--cf-nav-width`), luôn hiện nhãn; chân nav mô tả quyền |
| Nội dung | Padding 24 px; `max-width` 1200 px (trang dữ liệu) / 960 px (form, tài khoản); khoảng giữa khối 16 px |
| Trang khách (A-01, A-02, A-03) | Không header / nav; thẻ giữa rộng 440 px, cách trên 48 px |
| KPI | Lưới 4 cột bằng nhau |
| Dialog | 480 px (xác nhận), 560 px (form giao dịch); Drawer chi tiết học sinh 460 px bên phải |

## 5. Trợ năng
Tương phản ≥ 4.5:1 · focus `outline: 2px solid var(--cf-primary); outline-offset: 2px` · vùng bấm ≥ 32 × 32 (nút chuẩn 40 px) · `role="status"` (thành công / cảnh báo), `role="alert"` (lỗi) · ô lỗi có `aria-invalid` + `aria-describedby` · icon trang trí `aria-hidden="true"`, nút chỉ có icon có `aria-label` · `<html lang="vi">`.

## 6. Thành phần → Kendo UI for Angular 23.3.0

| Thành phần | Kendo | Quy tắc |
|---|---|---|
| Button | `kendoButton` (`themeColor` primary / base / error; `fillMode` flat) | Cao 40 px (medium), 32 px (small). Tối đa 1 nút chính / màn hình. Khi gửi: tắt + spinner |
| TextBox / số tiền | `kendo-textbox`, `kendo-numerictextbox` | Số tiền: `format="n0"`, `min=1`, `spinners=false`, căn phải |
| Ngày | `kendo-datepicker` | `format="dd/MM/yyyy"`, `max` = hôm nay (`transaction.future_date`) |
| Chọn | `kendo-dropdownlist`, `kendo-combobox` | Danh mục ẩn không có khi thêm mới; khi sửa vẫn hiện danh mục hiện tại |
| Ghi chú, lý do hủy | `kendo-textarea` | Lý do hủy bắt buộc (`transaction.cancel_reason_required`) |
| Nhãn / lỗi / gợi ý | `kendo-label`, `kendo-formerror`, `kendo-formhint` | Lỗi hiện **sau khi bấm Lưu**; lỗi 422 gắn vào ô theo `errors[field]` + hộp tóm tắt đầu form |
| Công khai | `kendo-switch` | Tắt → cảnh báo P1 dưới công tắc |
| Bảng | `kendo-grid` (pageable, server paging) | `page`, `pageSize` (20, tối đa 100); bộ lọc riêng phía trên bảng; cột tiền căn phải; dòng đã hủy mờ + gạch ngang + tag "Đã hủy" |
| Dialog | `kendo-dialog`, `kendo-dialog-actions` | Esc / "Hủy bỏ" đóng; hành động phá hủy luôn có dialog xác nhận |
| Drawer | `kendo-drawer` (mode overlay, position end) | Chi tiết học sinh G-02 |
| Toast | `NotificationService` | `position` bottom-right, `hideAfter` 4000, type success / warning / error |
| Tab | `kendo-tabstrip` | S-03 "Theo tháng" · "Theo năm" |
| Biểu đồ | `kendo-chart` (column, 2 series Thu / Chi) | Màu `--cf-income` / `--cf-expense`; kèm bảng số cho trình đọc màn hình |
| Loading | `kendo-loader`, `kendo-skeleton` | Skeleton cho KPI / bảng; loader trong nút |
| Tag / badge | `kendo-chip` hoặc `span.cf-tag` | Chỉ hiển thị, không bấm |
| AppHeader, AppNav | Component riêng `core/navigation` | Board `AppHeader`, `AppNav` |
| AppState | Component riêng `shared/components` | Board `States`: Loading / Empty / Error / 401 / 403 / 404 / 409 / mật khẩu tạm / chưa phân công |

## 7. Icon
SVG 24 × 24, nét 2 px, `stroke="currentColor"`, hiển thị 18–20 px: `funds`, `overview`, `ledger`, `reports`, `people`, `treasurers`, `categories`, `settings`, `account`, `add`, `chevron-down`, `check`, `warning`. Path chuẩn: board DS-01 và `ClassFundUI/src/app/core/navigation/nav-items.ts`.
