# Sitemap, điều hướng & danh sách màn hình

> Route khai báo ở `ClassFundUI/src/app/app.routes.ts`; mỗi module thay màn hình giữ chỗ trong `features/<module>/<module>.routes.ts`.
> Menu sinh từ permission (`core/navigation/nav-items.ts`).

## 1. Route

| Route | Mã | Màn hình | Guard / permission | Module |
|---|---|---|---|---|
| `/login` | A-01 | Đăng nhập (email + mật khẩu) | guest | M1 |
| `/register` | A-02 | Đăng ký thủ quỹ | guest | M1 |
| `/change-password` | A-03 | Đặt mật khẩu mới (bắt buộc khi mật khẩu tạm; đổi tự nguyện ở A-04) | đăng nhập | M1 |
| `/account` | A-04 | Tài khoản của tôi (thông tin cá nhân + đổi mật khẩu; **ngoài khung menu quỹ**, chỉ có thanh trên: Quỹ của tôi, Đăng xuất – theo canvas) | đăng nhập | M1 |
| `/funds` | F-01 | Quỹ của tôi (danh sách, nút Tạo quỹ nếu `Fund.Create`) | đăng nhập | M2 |
| `/funds/new` | F-02 | Tạo quỹ | `Fund.Create` (kiểm ở màn hình) | M2 |
| `/funds/:fundId` | S-01 | Tổng quan quỹ chung | `Fund.View` | M13 |
| `…/settings` | F-03 | Thông tin quỹ (đổi tên) | `Fund.Manage` | M2 |
| `…/members` | M-01 | Học sinh: danh sách, thêm bằng email, cấp mật khẩu tạm, ngừng | `Member.Manage` | M3 |
| `…/treasurers` | T-01 | Thủ quỹ: phân quyền / thu hồi | `Treasurer.Manage` | M4 |
| `…/categories` | C-01 | Danh mục thu chi | `Category.Manage` | M5 |
| `…/charges` | H-01 | Khoản thu chung: danh sách Đang mở / Đã đóng (học sinh: kèm tình trạng của mình) | `Fund.View` | M6 |
| `…/charges/new` | H-02 | Tạo khoản thu chung – **không có hạn đóng** (bản thiết kế: hộp thoại mở từ H-01) | `Charge.Manage` | M6 |
| `…/charges/:chargeId` | H-01b | Theo dõi một khoản thu: lọc Đã nộp / Chưa nộp; thủ quỹ tích **"Đã thu"** (chỉ Tiền mặt / Chuyển khoản), "Bỏ xác nhận thu", "Nhắc đóng" từng người, đóng / hủy khoản (cảnh báo khi còn người chưa nộp). Dùng lại cho khoản thu của sự kiện (tab Khoản thu sự kiện ở E-03) | `Charge.Manage` | M6 |
| `…/my-payments` | H-03 | Khoản cần đóng của tôi (chung + sự kiện) – **chỉ xem** Đã nộp / Chưa nộp | `Fund.View` | M6 |
| ~~`…/payments`~~ | ~~H-04~~ | **Đã bỏ** (2026-11-23): không còn hàng chờ xác nhận – thủ quỹ tích "đã thu" ở H-01b / E-03 | – | – |
| `…/ledger` | L-01 | Thu chi chung (lịch sử, lọc) | `Fund.View` | M7 |
| `…/ledger/new` | L-02 | Ghi giao dịch **quỹ chung** (không chọn phạm vi): Thu = tạo khoản thu chung; Chi = khoản chi ≤ số dư, chứng từ > 500k (bản thiết kế: hộp thoại mở từ L-01) | `Ledger.Manage` | M7 |
| `…/ledger/:entryId` | L-03 | Chi tiết giao dịch + chứng từ, bút toán đảo | `Fund.View` | M7 |
| `…/events` | E-01 | Danh sách sự kiện: Đang diễn ra / Đã quyết toán – hủy; học sinh trả lời tham gia ngay trên thẻ | `Event.Participate` | M8 |
| `…/events/new` | E-02 | Tạo sự kiện: tên, mô tả, ngày diễn ra, **hạn đóng danh sách** (bản thiết kế: hộp thoại mở từ E-01) | `Event.Manage` | M8 |
| `…/events/:eventId` | E-03 | Chi tiết sự kiện. Thủ quỹ: tiến trình Đang mở → Đã đóng danh sách → Đã quyết toán, KPI, tab Người tham gia (chỉ người `Accepted`, lọc Đã nộp / Chưa nộp, nhắc đóng) · Khoản thu sự kiện (danh sách người nộp, tích "đã thu") · Thu chi sự kiện (chỉ ghi chi sau khi đóng danh sách, ≤ số dư), nút **Đóng danh sách** (xem trước người bị loại và tiền hoàn, xác nhận đóng sớm) / Hủy / Quyết toán, mục **Lịch sử đóng danh sách**. Học sinh: trả lời tham gia, khoản của mình, khoản chi công khai | `Event.Participate` | M8 (+M6, M7) |
| `…/events/:eventId/settlement` | E-04 | Quyết toán sự kiện: đối chiếu, bảng chia đều tiền thừa theo đầu người, chọn hoàn trực tiếp / cộng số dư, đánh dấu "Đã hoàn" | `Settlement.Execute` (xem: `Event.Participate`) | M9 |
| `…/balance` | B-01 | Số dư cá nhân và lịch sử biến động – **chỉ xem** (gồm "Hoàn khi đóng danh sách") | `Balance.ViewOwn` | M10 |
| `…/withdrawals` | B-02 | **Rút số dư**: mọi học sinh kèm số dư, nút "Rút tiền" (nhập số tiền, kiểm FE + BE), lịch sử rút | `Balance.Handle` | M10 |
| `…/notifications` | N-01 | Thông báo | `Fund.View` | M11 |
| `…/audit` | U-01 | Nhật ký hoạt động | `Audit.View` | M12 |
| `…/reports` | S-02 | Báo cáo thu chi chung | `Report.View` | M13 |
| `…/reports/events/:eventId` | S-03 | Tổng quan sự kiện | `Report.View` | M13 |
| `…/reports/students` | S-04 | Báo cáo học sinh | `Report.View` | M13 |
| `…/reports/year-end` | S-05 | Tổng kết & quyết toán cuối năm quỹ chung (một lần) | `Report.View` (quyết toán: `Settlement.Execute`) | M13 + M14 |
| `/forbidden`, `**` | – | 403 / 404 | – | Foundation |

## 2. Menu theo vai trò

| Mục menu | Học sinh | Thủ quỹ | Thủ quỹ chính |
|---|:-:|:-:|:-:|
| Quỹ của tôi | ✔ | ✔ | ✔ |
| Tổng quan quỹ chung · Thu chi chung · Khoản thu chung | ✔ | ✔ | ✔ |
| Sự kiện | ✔ | ✔ | ✔ |
| Khoản cần đóng của tôi · Số dư cá nhân · Thông báo | ✔ | ✔ | ✔ |
| Rút số dư · Báo cáo | | ✔ | ✔ |
| Học sinh · Danh mục · Nhật ký hoạt động · Thông tin quỹ | | ✔ | ✔ |
| Thủ quỹ | | | ✔ |

Bản thiết kế nhóm menu thủ quỹ thành **Quỹ chung** (Tổng quan, Thu chi chung, Khoản thu chung, Sự kiện) · **Cá nhân** (Khoản cần đóng, Số dư cá nhân, Thông báo) · **Quản lý** (phần còn lại). Học sinh chỉ có hai nhóm đầu.

## 3. Quy tắc điều hướng

- Sau đăng nhập: mật khẩu tạm → `/change-password`; còn lại **luôn → `/funds` (F-01 Quỹ của tôi, không có menu quỹ, kể cả khi chỉ có một quỹ)**. Chọn một quỹ mới vào `/funds/:id` và hiện menu quỹ (theo canvas "ClassFund UI Design"). F-01, F-02 và A-04 nằm ngoài khung menu, chỉ có thanh trên (logo, Đăng xuất, ảnh đại diện).
- Không thuộc quỹ → 404; thiếu quyền → 403 (guard + backend).
- 401 từ API → xóa phiên, về `/login`.
- Header hiện quỹ đang mở, badge vai trò, "Đổi quỹ" khi có nhiều quỹ hoặc được tạo quỹ.
