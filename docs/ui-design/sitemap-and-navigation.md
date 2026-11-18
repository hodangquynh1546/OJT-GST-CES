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
| `…/charges/new` | H-02 | Tạo khoản thu chung (bản thiết kế: hộp thoại mở từ H-01) | `Charge.Manage` | M6 |
| `…/charges/:chargeId` | H-01b | Theo dõi một khoản thu: lọc đã/chưa đóng, ghi nhận tiền mặt, nhắc, đóng khoản (route thêm khi làm M6) | `Charge.Manage` | M6 |
| `…/my-payments` | H-03 | Khoản cần đóng của tôi (chung + sự kiện), nộp tiền, trạng thái | `Payment.Submit` | M6 |
| `…/payments` | H-04 | Xác nhận thanh toán (hàng chờ, xem minh chứng, xác nhận/từ chối) | `Payment.Confirm` | M6 |
| `…/ledger` | L-01 | Thu chi chung (lịch sử, lọc) | `Fund.View` | M7 |
| `…/ledger/new` | L-02 | Ghi giao dịch **quỹ chung** (không chọn phạm vi): Thu = tạo khoản thu chung; Chi = khoản chi ≤ số dư, chứng từ > 500k (bản thiết kế: hộp thoại mở từ L-01) | `Ledger.Manage` | M7 |
| `…/ledger/:entryId` | L-03 | Chi tiết giao dịch + chứng từ, bút toán đảo | `Fund.View` | M7 |
| `…/events` | E-01 | Danh sách sự kiện: Đang diễn ra / Đã quyết toán – hủy; học sinh trả lời tham gia ngay trên thẻ | `Event.Participate` | M8 |
| `…/events/new` | E-02 | Tạo sự kiện (bản thiết kế: hộp thoại mở từ E-01) | `Event.Manage` | M8 |
| `…/events/:eventId` | E-03 | Chi tiết sự kiện. Thủ quỹ: tiến trình Đang mở → Đã kết thúc → Đã quyết toán, KPI, tab Người tham gia (chỉ người `Accepted`, lọc Đã đóng / Chờ xác nhận / Chưa đóng) · Khoản thu sự kiện · Thu chi sự kiện (ghi chi ≤ số dư), nút Kết thúc / Hủy / Quyết toán. Học sinh: trả lời tham gia, khoản của mình, khoản chi công khai | `Event.Participate` | M8 (+M6, M7) |
| `…/events/:eventId/settlement` | E-04 | Quyết toán sự kiện: đối chiếu, bảng chia tiền thừa theo tỷ lệ đã đóng, chọn hoàn trực tiếp / cộng số dư, đánh dấu "Đã hoàn" | `Settlement.Execute` (xem: `Event.Participate`) | M9 |
| `…/balance` | B-01 | Số dư cá nhân, lịch sử biến động, nút "Rút tiền" (rút toàn bộ) | `Balance.ViewOwn` | M10 |
| `…/withdrawals` | B-02 | Xử lý yêu cầu rút tiền | `Balance.Handle` | M10 |
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
| Xác nhận thanh toán · Yêu cầu rút tiền · Báo cáo | | ✔ | ✔ |
| Học sinh · Danh mục · Nhật ký hoạt động · Thông tin quỹ | | ✔ | ✔ |
| Thủ quỹ | | | ✔ |

Bản thiết kế nhóm menu thủ quỹ thành **Quỹ chung** (Tổng quan, Thu chi chung, Khoản thu chung, Sự kiện) · **Cá nhân** (Khoản cần đóng, Số dư cá nhân, Thông báo) · **Quản lý** (phần còn lại). Học sinh chỉ có hai nhóm đầu.

## 3. Quy tắc điều hướng

- Sau đăng nhập: mật khẩu tạm → `/change-password`; đúng 1 quỹ → `/funds/:id`; còn lại → `/funds`.
- Không thuộc quỹ → 404; thiếu quyền → 403 (guard + backend).
- 401 từ API → xóa phiên, về `/login`.
- Header hiện quỹ đang mở, badge vai trò, "Đổi quỹ" khi có nhiều quỹ hoặc được tạo quỹ.
