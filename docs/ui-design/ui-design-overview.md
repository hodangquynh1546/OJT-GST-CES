# UI/UX Design – Tổng quan

> Phiên bản: 0.1 (bản nháp, chờ duyệt) · Ngày: 2026-11-03
> Căn cứ: BA v0.7 (D-01 → D-58), Database Design v0.1. Công nghệ (theo `training-doc.md`, không thay đổi): **Angular ^20.3**, TypeScript ~5.9, RxJS ~7.8, **Kendo UI for Angular 23.3.0**, **Kendo Theme Default ^14.3.0**.
> Tài liệu này **không** chứa code Angular; tên route, component Kendo và cấu trúc thư mục chỉ để định hướng triển khai.

## 1. Bộ tài liệu

| Tài liệu | Nội dung |
|---|---|
| [ui-design-overview.md](ui-design-overview.md) | Nguyên tắc, bố cục, quy ước hiển thị, ánh xạ Kendo, phân quyền trên UI |
| [sitemap-and-navigation.md](sitemap-and-navigation.md) | Cây màn hình, route, menu theo vai trò, phạm vi làm việc |
| [screen-specifications.md](screen-specifications.md) | Đặc tả 41 màn hình + 10 hộp thoại: dữ liệu, thao tác, permission, trạng thái |
| [user-flows.md](user-flows.md) | 14 luồng sử dụng chính |
| [wireframes.md](wireframes.md) | Wireframe ASCII cho màn hình quan trọng (desktop + mobile) |
| [ui-open-questions.md](ui-open-questions.md) | Câu hỏi UI và giả định tạm |

## 2. Nhóm người dùng

| Nhóm | Vai trò BA | Thiết bị chính (đề xuất – UQ-10) | Mục tiêu chính trên UI |
|---|---|---|---|
| **Admin** | Quản trị hệ thống | Desktop | Thiết lập năm học / lớp / tài khoản, chuyển lớp, duyệt chuyển quỹ, xem báo cáo toàn trường |
| **GVCN** | Giáo viên chủ nhiệm | Desktop + mobile | Quản lý lớp, duyệt (chi, hoàn trả, miễn giảm, chênh lệch), chốt sổ |
| **Treasurer** | Thủ quỹ (là phụ huynh) | **Mobile** + desktop | Ghi thu nhanh, xác nhận chuyển khoản, lập chi, bàn giao |
| **Parent** | Phụ huynh | **Mobile** | Xem khoản phải nộp của con, báo chuyển khoản, xem sổ thu chi lớp |
| *Người giữ quỹ lớp cũ* | `FundCustodian` (D-45) – không nằm trong 4 nhóm yêu cầu nhưng đã có trong BA | Mobile + desktop | Xác nhận nhận tiền thu hộ, xem báo cáo lớp được giao |

Một tài khoản có thể có nhiều vai trò, ví dụ phụ huynh kiêm thủ quỹ, hoặc GVCN của nhiều lớp. UI dùng **phạm vi làm việc** để chuyển giữa các vai trò (mục 5).

## 3. Nguyên tắc UX

| # | Nguyên tắc | Áp dụng |
|---|---|---|
| 1 | **Ba con số luôn đi cùng nhau**: số dư – đang giữ chỗ – **khả dụng** (khả dụng nổi bật nhất) | Thẻ số dư quỹ, số dư cá nhân, số nợ còn có thể thu (D-40, D-47, D-48) |
| 2 | **Không trộn ba loại tiền** trên cùng một con số: quỹ lớp, công nợ, số dư cá nhân là ba khối tách biệt, có nhãn và màu riêng | BR-GEN-06, BR-RPT-07 |
| 3 | **Nợ cũ luôn có nhãn nguồn**: "Nợ cũ – 6A / 2026-2027"; lớp thu hộ chỉ thấy "Đã thu hộ" | D-31, D-42 |
| 4 | **Xem trước ảnh hưởng tiền trước khi lưu**: form thu hiển thị "trả nợ X đ · vào số dư cá nhân Y đ"; form chi hiển thị khả dụng còn lại | D-48, D-54, D-40 |
| 5 | **Trạng thái luôn bằng chữ + màu** (không chỉ màu) | Chip trạng thái (mục 6.3) |
| 6 | **Hành động theo quyền và trạng thái**: không có permission ⇒ **ẩn**; có permission nhưng điều kiện chưa đủ ⇒ **hiện, vô hiệu + lý do** (tooltip) | Mục 4 |
| 7 | **Thao tác không đảo ngược phải xác nhận** và yêu cầu lý do khi BA bắt buộc (từ chối, hủy, đảo, chốt sổ) | DLG-07, DLG-08, DLG-06, TCH-05 |
| 8 | **Không có nút "Xóa"** cho chứng từ tài chính; chỉ "Hủy" (khi còn chờ) hoặc "Đảo giao dịch" (khi đã hoàn tất) | D-57 |
| 9 | **Chứng từ có dòng thời gian** (ai, lúc nào, làm gì) và danh sách chứng từ liên kết (đảo, bàn giao, biên bản) | FND-06; `DocumentStatusHistory` |
| 10 | **Mobile-first cho phụ huynh và thủ quỹ**: thao tác chính hoàn thành bằng một tay, ≤ 3 bước | PAR-*, TRS-02, TRS-03 |

## 4. Phân quyền trên UI

- **Server là nơi quyết định quyền**: UI chỉ phản ánh quyền. Mọi thao tác đều được API kiểm tra lại bằng `[RequirePermission]` + phạm vi.
- Sau khi đăng nhập, UI nhận **danh sách permission theo phạm vi** (toàn trường / từng lớp / từng học sinh). Từ danh sách này UI dựng menu, ẩn / hiện nút và bảo vệ route bằng guard (permission + phạm vi).
- **Ẩn** khi không có permission; **vô hiệu kèm lý do** khi có permission nhưng nghiệp vụ chưa cho phép. Ví dụ:
  - Nút "Duyệt" bị vô hiệu với người đã lập phiếu: "Bạn là người lập phiếu này".
  - Nút "Lập phiếu chi" bị vô hiệu khi quỹ đã khóa sổ.
- **Danh tính học sinh**:
  - Phụ huynh không bao giờ thấy tên học sinh khác hay công nợ của gia đình khác. Phần thu của sổ quỹ chỉ hiện **tổng theo đợt** (D-19).
  - Báo cáo có tên học sinh hiển thị cho người có `Report.ViewStudentIdentity`, kèm dải thông báo "Lượt xem này được ghi lại" (D-46, D-52). Quản trị không có `Receipt.View` nên màn hình chi tiết phiếu thu của Quản trị **ẩn** thông tin người nộp.
- **Phạm vi dữ liệu** luôn hiển thị rõ ở thanh trên: lớp – năm học đang làm việc, hoặc con đang xem.

## 5. Phạm vi làm việc (context)

| Người dùng | Phạm vi chọn được | Mặc định |
|---|---|---|
| Admin | "Toàn trường" + (khi xem báo cáo) chọn năm học → lớp | Toàn trường |
| GVCN | Các lớp mình chủ nhiệm (theo năm học) | Lớp năm học hiện tại |
| Thủ quỹ | Lớp mình làm thủ quỹ; đồng thời là phụ huynh → thêm "Chế độ phụ huynh" | Lớp làm thủ quỹ (UQ-03) |
| Người giữ quỹ | Các lớp – năm học được giao | Lớp đầu tiên có bàn giao chờ xác nhận |
| Phụ huynh | Từng con (D-04) | Con đầu tiên (UQ-02) |

Bộ chọn phạm vi là một `kendo-dropdownlist` ở AppBar, nhóm theo vai trò (ví dụ: "GVCN · 6A / 2026-2027", "Thủ quỹ · 7B / 2026-2027", "Phụ huynh · Nguyễn An"). Khi đổi phạm vi, UI chuyển sang trang chủ của phạm vi đó.

## 6. Quy ước hiển thị

### 6.1 Tiền & ngày (D-06, NFR-05)
- Chỉ tiếng Việt. Locale `vi-VN`, dùng dữ liệu CLDR `vi` của `@progress/kendo-angular-intl`.
- **Tiền**: số nguyên, phân cách hàng nghìn bằng dấu chấm, hậu tố "đ" (ví dụ `1.250.000 đ`). Nhập bằng `kendo-numerictextbox` với `format="n0"`, `decimals=0`, `min=1`, tắt nút tăng giảm (`spinners=false`), căn phải.
- **Ngày**: `dd/MM/yyyy` (`kendo-datepicker`). Thời điểm: `dd/MM/yyyy HH:mm`.
- Số âm không xuất hiện trong số dư (DB chặn); bút toán giảm trong sổ quỹ hiển thị dấu "−" màu đỏ.

### 6.2 Khối số dư

| Khối | Nhãn | Màu nhấn (Kendo themeColor) |
|---|---|---|
| Quỹ lớp | "Quỹ lớp 6A / 2026-2027" – Số dư · Đang giữ chỗ · **Khả dụng** | `primary` |
| Công nợ | "Còn phải nộp" · "Đang chờ xác nhận" · "Còn có thể thu" | `warning` |
| Số dư cá nhân | "Số dư cá nhân của học sinh" · "Đang giữ chỗ hoàn trả" · "Chờ bàn giao" · **Khả dụng** | `tertiary` |

### 6.3 Chip trạng thái (`kendo-chip`, `fillMode="solid"`)

| Trạng thái | Nhãn tiếng Việt | themeColor |
|---|---|---|
| `PendingApproval`, `PendingConfirmation`, `Proposed` | Chờ duyệt / Chờ xác nhận / Đã đề xuất | `warning` |
| `AwaitingPayment` | Chờ thực hiện hoàn trả | `info` |
| `Approved`, `Confirmed`, `Completed`, `Resolved` | Đã duyệt / Đã xác nhận / Hoàn tất / Đã xử lý | `success` |
| `Rejected` | Bị từ chối | `error` |
| `Discrepancy`, `Open` (biên bản) | Có chênh lệch / Chưa xử lý | `error` |
| `Cancelled` | Đã hủy | `base` |
| (suy ra) có đảo giao dịch đã duyệt | Đã đảo | `base` + biểu tượng liên kết |
| `ClassFund.Closed` | Đã khóa sổ | `dark` |
| Bút toán `IsPostClose` | Sau khóa sổ | `info` (outline) |

### 6.4 Tình trạng nộp (suy ra – BR-REC-06)
Chưa nộp · Nộp một phần · Đã nộp đủ · Được miễn · Quá hạn (kết hợp với tình trạng chưa đủ).

## 7. Bố cục & component Kendo

| Thành phần | Kendo for Angular 23.3.0 | Ghi chú |
|---|---|---|
| Khung ứng dụng | `kendo-appbar` (trên) + `kendo-drawer` (menu trái; `mode="push"` trên desktop, `overlay` trên mobile) | |
| Điều hướng phụ | `kendo-breadcrumb`, `kendo-tabstrip` | |
| Danh sách | `kendo-grid` (lọc, sắp xếp, phân trang phía server, xuất Excel `kendo-grid-excel` / PDF `kendo-grid-pdf`) | Trên mobile: `kendo-listview` dạng thẻ |
| Thẻ số liệu | `kendo-card` | Dashboard, khối số dư |
| Biểu đồ | `kendo-chart` (cột: thu / chi theo tháng; tròn: chi theo danh mục) | Dashboard, báo cáo |
| Form | `kendo-formfield`, `kendo-label`, `kendo-textbox`, `kendo-textarea`, `kendo-numerictextbox`, `kendo-datepicker`, `kendo-dropdownlist`, `kendo-combobox` (tìm học sinh), `kendo-multiselect` | Lỗi hiển thị bằng `kendo-formerror` |
| Đính kèm | `kendo-fileselect` (chọn ảnh từ camera trên mobile) | Biên lai, chứng từ, minh chứng |
| Hộp thoại | `kendo-dialog` (xác nhận, lý do), `kendo-window` (form phụ trên desktop) | |
| Phản hồi | `kendo-notification` (toast), `kendo-loader`, `kendo-skeleton` | |
| Quy trình nhiều bước | `kendo-stepper` | Chốt sổ, chuyển lớp |
| Dòng thời gian | `kendo-timeline` | Lịch sử chứng từ |
| Huy hiệu | `kendo-badge` | Số việc chờ, thông báo chưa đọc |

Theme: **Kendo Theme Default ^14.3.0**. Màu thương hiệu của trường (nếu có) chỉ thay biến màu chính của theme (UQ-09); không đổi theme.

## 8. Xử lý lỗi & trạng thái

| Tình huống | UI |
|---|---|
| Đang tải | `kendo-skeleton` cho thẻ / lưới |
| Danh sách rỗng | Thông điệp + hành động chính (ví dụ "Chưa có đợt thu. Tạo đợt thu") |
| Lỗi validate (FluentValidation) | Gắn vào từng field bằng `kendo-formerror`; tóm tắt ở đầu form |
| **Xung đột** (chứng từ đã được người khác xử lý, số dư thay đổi – 409) | Hộp thoại: "Dữ liệu đã thay đổi. Tải lại để xem trạng thái mới nhất"; không tự gửi lại |
| **Vượt số khả dụng** | Thông báo tại field số tiền kèm số khả dụng hiện tại |
| Gửi trùng | Nút gửi bị khóa sau lần bấm đầu; mỗi lần mở form có một khóa chống gửi trùng (`IdempotencyKey`) |
| Không có quyền / ngoài phạm vi | COM-07 |
| Quỹ đã khóa sổ | Dải thông báo `dark` "Năm học đã khóa sổ – chỉ xem"; ẩn các hành động thường, chỉ hiện các ngoại lệ đã chốt (D-30, D-36, D-50) |

## 9. Khả năng truy cập & responsive

- WCAG 2.1 mức AA: tương phản, nhãn cho mọi field, điều hướng bằng bàn phím (Kendo hỗ trợ sẵn), không truyền tải thông tin chỉ bằng màu.
- Breakpoint: < 768 px (mobile: Drawer overlay, lưới chuyển thành danh sách thẻ, nút hành động chính cố định ở đáy), 768–1199 px (tablet), ≥ 1200 px (desktop).
- Vùng chạm tối thiểu 44 × 44 px trên mobile.

## 10. Định hướng tổ chức mã (không phải code)

Theo `training-doc.md`: `src/app/core` (auth, guard permission/phạm vi, interceptor, điều hướng), `src/app/shared` (pipe tiền / ngày, chip trạng thái, khối số dư, dòng thời gian chứng từ, ô đính kèm), `src/app/features/`:
- `auth`, `admin`, `class-fund` (đợt thu, sổ quỹ, báo cáo, hồ sơ tài chính học sinh)
- `treasurer`, `homeroom`, `custodian`, `parent`
- `documents` (chi tiết chứng từ, biên bản chênh lệch), `tasks`, `notifications`
