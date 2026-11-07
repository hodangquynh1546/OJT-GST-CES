# Wireframes

> v4.0 · 2026-11-07 · **Desktop Web only** (khung 1440 × 900, tối thiểu 1280 px). Bản chuẩn là các artboard tương tác trên Design Canvas: https://claude.ai/artifact/D5JVqKkBFqQhmcp5spfw6Y. File này chỉ tóm tắt bố cục. Token và kích thước: [design-system.md](design-system.md).

## Khung trang trong ứng dụng
```text
┌ AppHeader 56: ClassFund | Quỹ lớp 5A · Đổi quỹ ▾ (GVCN / PH) [nhãn vai trò]      [Người dùng ▾] ┐
├ AppNav 232 ─────────────┬ Nội dung (padding 24, max 1200) ────────────────────────────────────┤
│ Quỹ của tôi (G / PH)    │ Tiêu đề trang                       [+ Khoản thu][+ Khoản chi] (T)  │
│ Tổng quan               │ [banner vai trò / "Chưa phân công thủ quỹ" / số dư âm]              │
│ Thu chi                 │ [Số dư][Tổng thu][Tổng chi][Tháng này]   ← KPI 4 cột                │
│ Báo cáo                 │ ┌ Giao dịch gần đây (5) ──────┐┌ Thu chi 3 tháng (reports/yearly) ┐ │
│ Học sinh & PH (G)       │ │ ngày · nội dung · ±tiền     │└ Thông tin quỹ (managers) ────────┘ │
│ Phân công thủ quỹ (G)   │ └─────────────────────────────┘                                     │
│ Thông tin quỹ (G)       │                                                                     │
│ Danh mục / Thành viên(T)│                                                                     │
│ ─ chân nav: mô tả quyền │                                                                     │
└─────────────────────────┴─────────────────────────────────────────────────────────────────────┘
```

## Màn hình
```text
G-01: lưới thẻ quỹ 3 cột [tên · trạng thái thủ quỹ · số dư · học sinh · thủ quỹ · ngày tạo] + dialog "Tạo quỹ" 480.
      Phụ huynh: thẻ [tên · "Con: …" · số dư · thủ quỹ] → bản công khai.
G-02: [tìm][chip lọc] Kendo Grid học sinh → Drawer phải 460 (thông tin · tài khoản · phụ huynh · thủ quỹ)
      → dialog mật khẩu tạm một lần.
G-03: thẻ "Đang là thủ quỹ (n)" [Thu hồi] + bảng lịch sử phân công; dialog radio chọn học sinh đã kích hoạt.
G-04: thẻ form Tên quỹ + thẻ thông tin (ngày tạo, GVCN, thủ quỹ, số học sinh); max 960.
S-02: [bộ lọc: loại · danh mục · từ – đến · tìm · trạng thái] [Grid | panel chi tiết 340 (Sửa / Hủy chỉ thủ quỹ)] + phân trang.
S-03: TabStrip [Theo tháng | Theo năm]; tháng: 5 KPI (đầu kỳ, thu, chi, chênh lệch, cuối kỳ) + theo danh mục + danh sách.
T-01: dialog 560 [Thu | Chi] nội dung · số tiền · ngày · danh mục · ghi chú · công khai; Hủy: tóm tắt + lý do.
A-01 / A-02 / A-03: thẻ giữa 440, logo ClassFund ở trên, không header / nav.
```

## Ma trận artboard

| Màn hình | GVCN | Thủ quỹ | Học sinh | Phụ huynh |
|---|---|---|---|---|
| Đăng ký / đăng nhập / mật khẩu tạm | `TeacherRegister`, `Login` | `Login`, `ChangePasswordRequired` | như thủ quỹ | như thủ quỹ |
| Quỹ của tôi / của con | `FundList` | – | – | `FundListParent` |
| Tổng quan | `OverviewTeacher` | `Overview` | `OverviewStudent` | `OverviewParent` |
| Thu chi + chi tiết | `LedgerTeacher` | `Ledger` | `LedgerStudent` | `LedgerParent` |
| Báo cáo | `ReportsTeacher` | `Reports` | `ReportsStudent` | `ReportsParent` |
| Thêm / sửa / hủy | – | `TransactionForm`, `TxDialog` | – | – |
| Học sinh & phụ huynh | `Students` | – | – | – |
| Phân công thủ quỹ | `Treasurers` | – | – | – |
| Thông tin quỹ / Danh mục / Thành viên | `FundSettingsTeacher` | `FundSettings`, `Members` | – | – |
| Tài khoản | `AccountTeacher` | `Account` | `AccountStudent` | `AccountParent` |
| Trạng thái | `States` | | | |
| Design System | `DSFoundations`, `DSComponents` | | | |

Artboard S-01 có tweak `viewState` (normal / loading / empty / error / negative / unassigned).
