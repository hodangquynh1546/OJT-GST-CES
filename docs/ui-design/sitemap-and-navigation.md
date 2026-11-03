# Sitemap & Navigation

> Phiên bản: 0.1 (bản nháp, chờ duyệt) · Ngày: 2026-11-03
> Mã màn hình (SCR) và hộp thoại (DLG): [screen-specifications.md](screen-specifications.md). Route dưới đây chỉ là định hướng cho Angular Router.

## 1. Danh sách màn hình

| Nhóm | Mã | Màn hình | Route (định hướng) |
|---|---|---|---|
| **Chung** | COM-01 | Đăng nhập | `/login` |
| | COM-02 | Kích hoạt tài khoản từ lời mời | `/activate/:token` |
| | COM-03 | Quên / đặt lại mật khẩu | `/forgot-password`, `/reset-password/:token` |
| | COM-04 | Hồ sơ cá nhân & đổi mật khẩu | `/profile` |
| | COM-05 | Thông báo | `/notifications` |
| | COM-06 | Việc cần xử lý (chờ duyệt / chờ xác nhận) | `/tasks` |
| | COM-07 | Không có quyền / không tìm thấy | `/forbidden`, `/**` |
| **Admin** | ADM-01 | Tổng quan toàn trường | `/admin` |
| | ADM-02 | Năm học | `/admin/school-years` |
| | ADM-03 | Lớp & phân công GVCN | `/admin/classes` |
| | ADM-04 | Tài khoản & lời mời GVCN | `/admin/users` |
| | ADM-05 | Chuyển lớp / lên lớp | `/admin/student-moves` |
| | ADM-06 | Người giữ quỹ lớp cũ & quyền cấp riêng | `/admin/fund-custodians` |
| | ADM-07 | Danh mục chi chung | `/admin/expense-categories` |
| | ADM-08 | Nhật ký thao tác & truy cập | `/admin/audit` |
| **GVCN** | TCH-01 | Tổng quan lớp (GVCN) | `/classes/:classId/homeroom` |
| | TCH-02 | Học sinh & phụ huynh | `/classes/:classId/students` |
| | TCH-03 | Phân công thủ quỹ | `/classes/:classId/treasurer` |
| | TCH-04 | Chuyển quỹ | `/classes/:classId/fund-transfers` |
| | TCH-05 | Chốt sổ năm học | `/classes/:classId/closing` |
| **Thủ quỹ** | TRS-01 | Tổng quan thủ quỹ | `/classes/:classId/treasury` |
| | TRS-02 | Ghi phiếu thu (kể cả thu hộ) | `/classes/:classId/receipts/new` |
| | TRS-03 | Xác nhận chuyển khoản | `/classes/:classId/transfer-confirmations` |
| | TRS-04 | Thu khác | `/classes/:classId/other-incomes` |
| | TRS-05 | Phiếu chi | `/classes/:classId/expenses` |
| | TRS-06 | Phiếu hoàn trả | `/classes/:classId/refunds` |
| | TRS-07 | Bàn giao tiền thu hộ | `/classes/:classId/cash-handovers` |
| | TRS-08 | Bàn giao số dư cá nhân | `/classes/:classId/balance-handovers` |
| | TRS-09 | Danh mục chi của lớp | `/classes/:classId/expense-categories` |
| **Quỹ lớp (dùng chung)** | FND-01 | Đợt thu | `/classes/:classId/campaigns` |
| | FND-02 | Chi tiết đợt thu | `/classes/:classId/campaigns/:campaignId` |
| | FND-03 | Hồ sơ tài chính học sinh | `/classes/:classId/students/:studentId/finance` |
| | FND-04 | Sổ quỹ | `/classes/:classId/ledger` |
| | FND-05 | Báo cáo | `/classes/:classId/reports/:reportType` |
| | FND-06 | Chi tiết chứng từ | `/documents/:documentType/:documentId` |
| | FND-07 | Biên bản chênh lệch | `/discrepancies/:discrepancyId` |
| **Người giữ quỹ** | CUS-01 | Lớp được giao giữ quỹ | `/custodian` |
| **Phụ huynh** | PAR-01 | Trang chủ phụ huynh | `/parent/children/:studentId` |
| | PAR-02 | Khoản nộp của con | `/parent/children/:studentId/payments` |
| | PAR-03 | Báo chuyển khoản | `/parent/children/:studentId/transfer-report` |
| | PAR-04 | Sổ thu chi lớp | `/parent/children/:studentId/class-ledger` |

**Tổng: 41 màn hình** (7 chung + 8 Admin + 5 GVCN + 9 Thủ quỹ + 7 dùng chung + 1 Người giữ quỹ + 4 Phụ huynh) và **10 hộp thoại** (DLG-01 → DLG-10).

## 2. Khung điều hướng

```text
┌─────────────────────────────────────────────────────────────────────┐
│ AppBar: [☰] School Fund · [Phạm vi: GVCN · 6A / 2026-2027 ▾] · [Việc cần xử lý (3)] · [🔔 2] · [Tài khoản ▾] │
├──────────────┬──────────────────────────────────────────────────────┤
│ Drawer       │ Breadcrumb: Lớp 6A / 2026-2027 › Đợt thu › Quỹ HK1     │
│ (menu theo   │                                                      │
│  phạm vi)    │  Nội dung màn hình                                   │
└──────────────┴──────────────────────────────────────────────────────┘
```

- Menu trái (`kendo-drawer`) **thay đổi theo phạm vi** đang chọn (mục 3). Mục không có permission thì không hiện.
- "Việc cần xử lý" (COM-06) và thông báo (COM-05) luôn ở AppBar, kèm số đếm (`kendo-badge`). Không có tìm kiếm toàn cục ở bản này (UQ-12).
- Mobile: Drawer dạng overlay. Phụ huynh và thủ quỹ có **thanh điều hướng đáy** gồm 4 mục chính.

## 3. Menu theo phạm vi

### 3.1 Admin – phạm vi "Toàn trường"

| Mục menu | Màn hình | Permission |
|---|---|---|
| Tổng quan | ADM-01 | `Report.ViewSummary` |
| Năm học | ADM-02 | `SchoolYear.Manage` |
| Lớp & GVCN | ADM-03 | `Class.Manage` |
| Tài khoản & lời mời | ADM-04 | `Invitation.SendTeacher` |
| Chuyển lớp / lên lớp | ADM-05 | `Student.Transfer` |
| Người giữ quỹ lớp cũ & quyền cấp riêng | ADM-06 | `FundCustodian.Assign` |
| Danh mục chi chung | ADM-07 | `ExpenseCategory.ManageShared` |
| Báo cáo các lớp | ADM-01 → chọn lớp → FND-04 / FND-05 (chỉ xem) | `Report.ViewSummary`, `Report.ViewDetail` |
| Nhật ký | ADM-08 | `AuditLog.View` |

### 3.2 GVCN – phạm vi "GVCN · lớp"

| Mục menu | Màn hình | Permission |
|---|---|---|
| Tổng quan lớp | TCH-01 | `Report.ViewSummary` |
| Học sinh & phụ huynh | TCH-02 | `Student.Manage` |
| Thủ quỹ | TCH-03 | `Treasurer.Assign` |
| Đợt thu | FND-01 | `Collection.View` |
| Sổ quỹ | FND-04 | `Report.ViewDetail` |
| Báo cáo | FND-05 | `Report.ViewDetail` |
| Chuyển quỹ | TCH-04 | `FundTransfer.Request` |
| Chốt sổ | TCH-05 | `Fund.Close` |
| Nhật ký lớp | ADM-08 (lọc theo lớp) | `AuditLog.View` |

Việc duyệt (phiếu chi, hoàn trả, miễn giảm, phương án chênh lệch) đi qua COM-06.

### 3.3 Thủ quỹ – phạm vi "Thủ quỹ · lớp"

| Mục menu | Màn hình | Permission |
|---|---|---|
| Tổng quan | TRS-01 | `Report.ViewSummary` |
| **Ghi thu** (nút chính) | TRS-02 | `Receipt.Create` / `Receipt.CollectOnBehalf` |
| Xác nhận chuyển khoản | TRS-03 | `Receipt.ConfirmTransfer` |
| Đợt thu | FND-01 | `Collection.View` |
| Thu khác | TRS-04 | `OtherIncome.Create` |
| Phiếu chi | TRS-05 | `Expense.Create` |
| Hoàn trả | TRS-06 | `Refund.Create` |
| Bàn giao tiền thu hộ | TRS-07 | `CashHandover.Create` / `CashHandover.Confirm` |
| Bàn giao số dư cá nhân | TRS-08 | `BalanceHandover.Create` / `BalanceHandover.Confirm` |
| Sổ quỹ / Báo cáo | FND-04 / FND-05 | `Report.ViewDetail` |
| Danh mục chi của lớp | TRS-09 | `ExpenseCategory.ManageClass` |

Thanh đáy trên mobile: **Tổng quan · Ghi thu · Chờ xác nhận · Thêm**.

### 3.4 Người giữ quỹ – phạm vi "Giữ quỹ · lớp cũ"

| Mục menu | Màn hình | Permission |
|---|---|---|
| Lớp được giao | CUS-01 | `CashHandover.Confirm` |
| Sổ quỹ / Báo cáo (lớp được giao) | FND-04 / FND-05 | `Report.ViewSummary`, `Report.ViewDetail` |
| Ghi thu nợ cũ (nếu được cấp riêng) | TRS-02 | `Receipt.CollectFormerStudent` (D-45) |

### 3.5 Phụ huynh – phạm vi "Phụ huynh · tên con"

| Mục menu | Màn hình | Permission |
|---|---|---|
| Trang chủ | PAR-01 | `Student.ViewOwn` |
| Khoản nộp | PAR-02 | `Receipt.ViewOwn` |
| Báo chuyển khoản | PAR-03 | `Receipt.SubmitTransfer` |
| Sổ thu chi lớp | PAR-04 | `Report.ViewSummary`, `Expense.View` |

Thanh đáy trên mobile: **Trang chủ · Khoản nộp · Báo chuyển khoản · Sổ lớp**.

## 4. Trang đích sau đăng nhập

| Điều kiện | Trang đích |
|---|---|
| Có vai trò Admin | ADM-01 |
| Có phân công GVCN đang hiệu lực | TCH-01 của lớp năm học hiện tại |
| Là thủ quỹ đang hiệu lực | TRS-01 (UQ-03) |
| Chỉ là người giữ quỹ | CUS-01 |
| Chỉ là phụ huynh | PAR-01 của con đầu tiên |
| Nhiều vai trò | Theo thứ tự trên; đổi bằng bộ chọn phạm vi. Nếu có việc chờ xử lý, hiện thông báo dẫn tới COM-06 |

## 5. Điều hướng chéo quan trọng

| Từ | Đến | Cách |
|---|---|---|
| FND-02 (dòng học sinh) | FND-03 | Bấm tên học sinh |
| FND-03 (khoản nợ) | TRS-02 | "Ghi thu" có sẵn học sinh + khoản nợ |
| FND-04 (bút toán) | FND-06 | Bấm số chứng từ nguồn |
| FND-06 | FND-06 (chứng từ liên kết: đảo, bàn giao) / FND-07 | Khối "Chứng từ liên kết" |
| COM-06 | FND-06 / FND-07 | Bấm từng việc |
| COM-05 (thông báo) | Màn hình liên quan | Liên kết `RelatedEntityType/Id` |
| PAR-02 (khoản nợ) | PAR-03 | "Báo chuyển khoản" có sẵn khoản nợ |
| ADM-01 (thẻ lớp) | FND-04 / FND-05 ở chế độ chỉ xem | Phạm vi tạm thời "Admin xem lớp …" |

## 6. Guard route

| Guard | Kiểm tra |
|---|---|
| Xác thực | Đã đăng nhập; nếu chưa → COM-01 (giữ URL để quay lại) |
| Permission | Route khai báo permission cần có; không có → COM-07 |
| Phạm vi | `:classId` thuộc phạm vi của người dùng với permission đó; `:studentId` là con của phụ huynh; ngoài phạm vi → COM-07 |
| Quỹ đã khóa | Route tạo chứng từ thường (TRS-04, TRS-05 tạo mới, …) chuyển về chế độ chỉ xem (không chặn route) |

Guard UI chỉ để trải nghiệm; API vẫn kiểm tra lại toàn bộ.
