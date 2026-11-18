# Design System

> Cài đặt: `ClassFundUI/src/styles.scss` (token + lớp tiện ích) và `src/app/shared/components/`. Desktop ≥ 1280 px, tiếng Việt, Kendo Theme Default.

## 1. Token màu (`--cf-*`)

| Nhóm | Token | Dùng cho |
|---|---|---|
| Chính | `--cf-primary`, `--cf-primary-dark`, `--cf-primary-tint` | Nút chính, link, mục menu đang chọn, nhãn phạm vi Sự kiện |
| Tiền | `--cf-income` (xanh lá), `--cf-expense` (đỏ) | Số tiền thu / chi |
| Chữ & nền | `--cf-text`, `--cf-text-muted`, `--cf-surface`, `--cf-background`, `--cf-subtle`, `--cf-border`, `--cf-border-strong` | Bố cục |
| Trạng thái | `--cf-success-*`, `--cf-warning-*`, `--cf-danger-*` (fg/bg) | Tag, banner |
| Vai trò | `--cf-role-owner*`, `--cf-role-treasurer*`, `--cf-role-student*` | Badge vai trò ở header |

Component **không** viết mã màu – chỉ dùng token. Token được ánh xạ sang biến Kendo (`--kendo-color-*`).

## 2. Kích thước & bố cục

- Header 56 px, menu trái 232 px, nội dung tối đa 1200 px (`.cf-page`), trang form 960 px (`.cf-page--narrow`).
- Control Kendo: medium 40 px, small 32 px. Bo góc 4/6/8 px. Font hệ thống 14/20.
- KPI: lưới 4 cột `.cf-kpis` với thẻ `.cf-card`.

## 3. Lớp tiện ích

| Lớp | Ý nghĩa |
|---|---|
| `.cf-card` | Thẻ nền trắng viền |
| `.cf-money` + `.cf-income` / `.cf-expense` | Số tiền căn phải, chữ số đều, màu thu/chi |
| `.cf-tag`, `--success` / `--warning` / `--danger` | Nhãn trạng thái |
| `.cf-alert`, `--warning` / `--danger` | Banner trong trang (role="status"/"alert") |
| `.cf-muted` | Chữ phụ |

## 4. Component dùng chung

| Component | Selector | Khi nào dùng |
|---|---|---|
| `PageHeader` | `<app-page-header title subtitle>` | Đầu mọi trang; nút hành động đặt trong content |
| `AppState` | `<app-state kind>` | loading (skeleton) / empty / error / forbidden / not-found / warning |
| `ScopeBadge` | `<app-scope-badge scope eventName>` | **Bắt buộc** cạnh giao dịch, khoản thu, thông báo: "Quỹ chung" (xám) / "Sự kiện: tên" (xanh) |
| `ComingSoon` | route `comingSoon(code, title)` | Màn hình giữ chỗ chưa làm |
| `HasPermissionDirective` | `*appHasPermission="Permissions.X"` | Ẩn nút/khu vực theo quyền |
| `ConfirmDialog` | service | Xác nhận hành động phá hủy (480 px, nút đỏ khi `danger`) |
| `AppNotifications` | service | Toast góc dưới phải 4 s |
| Pipes | `vndMoney`, `vnDate` | `1.250.000 đ`, `16/11/2026` (giờ VN) |

## 5. Quy tắc hiển thị nghiệp vụ

- **Phân biệt phạm vi**: mọi bảng giao dịch/khoản thu có cột "Phạm vi" hoặc nằm trong trang đã rõ phạm vi (tiêu đề "Thu chi chung", "Sự kiện: 20/11").
- **Thu tự ghi nhận của thủ quỹ**: tag cảnh báo "Tự ghi nhận" + tooltip "Chưa đối chiếu với thủ quỹ khác".
- Trạng thái thanh toán: Chờ xác nhận (warning), Đã xác nhận (success), Bị từ chối (danger, kèm lý do).
- Số tiền thiếu/âm dùng `--cf-expense`; thừa dùng `--cf-income`.
- Mật khẩu tạm hiển thị một lần trong dialog có nút "Sao chép" + cảnh báo "sẽ không hiển thị lại".
- Mọi thông báo lỗi lấy theo `code` (`registerErrorMessages`); không hiện stack/chi tiết kỹ thuật.
- **Form ghi chi** hiện mức chi tối đa (số dư phạm vi) dưới ô số tiền; vượt thì banner `--danger` và khóa nút lưu (BR-38).
- **Form ghi thu** dùng chung bố cục với "Tạo khoản thu": tên, mục đích, số tiền mỗi người, hạn đóng, banner "Áp cho N người → tổng phải thu".
- Hành động không hoàn tác (kết thúc sự kiện, quyết toán, tổng kết cuối năm) mở hộp thoại liệt kê hệ quả (ai bị loại, ai được nhắc, ai nhận thông báo) trước khi xác nhận.

## 6. Bản thiết kế tham chiếu

- Canvas **"ClassFund UI Design"** (Claude Design, riêng tư – chủ canvas chia sẻ khi cần): 34 màn hình chia 6 trang theo vai trò + 8 thành phần dùng chung; bấm Play ở màn Đăng nhập để đi theo luồng.
- Bản thiết kế vẽ theo phong cách shadcn/ui (zinc, nút chính màu đen, bo góc 8–12 px) **chỉ để thống nhất bố cục và luồng**. Code vẫn dùng Kendo UI và token `--cf-*` ở trên; không thêm thư viện UI khác.
- Khi làm màn hình: theo bố cục, nội dung, trạng thái và luồng của bản thiết kế; ánh xạ sang component Kendo tương ứng (Grid, Dialog, TabStrip, SegmentedControl/ButtonGroup, Upload). Chỗ bản thiết kế dùng hộp thoại cho H-02, E-02, L-02 thì giữ route ở sitemap để mở thẳng được.
- Công tắc ghi "Bản mẫu" trên canvas chỉ để xem các trạng thái; không phải chức năng.
