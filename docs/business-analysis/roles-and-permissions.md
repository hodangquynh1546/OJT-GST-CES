# Người dùng & Phân quyền

> Phiên bản: 2.0 · 2026-11-05 – mô hình **Giáo viên · Học sinh · Phụ huynh** (D-24 → D-33 trong [open-questions.md](../requirements/open-questions.md)). v1.2 (Thủ quỹ / GVCN / Phụ huynh qua bảng `FundMember` – **chưa từng triển khai**) nằm trong Git history.
>
> Trạng thái triển khai: **Partially Implemented** – đã có `RolePermissions`, `AccessService` (suy ra vai trò, mục 1), `[RequirePermission]`; chưa có phạm vi dữ liệu ở mục 4, 5. Đặc tả là Source of Truth. Xem [implementation-status.md](../implementation/implementation-status.md).

## 1. Loại tài khoản và vai trò trong quỹ

**Loại tài khoản** (`User.Kind`) cố định khi tạo, không đổi qua API:

| Loại | Đăng nhập bằng | Ai tạo | Số quỹ |
|---|---|---|---|
| `Teacher` – Giáo viên | Email | Tự đăng ký / lệnh `create-teacher` | Nhiều (quỹ mình tạo) |
| `Student` – Học sinh | Tên đăng nhập (không bắt buộc email) | GVCN cấp | Đúng một |
| `Parent` – Phụ huynh | Email | GVCN khi liên kết email với học sinh | Mọi quỹ có con được liên kết |

**Vai trò trong một quỹ** (`FundRole`) **không lưu**, backend suy ra ở mỗi request theo thứ tự:

| Thứ tự | Điều kiện (dữ liệu) | Vai trò |
|---|---|---|
| 1 | `Fund.OwnerUserId` = người dùng | **GVCN** (`Teacher`) |
| 2 | Có `Student` của quỹ với `UserId` = người dùng **và** `TreasurerAssignment` đang hiệu lực (`RevokedAt IS NULL`) | **Thủ quỹ** (`Treasurer`) |
| 3 | Có `Student` của quỹ với `UserId` = người dùng | **Học sinh** (`Student`) |
| 4 | Có `ParentLink` đang hiệu lực tới một học sinh của quỹ | **Phụ huynh** (`Parent`) |
| – | Không điều kiện nào | Không thuộc quỹ → **404** |

Phụ huynh có con là thủ quỹ vẫn chỉ là **Phụ huynh** (tài khoản phụ huynh khác tài khoản học sinh) – không có quyền thủ quỹ.

## 2. Permission

Theo `training-doc.md`, endpoint kiểm tra **permission** bằng `[RequirePermission]`, không kiểm tra tên vai trò. Bảng vai trò → permission là hằng số trong Domain (`RolePermissions`), không lưu trong DB.

| Permission | Cho phép | GVCN | Thủ quỹ | Học sinh | Phụ huynh |
|---|---|---|---|---|---|
| `Fund.Create` *(cấp tài khoản)* | Tạo quỹ | ✔ (mọi giáo viên) | – | – | – |
| `Fund.View` | Tổng quan, số dư, báo cáo, danh mục, lịch sử (theo phạm vi bên dưới) | ✔ | ✔ | ✔ | ✔ |
| `Ledger.ViewFull` | Mọi giao dịch (kể cả không công khai, đã hủy), ghi chú, lý do hủy, người thực hiện, lịch sử thay đổi | ✔ | ✔ | – | – |
| `Ledger.ViewPublic` | Chỉ khoản công khai; báo cáo bản công khai ("Khoản khác") | – | – | ✔ | ✔ |
| `Fund.Manage` | Đổi tên quỹ | ✔ | – | – | – |
| `Student.Manage` | Học sinh, cấp / cấp lại tài khoản, liên kết / gỡ phụ huynh | ✔ | – | – | – |
| `Treasurer.Assign` | Phân công / thu hồi thủ quỹ, lịch sử phân công | ✔ | – | – | – |
| `Member.View` | Danh sách thành viên (họ tên + vai trò) | ✔ | ✔ | – | – |
| `Transaction.Manage` | Thêm, sửa, hủy giao dịch | – | ✔ | – | – |
| `Category.Manage` | Thêm, đổi tên, ẩn / hiện danh mục | – | ✔ | – | – |

Không cần permission (chỉ cần đăng nhập): `GET/PUT /auth/me`, `GET /funds`, đổi mật khẩu, đăng xuất. Không cần đăng nhập: đăng nhập, đăng ký giáo viên, lấy token antiforgery.

## 3. Cách backend xác định quyền

1. Cookie phiên chỉ chứa mã người dùng và SecurityStamp; mỗi request kiểm tra tài khoản còn hoạt động và stamp còn khớp. Đổi / đặt lại mật khẩu, khóa tài khoản → phiên cũ hết hiệu lực.
2. `PermissionAuthorizationHandler` lấy `fundId` từ **đường dẫn** (`/funds/{fundId}/…`) và gọi `AccessService` để suy ra vai trò từ DB (mục 1). Endpoint không có `fundId` → kiểm tra quyền cấp tài khoản (`Fund.Create`). `fundId` chỉ chọn quỹ, **không** cấp quyền; không có tham số `teacherId` / `role` nào được tin.
3. Kết quả: không thuộc quỹ → **404** `not_found`; còn mật khẩu tạm → **403** `password.change_required`; thiếu permission → **403** `forbidden`.
4. Service tầng Application kiểm tra lại (`AccessService.RequireAsync`) – phòng thủ nhiều lớp – và áp **phạm vi dữ liệu**: `Ledger.ViewFull` → DTO đầy đủ; chỉ `Ledger.ViewPublic` → lọc `IsPublic` **trước** tìm kiếm / đếm / phân trang, DTO bỏ trường nội bộ (trường null không xuất hiện trong JSON).
5. Vai trò mới (phân công / thu hồi, liên kết / gỡ) có hiệu lực ngay ở request kế tiếp.

## 4. Phạm vi dữ liệu & quy tắc

- Học sinh, phân công, liên kết chỉ thao tác trong quỹ trên đường dẫn; Id thuộc quỹ khác → 404. Phân công thủ quỹ: ràng buộc DB `FK_TreasurerAssignment_Student_SameFund` bảo đảm học sinh cùng quỹ.
- Phân công: học sinh phải có tài khoản **đã kích hoạt** và không bị khóa; một học sinh tối đa một phân công đang hiệu lực (`UQ_TreasurerAssignment_Student_Active`).
- Thu hồi: không thu hồi được thủ quỹ cuối cùng (`treasurer.last`); khóa dòng quỹ (`FOR UPDATE`) để hai lệnh thu hồi đồng thời không cùng lọt.
- Thủ quỹ không tạo tài khoản, không phân công / thu hồi, không đổi tên quỹ.
- GVCN không thêm / sửa / hủy giao dịch, không quản lý danh mục.

## 5. Dữ liệu phụ huynh và học sinh chưa phân công không bao giờ nhận

Ghi chú nội bộ · người ghi / sửa / hủy · lý do hủy · cờ công khai · thời điểm sửa · giao dịch không công khai (nội dung, danh mục, ngày, số lượng) · lịch sử thay đổi / AuditLog · danh sách học sinh, tên đăng nhập, email, trạng thái tài khoản người khác · danh sách thành viên · danh mục ẩn hoặc chưa có khoản công khai. Khoản công khai đã hủy: nhãn "Đã hủy" + ngày hủy (D-18).

## 6. Lịch sử thay đổi

| Ngày | Phiên bản | Thay đổi |
|---|---|---|
| 2026-11-04 | 1.1 / 1.2 | 3 vai trò Thủ quỹ / GVCN / Phụ huynh qua `FundMember` (thiết kế, chưa triển khai) |
| 2026-11-05 | 2.0 | Viết lại theo D-24 → D-33: loại tài khoản, vai trò suy ra, 10 permission, 404 / 403 có mã, phân công thủ quỹ |
