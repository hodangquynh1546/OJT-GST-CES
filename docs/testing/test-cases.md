# Test cases – ClassFund 2.0 (Giáo viên · Học sinh · Phụ huynh)

> v2.1 · 2026-11-09 · Mục **A** là bộ test case hiện hành (D-24 → D-34) – **đặc tả**, căn cứ viết test khi làm từng module. Cột **Module** cho biết module nào chịu trách nhiệm ([module-plan.md](../implementation/module-plan.md)).
> **Hiện trạng:** chỉ có Foundation (M0) – mọi case nghiệp vụ là `Not Implemented`. Cột "Test (bản trước, tham khảo)" ghi tên test của bản cài đặt trước (đã bỏ khỏi codebase ngày 2026-11-09, xem tại commit `6b6645d`) – dùng để tham khảo khi viết lại, không phải kết quả hiện tại.
> Mục 0 → 10 bên dưới là đặc tả R2 (v1.1 / v1.2, `FundMember`, `/members`, đổi vai trò) – **không dùng**, giữ để lưu lịch sử.
>
> Trạng thái: `Not Implemented` (chưa có test tự động) · `Partially Implemented` (mới phủ một phần – ghi rõ phần thiếu) · `Pending Decision` · `Passed` / `Failed` (kết quả test tự động trên codebase hiện tại, ghi tên test).
> **Quy ước khi làm module:** mỗi case thành ít nhất một test tự động (integration cho API, e2e cho luồng UI); tên test viết bằng tiếng Anh mô tả hành vi; khi đạt, đổi cột Trạng thái thành `Passed – <Lớp test>.<tên test>`. Case mới phát sinh: thêm dòng với mã kế tiếp trong nhóm (T-, S-, TR-, P-, G-, R-).

## Kết quả hiện tại

| Ngày | Phạm vi | Kết quả |
|---|---|---|
| 2026-11-09 | Foundation (M0) – `Foundation/FoundationTests`, `Contracts/ApiContractTests` + unit nền | Build 0 warning · Unit 102 / 102 · Integration 19 / 19 Passed · Frontend unit 42 / 42 · E2E mock 9 / 9 · E2E full 3 / 3 |
| 2026-11-10 | M1 Auth & Tài khoản – `Auth/AuthTests`, `AuthServiceTests`, `auth.service.spec`, `e2e/mock/auth.spec`, `e2e/full/auth.spec` (T-01, T-02, S-03, S-04, G-03, G-05) | Build 0 warning · Unit 116 / 116 · Integration 35 / 35 · Frontend unit 54 / 54 · E2E mock 29 / 29 · E2E full 6 / 6 |

Chi tiết: [implementation-status.md](../implementation/implementation-status.md) mục 3a.

## Historical Test Results

| Ngày | Phạm vi | Kết quả | Ghi chú |
|---|---|---|---|
| 2026-11-04 | R1 – một thủ quỹ / quỹ | Unit 45 / 45 · Integration 53 / 53 | Mã nguồn đã xóa |
| 2026-11-05 | R3 – bộ test mục A (trừ X-01, X-02) | Build 0 warning · Unit 57 / 57 · Integration 67 / 67 (PostgreSQL 16 – Testcontainers) | Mã nguồn đã xóa ngày 2026-11-05 |
| 2026-11-05 → 06 | Bản cài đặt backend theo feature (Auth, Funds, Students, Treasurers, Categories, Transactions, Overview, Reports, Members) | Build 0 warning · Unit 302 / 302 · Integration 92 / 92 | Đã bỏ khỏi codebase 2026-11-09 (`6b6645d`) – làm lại theo module |

## A. Bộ test 2.0

Lớp test: `TS` = `Roles/TeacherStudentTreasurerTests`, `PP` = `Roles/ParentAndPrivacyTests`, `TX` = `Fund/TransactionTests`, `RC` = `Fund/ReportAndCategoryTests`, `UP` = `Database/UpgradeMigrationTests`, `PT` = `Database/ProtectionTriggerTests`, `U` = Unit test.

### A.1 Giáo viên
| Mã | Tình huống | Kỳ vọng | Test (bản trước, tham khảo) | Module | Trạng thái |
|---|---|---|---|---|---|
| T-01 | Đăng ký: xác nhận mật khẩu sai / mật khẩu yếu / email trùng (khác hoa thường) | 400 `validation_failed` / 422 `auth.password_policy` / 422 `user.email_taken` | TS `Teacher_registers…` | M1 | Passed – `AuthTests.Register_rejects_mismatched_confirmation_weak_password_and_taken_email` |
| T-02 | Đăng ký gửi kèm `kind`, `role = Treasurer` | Vẫn là `Teacher`, `accountPermissions = [Fund.Create]`, không có quỹ | TS `Teacher_registers…` | M1 | Passed – `AuthTests.Teacher_registers_and_is_signed_in_as_teacher` |
| T-03 | Tạo 2 quỹ | `GET /funds`: 2 quỹ, vai trò Teacher, `Unassigned`, 0 học sinh, có danh mục mặc định | TS `Teacher_registers…` | M2 | Passed – `Funds/FundsTests.Teacher_creates_two_funds_with_default_categories_unassigned_and_no_students` |
| T-04 | GVCN thêm giao dịch / thêm danh mục | 403 `forbidden` | TS `Teacher_registers…` | M5, M6 | Not Implemented |
| T-05 | Giáo viên B xem / thêm học sinh / thu hồi / đổi tên quỹ của giáo viên A; Id sai định dạng | 404 `not_found`; `GET /funds` của B rỗng | TS `Teacher_cannot_see…` | M2, M3, M4 | Partially Implemented – M2 Passed (`Funds/FundsTests.Teacher_cannot_see_or_rename_another_teachers_fund`); phần học sinh / thu hồi chờ M3, M4 |
| T-06 | GVCN xem chi tiết: ghi chú, người ghi, lý do hủy, người hủy, lịch sử thay đổi | Có đủ | TS `Treasurer_manages…`, PP `Parent_sees…` | M6 | Not Implemented |

### A.2 Học sinh
| Mã | Tình huống | Kỳ vọng | Test (bản trước, tham khảo) | Module | Trạng thái |
|---|---|---|---|---|---|
| S-01 | Thêm học sinh → chi tiết | `accountStatus = None` | TS `Student_account…` | M3 | Not Implemented |
| S-02 | Cấp tài khoản | Username gợi ý `huy.pham.NNN`; mật khẩu tạm dạng `Xxxx-xxxx-0000-xxxx`; cấp lần 2 → 422 `student.account_exists`; trạng thái `PendingActivation` | TS `Student_account…`, U `CredentialsTests` | M3 | Not Implemented |
| S-03 | Học sinh đăng nhập bằng mật khẩu tạm, gọi `/funds`, `/funds/{id}` | 403 `password.change_required`; `me.mustChangePassword = true` | TS `Student_account…` | M1 | Passed – `AuthTests.Temporary_password_blocks_other_endpoints_until_changed` (endpoint thử của test; `/funds` kiểm lại khi có M2) |
| S-04 | Đổi mật khẩu | Dùng được; mật khẩu tạm cũ → 401 | TS `Student_account…` | M1 | Passed – `AuthTests.Temporary_password_blocks_other_endpoints_until_changed` |
| S-05 | Mật khẩu tạm trong AuditLog / log | Không có | TS `Student_account…` | M3 | Not Implemented |
| S-06 | GVCN cấp lại mật khẩu | Phiên cũ 401; đăng nhập lại phải đổi mật khẩu | TS `Student_account…` | M3 | Not Implemented |
| S-07 | Học sinh chưa phân công: tổng quan, danh sách | Số dư gồm khoản không công khai; chỉ khoản công khai; không `note`, `createdByName` | TS `Unassigned_student…` | M6, M7 | Not Implemented |
| S-08 | Học sinh chưa phân công ghi thu chi / danh mục / xem thành viên / học sinh / tạo quỹ | 403 `forbidden` | TS `Unassigned_student…` | M2 → M6 | Partially Implemented – tạo quỹ 403 Passed (`Funds/FundsTests.Treasurer_student_and_parent_cannot_create_or_rename_fund`); phần còn lại chờ M3 → M6 |
| S-09 | Danh sách học sinh | Có username, `isTreasurer` | TS `Student_account…` | M3 | Not Implemented |

### A.3 Thủ quỹ
| Mã | Tình huống | Kỳ vọng | Test (bản trước, tham khảo) | Module | Trạng thái |
|---|---|---|---|---|---|
| TR-01 | Phân công học sinh chưa kích hoạt / chưa có tài khoản / đang là thủ quỹ / thuộc quỹ khác | 422 `treasurer.account_not_activated` / `treasurer.no_account` / `treasurer.already_assigned`; 404 | TS `Student_account…`, `Changing_treasurer…` | M4 | Not Implemented |
| TR-02 | Thủ quỹ thêm thu chi, danh mục; xem thành viên (chỉ họ tên + vai trò) | 201; members chỉ 2 trường | TS `Treasurer_manages…` | M5, M6 | Not Implemented |
| TR-03 | Thủ quỹ xem / thêm học sinh, phân công, thu hồi, đổi tên quỹ, tạo quỹ | 403 `forbidden` | TS `Treasurer_manages…` | M2, M3, M4 | Partially Implemented – M2 Passed (`Funds/FundsTests.Treasurer_student_and_parent_cannot_create_or_rename_fund`); phần học sinh / phân công / thu hồi chờ M3, M4 |
| TR-04 | Thu hồi thủ quỹ duy nhất | 422 `treasurer.last` | TS `Changing_treasurer…` | M4 | Not Implemented |
| TR-05 | Phân công người mới rồi thu hồi người cũ | 204; người cũ ghi → 403 ngay request kế tiếp; người mới ghi được; lịch sử có người phân công / thu hồi; AuditLog `Treasurer.Revoke` | TS `Changing_treasurer…` | M4 | Not Implemented |
| TR-06 | Thu hồi lại phân công đã thu hồi | 409 `treasurer.already_revoked` | TS `Changing_treasurer…` | M4 | Not Implemented |
| TR-07 | Hai lệnh thu hồi đồng thời (2 thủ quỹ) | Đúng 1 thành công; còn 1 thủ quỹ | TS `Concurrent_revokes…` | M4 | Not Implemented |
| TR-08 | Hủy không lý do / có lý do | 400 / 200 + `cancelledByName` | PP `Cancelling_requires…`, U | M6 | Not Implemented |

### A.4 Phụ huynh
| Mã | Tình huống | Kỳ vọng | Test (bản trước, tham khảo) | Module | Trạng thái |
|---|---|---|---|---|---|
| P-01 | Thêm học sinh kèm email phụ huynh mới | `accountCreated = true`, `emailSent = false`, có mật khẩu tạm | PP `Parent_account…` | M3 | Not Implemented |
| P-02 | Cùng email (khác hoa thường) ở quỹ khác | Chỉ liên kết; `credential = null`; 1 tài khoản | PP `Parent_account…` | M3 | Not Implemented |
| P-03 | Liên kết thứ hai cho cùng học sinh; email của giáo viên | 422 `parent_link.exists`; 422 `parent.email_in_use` | PP `Parent_account…` | M3 | Not Implemented |
| P-04 | Phụ huynh xem `/funds` | 2 quỹ, vai trò Parent, tên con, không `studentCount` | PP `Parent_account…` | M2, M3 | Passed – `Funds/FundsTests.Parent_sees_child_funds_without_student_count` (liên kết phụ huynh seed bằng DB, chờ M3 cho API) |
| P-05 | Gỡ liên kết | Quỹ đó → 404; quỹ kia vẫn 200 | PP `Parent_account…` | M3 | Not Implemented |
| P-06 | Danh sách, tìm kiếm theo nội dung / ghi chú khoản không công khai, chi tiết khoản không công khai, lịch sử | Không có trường nội bộ; tìm → 0; chi tiết → 404; lịch sử → 403 | PP `Parent_sees…` | M6 | Not Implemented |
| P-07 | Danh mục; báo cáo tháng | Không có danh mục chỉ dùng cho khoản không công khai; "Khoản khác" `count = null`; tổng chi tháng đầy đủ | PP `Parent_sees…` | M5, M7 | Not Implemented |
| P-08 | Con là thủ quỹ – phụ huynh ghi / hủy / xem thành viên / học sinh / tạo quỹ | 403 | PP `Parent_sees…` | M2 → M6 | Partially Implemented – tạo quỹ 403 Passed (`Funds/FundsTests.Treasurer_student_and_parent_cannot_create_or_rename_fund`); phần còn lại chờ M3 → M6 |

### A.5 Phân quyền chung, migration, regression
| Mã | Tình huống | Kỳ vọng | Test (bản trước, tham khảo) | Module | Trạng thái |
|---|---|---|---|---|---|
| G-01 | Ma trận permission theo vai trò; chỉ giáo viên tạo quỹ | Đúng bảng `roles-and-permissions.md` | `RolePermissionsTests`, `AccessServiceTests` | M0 | Passed – `RolePermissionsTests`, `AccessServiceTests`, `AccessServiceMembershipTests`, `PermissionAuthorizationHandlerTests` (Foundation) |
| G-02 | Thiếu header antiforgery khi ghi | 400 | RC `Treasurer_signs_in…` | M0 | Passed – `FoundationTests.Write_without_antiforgery_header_is_rejected` (Foundation) |
| G-03 | Đổi mật khẩu / khóa tài khoản → phiên cũ | 401 | RC `Treasurer_signs_in…` | M0, M1 | Passed – `FoundationTests.Disabled_account_session_is_treated_as_signed_out` (khóa), `AuthTests.Changing_password_ends_every_other_session` (đổi mật khẩu) |
| G-04 | Thủ quỹ quỹ B dùng Id giao dịch / đường dẫn quỹ A | 404 | TX `A_treasurer_cannot…` | M6 | Not Implemented |
| G-05 | Đăng nhập bằng một ô `login`: email giáo viên / phụ huynh, tên đăng nhập học sinh; request không có tham số vai trò; gửi kèm `role` bị bỏ qua (D-34) | 200 với mọi loại tài khoản; `/auth/me` trả loại tài khoản + vai trò theo quỹ | – (mới) | M1 | Passed – `AuthTests.Teacher_signs_in_with_email_case_insensitively_without_role_parameter`, `Student_signs_in_with_username_and_must_change_temporary_password`, `Me_lists_memberships_with_roles_derived_from_data` |
| M-01 | Nâng cấp DB `InitialClassFund` có dữ liệu | Owner → Teacher, không phân công, giao dịch / AuditLog giữ nguyên, `IsPublic = true`, `CancelledBy` từ AuditLog | UP | – | Không áp dụng (D-36) |
| M-02 | DELETE Student / ParentLink / TreasurerAssignment bằng `app_user` | P0001 | PT | M3, M4 | Not Implemented |
| M-03 | Lịch sử migration có `_InitialClassFund`, `_AddDatabaseProtections` *(sửa 2026-11-05, D-36)* | Có | `DatabaseAccountTests` | M0 | Not Implemented – migration có trong Foundation, chưa có test tự động |
| R-01 | Thu chi, sửa, hủy, chống trùng (5 request đồng thời), lọc, báo cáo tháng / năm, danh mục ẩn | Như R1 | TX, RC | M6, M7 | Not Implemented |
| X-01 | Khởi động API bằng `dotnet run` và thao tác thủ công (smoke test) | Đăng ký → tạo quỹ → học sinh → phân công → ghi thu chi | – | M8 | Not Implemented |
| X-02 | Phụ huynh quên mật khẩu *(sửa 2026-11-09, D-41: bỏ ~~GVCN khóa học sinh, học sinh rời lớp, chuyển quỹ~~)* | – | – | – | Pending Decision (PD-02) |

---

# Đặc tả R2 (v1.1 / v1.2) – không triển khai, giữ lịch sử

> 2026-11-04 · Đặc tả cho R2. Chưa có code; mỗi test case sẽ thành integration test (PostgreSQL thật, Testcontainers) hoặc unit test (Domain).
> Nguồn: [roles-and-permissions.md](../business-analysis/roles-and-permissions.md), [business-rules-and-processes.md](../business-analysis/business-rules-and-processes.md), [backend-api.md](../api/backend-api.md).

## 0. Dữ liệu chuẩn (seed)

| Đối tượng | Nội dung |
|---|---|
| Quỹ A | Thủ quỹ **T1**, **T2** (hoạt động); GVCN **G1**; phụ huynh **P1**; phụ huynh **P2** (đã khóa); thành viên **N1** (vừa tạo, mật khẩu tạm) |
| Quỹ B | Thủ quỹ **TB**, phụ huynh **PB** |
| Giao dịch quỹ A, tháng 11 | X1 thu 3.200.000 công khai · X2 chi 180.000 công khai · X3 chi 1.250.000 công khai (T2 đã sửa) · X4 chi 500.000 **không công khai**, ghi chú "Hoa và phong bì" · X5 thu 300.000 **không công khai** · X6 chi 250.000 công khai, **đã hủy**, lý do "Nhập nhầm" · X7 chi 90.000 không công khai, **đã hủy** |
| Danh mục quỹ A | "Khen thưởng" chỉ dùng cho X4; "Photo" **đã ẩn** |

Ký hiệu kết quả: 200/201/204 thành công · 401 chưa đăng nhập · 403 thiếu quyền · 404 không tồn tại / ngoài phạm vi · 409 xung đột · 422 vi phạm quy tắc.

## 1. Phân quyền (AUTHZ)

| Mã | Tình huống | Kỳ vọng |
|---|---|---|
| AUTHZ-01 | T1, G1, P1 gọi `GET /fund`, `/fund/reports/yearly`, `/categories` | 200 với cả 3 |
| AUTHZ-02 | Ẩn danh gọi mọi endpoint nghiệp vụ | 401 |
| AUTHZ-03 | G1 gọi `POST /transactions`, `PUT /transactions/X1`, `POST /transactions/X1/cancel` | 403; DB không đổi; không có `AuditLog` mới |
| AUTHZ-04 | P1 gọi các endpoint ghi ở AUTHZ-03 | 403 |
| AUTHZ-05 | G1, P1 gọi `POST /categories`, `PUT /categories/{id}`, `PUT /fund` | 403 |
| AUTHZ-06 | G1, P1 gọi `GET /members`, `POST /members`, `PUT /members/{id}/role`, reset / disable / enable | 403 |
| AUTHZ-07 | P1 gọi `GET /transactions/X1/history` | 403. G1 → 200 |
| AUTHZ-08 | T1 gửi `fundId` của quỹ B (query, body, header) khi gọi `/transactions` | Bị bỏ qua: vẫn chỉ dữ liệu quỹ A |
| AUTHZ-09 | P1 gửi `role=Treasurer` / `permissions=[...]` trong body / header | Bị bỏ qua: vẫn 403 khi ghi |
| AUTHZ-10 | `GET /auth/me` của T1, G1, P1 | `role` và `permissions` khớp ma trận (T: 6 permission, G: Fund.View + Ledger.ViewFull, P: Fund.View + Ledger.ViewPublic) |
| AUTHZ-11 | Unit: `RolePermissions.For(role)` cho 3 vai trò | Đúng ma trận; vai trò không hợp lệ → lỗi |
| AUTHZ-12 | T1 đổi G1 thành Phụ huynh; G1 (đang có phiên) gọi ngay `GET /transactions?status=Cancelled` | Request kế tiếp đã theo quyền Phụ huynh (không thấy X7, không có ghi chú) – không cần đăng nhập lại |

## 2. Cô lập giữa các quỹ (ISO)

| Mã | Tình huống | Kỳ vọng |
|---|---|---|
| ISO-01 | TB gọi `GET/PUT /transactions/X1`, `/cancel`, `/history` | 404 (không phải 403) |
| ISO-02 | TB gọi `PUT /members/{G1}/role`, reset / disable G1 | 404 |
| ISO-03 | PB gọi `GET /transactions/X1` | 404 |
| ISO-04 | TB tạo giao dịch dùng `categoryId` của quỹ A | 422 `transaction.category_invalid` (FK ghép chặn ở DB) |
| ISO-05 | `GET /fund`, `/fund/reports/*` của TB | Không chứa số liệu quỹ A |

## 3. Rò rỉ dữ liệu không công khai (LEAK) – áp dụng cho P1

| Mã | Tình huống | Kỳ vọng |
|---|---|---|
| LEAK-01 | `GET /transactions` (mặc định) | Chỉ X1, X2, X3. Không có X4, X5, X7. Response không có các field `note`, `createdBy`, `updatedBy`, `cancelledBy`, `cancelReason`, `isPublic` |
| LEAK-02 | `GET /transactions?status=Cancelled` | Chỉ X6, `isCancelled=true`, có `cancelledOn`; **không** lý do / người hủy. Không có X7 |
| LEAK-03 | `GET /transactions?status=All` | X1, X2, X3, X6 |
| LEAK-04 | `GET /transactions/X4`, `/X5`, `/X7` | 404, body giống hệt 404 của một id ngẫu nhiên không tồn tại |
| LEAK-05 | `GET /transactions?search=phong bì` và `?search=Hoa` | 0 kết quả (không tìm trong ghi chú / khoản ẩn) |
| LEAK-06 | `GET /transactions?pageSize=1` | `total` = 3 (đếm sau khi lọc công khai), không phải 6 |
| LEAK-07 | `GET /transactions?categoryId={Khen thưởng}` | 0 kết quả, không lỗi khác biệt |
| LEAK-08 | `GET /categories` | Không có "Khen thưởng" (chưa có khoản công khai) và "Photo" (đã ẩn); không có field `isHidden` (D-22) |
| LEAK-09 | `GET /fund/reports/monthly?year=2026&month=11` | Danh sách chỉ khoản công khai đang hiệu lực. `byCategory` có dòng "Khoản khác" mỗi loại `{amount}`, **không** `count`, không ngày / danh mục. Không có tên "Khen thưởng" |
| LEAK-10 | Tổng của LEAK-09: Σ dòng thu = tổng thu tháng; Σ dòng chi = tổng chi tháng; = báo cáo của T1 | Khớp (BR-15) |
| LEAK-11 | `GET /fund` | `recentTransactions` không có X4, X5, X6, X7; `info.managers` chỉ `{fullName, role}` của T1, T2, G1 – không email / điện thoại / id người dùng |
| LEAK-12 | `GET /fund/reports/yearly` | Chỉ số tổng theo tháng; không danh sách, không số lượng khoản ẩn |
| LEAK-13 | Quét toàn bộ response JSON của LEAK-01 → LEAK-12 | Không chứa chuỗi "Hoa và phong bì", "Nhập nhầm", email của bất kỳ ai, id người dùng khác P1 |
| LEAK-14 | B1 (chờ chốt): tháng chỉ có 1 khoản ẩn | Kỳ vọng phụ thuộc phương án PO chọn – bổ sung sau |

## 4. GVCN chỉ xem đầy đủ (VIEW)

| Mã | Tình huống | Kỳ vọng |
|---|---|---|
| VIEW-01 | G1 `GET /transactions?status=All` | Đủ X1 → X7, có `note`, `isPublic`, `createdBy`, `updatedBy`, `cancelledBy`, `cancelReason` |
| VIEW-02 | G1 `GET /transactions/X3/history` | 2 dòng: Tạo (T1), Sửa (T2) kèm giá trị trước / sau |
| VIEW-03 | G1 báo cáo tháng | Giống hệt báo cáo T1 |

## 5. Giao dịch, người thực hiện, hủy (TX)

| Mã | Tình huống | Kỳ vọng |
|---|---|---|
| TX-01 | T1 thêm khoản không gửi `isPublic` | `isPublic=true`, `CreatedBy=T1` |
| TX-02 | T2 sửa X1 | `UpdatedBy=T2`; `AuditLog` actor T2, old / new |
| TX-03 | T2 hủy X2 với lý do | `IsCancelled`, `CancelledBy=T2`, `CancelledAt`, lý do lưu; số dư +180.000 |
| TX-04 | Hủy với lý do rỗng / chỉ khoảng trắng / > 500 ký tự | 400; không đổi DB |
| TX-05 | Hủy lại X6 / sửa X6 | 409 / 422 `transaction.cancelled` |
| TX-06 | `DELETE FROM "FundTransaction"` bằng `app_user` | Bị trigger chặn (P0001) |
| TX-07 | Số dư sau mọi thao tác | = Σ thu − Σ chi chưa hủy; không tính X6, X7 |
| TX-08 | Unit: `FundTransaction.Cancel("")` | Ném `BusinessRuleException` |

## 6. Thành viên (MEM)

| Mã | Tình huống | Kỳ vọng |
|---|---|---|
| MEM-01 | T1 `POST /members` hợp lệ | 201 `{id, temporaryPassword}`; user `MustChangePassword=true`; `FundMember` quỹ A; `AuditLog Member.Create` **không** chứa mật khẩu |
| MEM-02 | Tạo với email đã có (kể cả khác hoa thường, kể cả thuộc quỹ B) | 409 `member.email_taken`; không lộ quỹ của email đó |
| MEM-03 | Mật khẩu tạm | ≥ 12 ký tự, khác nhau giữa 2 lần tạo, đăng nhập được, không xuất hiện trong log (CapturedLogSink) |
| MEM-04 | T1 `PUT /members/{G1}/role` → Parent | 200; `Role=Parent`, `UpdatedBy=T1`; `AuditLog Member.ChangeRole` old=HomeroomTeacher, new=Parent |
| MEM-05 | T1 đổi vai trò / khóa / đặt lại MK của chính T1 | 422 `member.self_action` |
| MEM-06 | Khóa T2, khi đó T1 là thủ quỹ hoạt động duy nhất; T1 thử tự hạ vai trò / tự khóa | 422 `member.self_action`; quỹ vẫn còn T1. CLI không có lệnh đổi vai trò |
| MEM-07 | T1 hạ T2 → Parent khi T1 và T2 cùng hoạt động | 200 (còn T1) |
| MEM-08 | Vai trò mới trùng vai trò hiện tại / giá trị lạ | 400 |
| MEM-09 | T1 đặt lại MK của P1 | 200 `{temporaryPassword}`; `MustChangePassword=true`; SecurityStamp đổi; phiên cũ của P1 → 401 |
| MEM-10 | T1 khóa P1 đang đăng nhập | Request kế tiếp của P1 → 401; đăng nhập lại đúng mật khẩu → 401 `auth.account_disabled`; sai mật khẩu → 401 `auth.invalid_credentials` |
| MEM-11 | T1 mở khóa P2 | P2 đăng nhập được |
| MEM-12 | `GET /members` | Chỉ thành viên quỹ A; `isSelf` đúng; không có `passwordHash`, `securityStamp` |
| MEM-13 | Không có endpoint xóa thành viên; `DELETE FROM "FundMember"` | 404/405; trigger chặn |

## 7. Phiên đăng nhập & mật khẩu (SES)

| Mã | Tình huống | Kỳ vọng |
|---|---|---|
| SES-01 | N1 đăng nhập bằng mật khẩu tạm | 200 `mustChangePassword=true` |
| SES-02 | N1 gọi `GET /fund`, `/transactions` | 403 `password.change_required` |
| SES-03 | N1 gọi `GET /auth/me`, `POST /auth/change-password`, `POST /auth/logout` | Được phép |
| SES-04 | N1 đổi mật khẩu (mới ≥ 8, khác tạm) | `MustChangePassword=false`; các API khác dùng được |
| SES-05 | T1 đổi mật khẩu ở thiết bị 1 | Phiên thiết bị 2 → 401 |
| SES-06 | Đăng nhập sai quá ngưỡng rate limit | 429 |
| SES-07 | POST không có `X-XSRF-TOKEN` | 400/403 antiforgery |
| SES-08 | Quét log toàn bộ test SES, MEM | Không có mật khẩu, mật khẩu tạm, hash, cookie |

## 8. Đồng thời (CONC)

| Mã | Tình huống | Kỳ vọng |
|---|---|---|
| CONC-01 | T1 và T2 cùng lúc hạ quyền nhau (2 request song song, chỉ có T1, T2 là thủ quỹ) | Đúng 1 thành công, 1 nhận 422 `member.last_treasurer` (sau retry 40001); quỹ còn đúng 1 thủ quỹ hoạt động |
| CONC-02 | T1 khóa T2 và T2 khóa T1 cùng lúc | Như CONC-01 |
| CONC-03 | T1 và T2 cùng hủy X1 | 1 thành công; 1 nhận 409; `CancelledBy` là người thắng; 1 dòng `AuditLog` Cancel |
| CONC-04 | T1 sửa X1 trong khi T2 hủy X1 | Kết quả cuối nhất quán: nếu hủy thắng thì sửa bị 409; nếu sửa trước thì hủy giữ giá trị đã sửa; số dư khớp |
| CONC-05 | T1 và T2 cùng thêm 20 khoản song song | Số dư = Σ đúng 40 khoản; không trùng; 40 dòng AuditLog |
| CONC-06 | T1 gửi 5 lần cùng `idempotencyKey` song song | 1 bản ghi; 4 response `replayed=true` |
| CONC-07 | T1 sửa X1 đồng thời T2 sửa X1 | Lần ghi sau thắng (DD-06); AuditLog có cả 2 dòng với old / new tương ứng |

## 9. Migration (MIG)

| Mã | Tình huống | Kỳ vọng |
|---|---|---|
| MIG-01 | Áp `AddFundMembers` lên DB có dữ liệu `InitialClassFund` (quỹ, giao dịch, có giao dịch đã hủy) | Mỗi quỹ có 1 `FundMember(Treasurer)` = OwnerUserId; `IsPublic=true` cho mọi giao dịch cũ; `MustChangePassword=false`; số dư mọi quỹ không đổi |
| MIG-02 | Giao dịch cũ đã sửa / hủy có AuditLog | `UpdatedBy` / `CancelledBy` được điền đúng actor; không có AuditLog → giữ null |
| MIG-03 | Thêm hủy mới thiếu `CancelledBy` (SQL trực tiếp) | Bị `CK_FundTransaction_CancelledBy` chặn; dữ liệu cũ null không bị báo lỗi |
| MIG-04 | `app_user` có quyền DML trên `FundMember`, không DDL | Đúng |
| MIG-05 | `DatabaseAccountTests` | Migration cuối là `_AddFundMembers` |

## 10. Truy vết yêu cầu

| Yêu cầu | Test |
|---|---|
| D-09 / BR-14, BR-15, NFR-08 | LEAK-01 → LEAK-13 |
| D-10 / BR-16, BR-17, BR-27 | MEM-01, MEM-03, SES-01 → SES-04, SES-08 |
| D-11 / BR-19, BR-22, NFR-09 | TX-02, TX-03, MEM-05 → MEM-07, CONC-01 → CONC-07 |
| D-12 | LEAK-09, LEAK-12 |
| D-16 (Q-11) / BR-21 | MEM-04, MEM-05, MEM-08, AUTHZ-12, CONC-01 |
| D-17 (Q-12) / BR-23 | LEAK-11 |
| D-18 (B2) | LEAK-02, LEAK-03, LEAK-04 (X7) |
| D-19 (B3) | TX-02, TX-03, MIG-02, MIG-03 |
| D-20 (B4) | `GET /fund` có `cumulative` và `currentMonth` (TX-07 kiểm số) |
| D-22 (B6) | LEAK-07, LEAK-08 |
| D-23 (B7) | MEM-10 |
| NFR-04 | AUTHZ-*, ISO-* |
| BR-18 | MEM-09, MEM-10, SES-05 |
