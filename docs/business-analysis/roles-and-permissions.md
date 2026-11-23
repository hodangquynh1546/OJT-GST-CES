# Vai trò & phân quyền

> Nguồn sự thật trong code: `ClassFundAPI/src/ClassFund.Domain/Authorization/Permissions.cs` (`RolePermissions`) và
> `ClassFundUI/src/app/core/auth/permissions.ts`. Sửa ma trận ở đây thì phải sửa cả hai file + test `RolePermissionsTests`.

## 1. Vai trò

Vai trò gắn với **từng quỹ** (bảng `FundMember.Role`); một người có thể là Owner ở quỹ này và học sinh ở quỹ khác.

| Vai trò | Mã | Có được bằng cách | Ghi chú |
|---|---|---|---|
| Thủ quỹ chính | `Owner` | Tạo quỹ (tài khoản tự đăng ký) | Đúng 1 Owner / quỹ (unique index). Toàn quyền. |
| Thủ quỹ | `Treasurer` | Owner phân quyền một học sinh của quỹ | Mọi nghiệp vụ quản lý quỹ, **trừ** quản lý thủ quỹ khác. |
| Học sinh | `Student` | Thủ quỹ thêm bằng email | Xem công khai + dữ liệu của mình (Đã nộp / Chưa nộp, số dư), tham gia sự kiện. **Không** tự nộp/xác nhận/rút tiền – việc đó do thủ quỹ ghi nhận. |

Thủ quỹ (Owner/Treasurer) **có đủ quyền học sinh**: được mời vào sự kiện, có nghĩa vụ đóng tiền, có số dư cá nhân.

Quyền cấp tài khoản (không gắn quỹ): `Fund.Create` – chỉ tài khoản `SelfRegistered` (OQ-07).

## 2. Ma trận permission

| Permission | Ý nghĩa | Owner | Treasurer | Student |
|---|---|:-:|:-:|:-:|
| `Fund.View` | Xem thông tin công khai quỹ, tổng quan quỹ chung, thông báo | ✔ | ✔ | ✔ |
| `Fund.Manage` | Đổi tên / cập nhật quỹ | ✔ | ✔ | |
| `Member.Manage` | Thêm học sinh bằng email, cấp mật khẩu tạm, sửa, ngừng | ✔ | ✔ | |
| `Treasurer.Manage` | Phân quyền / thu hồi thủ quỹ | ✔ | | |
| `Category.Manage` | Quản lý danh mục | ✔ | ✔ | |
| `Event.Manage` | Tạo/sửa/hủy, đóng danh sách sự kiện | ✔ | ✔ | |
| `Event.Participate` | Xem sự kiện được mời, xác nhận tham gia | ✔ | ✔ | ✔ |
| `Charge.Manage` | Tạo/đóng/hủy khoản thu (chung, sự kiện), nhắc đóng | ✔ | ✔ | |
| `Ledger.Manage` | Ghi thu/chi, bút toán đảo, đính chứng từ | ✔ | ✔ | |
| ~~`Payment.Submit`~~ | **Bỏ** (2026-11-23) – học sinh không còn tự nộp | | | |
| `Payment.Confirm` | Tích "đã thu" (Tiền mặt / Chuyển khoản) / bỏ xác nhận thu; tự ghi nhận cho bản thân; tải minh chứng chuyển khoản | ✔ | ✔ | |
| `Settlement.Execute` | Quyết toán sự kiện, xác nhận hoàn tiền | ✔ | ✔ | |
| `Balance.ViewOwn` | Xem số dư & lịch sử của mình | ✔ | ✔ | ✔ |
| ~~`Balance.Withdraw`~~ | **Bỏ** (2026-11-23) – học sinh chỉ xem số dư | | | |
| `Balance.Handle` | "Rút số dư" cho học sinh (nhập số tiền), xem lịch sử rút | ✔ | ✔ | |
| `Report.View` | Báo cáo đầy đủ (học sinh, sự kiện, cuối năm) | ✔ | ✔ | |
| `Audit.View` | Xem nhật ký hoạt động | ✔ | ✔ | |

## 3. Quy tắc kiểm tra quyền (backend)

Thứ tự trong `PermissionAuthorizationHandler` cho route có `{fundId}`:

1. Chưa đăng nhập / phiên hết hiệu lực (SecurityStamp đổi, tài khoản bị khóa) → **401** `unauthorized`.
2. Không phải thành viên `Active` của quỹ (hoặc quỹ không tồn tại) → **404** `not_found` (không lộ sự tồn tại của quỹ).
3. Còn mật khẩu tạm → **403** `password.change_required`.
4. Thiếu permission → **403** `forbidden`.

Quyền phụ thuộc **dữ liệu** (không thể diễn đạt bằng permission) kiểm tra trong service, ví dụ:

| Mã | Quy tắc | Module |
|---|---|---|
| AZ-01 | Học sinh chỉ xem nghĩa vụ, thanh toán, số dư **của chính mình**. Truy cập của người khác → 404. | M6, M10 |
| AZ-02 | Treasurer không được tác động lên FundMember có role `Owner`/`Treasurer` (sửa, ngừng, cấp mật khẩu). Chỉ Owner. | M3, M4 |
| AZ-03 | Owner không thể tự thu hồi / tự ngừng chính mình. | M4 |
| AZ-04 | Tích "đã thu": người tích == người nộp ⇒ `SelfDeclared` (chỉ khi có `Payment.Confirm`); khác ⇒ `Verified`. Dùng `PaymentConfirmationPolicy`. | M6 |
| AZ-05 | Học sinh không xem minh chứng thanh toán của học sinh khác (FR-17). | M5 |
| AZ-06 | Học sinh chỉ thấy sự kiện mình được mời và khoản thu sự kiện mình tham gia. | M6, M8 |
| AZ-07 | Chỉ sửa họ tên / cấp lại mật khẩu tạm cho tài khoản **do thủ quỹ cấp (`Provisioned`) và không còn là thành viên hoạt động của quỹ nào khác**; tài khoản tự đăng ký hoặc đang ở quỹ khác thì từ chối 422 `member.account_not_managed` (họ tự đổi ở trang Tài khoản). Ngăn thủ quỹ một quỹ chiếm tài khoản của người ở quỹ khác. Không tự thao tác trên chính mình: 422 `member.cannot_act_on_self`. *(đề xuất của M3, chờ nhóm xác nhận)* | M3 |

## 4. Phía frontend

- `CurrentUser.memberships[].permissions` (từ `GET /auth/me`) quyết định menu (`nav-items.ts`), guard (`fundPermissionGuard`) và `*appHasPermission`.
- Frontend chỉ ẩn/hiện; backend luôn kiểm tra lại.
