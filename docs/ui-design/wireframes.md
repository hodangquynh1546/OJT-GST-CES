# Wireframes

> v3.1 · 2026-11-05 · Bản chuẩn là các artboard tương tác trên Design Canvas: https://claude.ai/artifact/D5JVqKkBFqQhmcp5spfw6Y. File này chỉ tóm tắt bố cục. Trạng thái triển khai: **Not Implemented**.

## Desktop (≥ 1024)
```text
┌ AppHeader: Quỹ Lớp | Quỹ lớp 5A · Đổi quỹ ▾ (GVCN / PH) [nhãn vai trò]       [Người dùng ▾] ┐
├ AppNav ─────────────────┬ Nội dung (max 1200) ─────────────────────────────────────────────┤
│ Quỹ của tôi (G / PH)    │ Tiêu đề trang                      [+ Khoản thu][+ Khoản chi] (T) │
│ Tổng quan               │ [banner vai trò / "Chưa phân công thủ quỹ"]                       │
│ Thu chi                 │ Cộng dồn: [Số dư][Tổng thu][Tổng chi]                             │
│ Báo cáo                 │ Tháng này: [Thu][Chi][Chênh lệch]                                 │
│ Học sinh & PH (G)       │ ┌ Giao dịch gần đây ─────────┐┌ Biểu đồ 3 tháng ┐                │
│ Phân công thủ quỹ (G)   │ │ ngày · nội dung · ±tiền    │└ Thông tin quỹ ──┘                │
│ Thông tin quỹ (G)       │                                                                  │
│ Danh mục / Thành viên(T)│                                                                  │
└─────────────────────────┴──────────────────────────────────────────────────────────────────┘
G-01: lưới thẻ quỹ [tên · trạng thái thủ quỹ · số dư · học sinh · thủ quỹ] + dialog "Tạo quỹ".
G-02: [tìm][chip lọc] bảng học sinh → drawer phải 460px (thông tin · tài khoản · phụ huynh · thủ quỹ) → dialog mật khẩu tạm một lần.
G-03: thẻ "Đang là thủ quỹ" [Thu hồi] + bảng lịch sử; dialog radio chọn học sinh đã kích hoạt.
S-02: [bộ lọc] [bảng | panel chi tiết 340px (Sửa / Hủy chỉ thủ quỹ)].
A-02: thẻ đăng nhập 400px: [Email hoặc tên đăng nhập][Mật khẩu][Đăng nhập] · link Đăng ký giáo viên.
```

## Mobile (< 768; thiết kế 360 / 390 / 430)
```text
┌ Tiêu đề màn hình                [nhãn vai trò] ┐
│ GVCN: [+ Tạo quỹ / + Thêm học sinh / + Phân công] │
│ Thủ quỹ / HS / PH: ┌ Số dư (nền xanh) ┐ [Thu][Chi] │
│ Danh sách thẻ …                                 │
│                                  (+) FAB (T)    │
├ GVCN: Quỹ | Học sinh | Thủ quỹ | Thu chi        ┤
├ Khác: Tổng quan | Thu chi | Báo cáo | Khác/Tài khoản ┤
```
Chạm vào thẻ → bottom sheet chi tiết. Mọi dialog desktop → bottom sheet; mật khẩu tạm hiện trong sheet một lần.

## Ma trận artboard

| Màn hình | GVCN | Thủ quỹ | Học sinh | Phụ huynh | Mobile |
|---|---|---|---|---|---|
| Đăng ký / đăng nhập / mật khẩu tạm | `TeacherRegister`, `Login` | `Login`, `ChangePasswordRequired` | như thủ quỹ | như thủ quỹ | `MobileAuth`, `Mobile360Auth` |
| Quỹ của tôi / của con | `FundList` | – | – | `FundListParent` | `MobileTeacherAdmin`, `MobileParentFunds` |
| Tổng quan | `OverviewTeacher` | `Overview` | `OverviewStudent` | `OverviewParent` | `MobileTeacher`, `Mobile`, `MobileStudent`, `MobileParent` |
| Thu chi + chi tiết | `LedgerTeacher` | `Ledger` | `LedgerStudent` | `LedgerParent` | tab Thu chi |
| Báo cáo | `ReportsTeacher` | `Reports` | `ReportsStudent` | `ReportsParent` | tab Báo cáo |
| Thêm / sửa / hủy | – | `TransactionForm`, `TxDialog` | – | – | sheet |
| Học sinh & phụ huynh | `Students` | – | – | – | `MobileTeacherStudents`, `Mobile360Teacher` |
| Phân công thủ quỹ | `Treasurers` | – | – | – | `MobileTeacherTreasurers`, `Mobile430Teacher` |
| Thông tin quỹ / Danh mục / Thành viên | `FundSettingsTeacher` | `FundSettings`, `Members` | – | – | "Khác" |
| Tài khoản | `AccountTeacher` | `Account` | `AccountStudent` | `AccountParent` | "Khác" / "Tài khoản" |
| Trạng thái | `States` | | | | `MobileStates` |

Artboard S-01 có tweak `viewState` (normal / loading / empty / error / negative / unassigned).
