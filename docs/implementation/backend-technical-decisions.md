# Backend Technical Decisions

> 2026-11-05: mã nguồn đã xóa (D-35). Các TD dưới đây là quyết định kỹ thuật dùng cho lần triển khai lại; tên lớp / file chỉ là tham chiếu lịch sử. Trạng thái: **Not Implemented** ([implementation-status.md](implementation-status.md)).

> Cập nhật 2026-11-04 (sau Review & Simplify). Quyết định kỹ thuật của phạm vi cũ (TD-40 → TD-55, DV-01 → DV-07) không còn áp dụng – Git history.

## Giữ từ M0
| Mã | Quyết định |
|---|---|
| TD-01 → TD-31 | Cấu trúc solution, build props, dependency rule (có test), ProblemDetails, validation ở Api (FluentValidation), Correlation ID, Serilog, versioning URL, Swagger (Development), health check, 2 tài khoản DB, Testcontainers |

## Quyết định hiện hành
| Mã | Quyết định | Lý do |
|---|---|---|
| TD-60 | Domain chỉ còn `User`, `Fund`, `Category`, `FundTransaction`, `AuditLog`; một bảng giao dịch có cột `Type` | Bài toán thủ quỹ một lớp |
| TD-61 | Số dư và mọi số liệu báo cáo tính bằng truy vấn tổng trên giao dịch chưa hủy; không bảng tổng hợp, không khóa dòng | Không thể lệch; đủ nhanh cho quy mô một lớp |
| TD-62 | Hủy = cờ `IsCancelled`; trigger DB chặn DELETE; nhật ký `AuditLog` append-only lưu giá trị trước / sau | Không mất dữ liệu, truy vết sửa / hủy |
| TD-63 | FK ghép (CategoryId, FundId, Type) → Category | DB chặn danh mục sai loại / sai quỹ |
| TD-64 | `idempotencyKey` bắt buộc khi thêm; trùng (tuần tự hoặc đồng thời) → trả giao dịch đã lưu (200, `replayed`) | Nhấn Lưu nhiều lần |
| TD-65 | Phạm vi dữ liệu qua `FundContext` (quỹ của người đăng nhập); client không gửi mã quỹ; quỹ khác → 404 | Không IDOR |
| TD-66 | `[RequirePermission]` + 5 permission cố định trong code cho thủ quỹ; không bảng vai trò | Giữ chuẩn training-doc, không thêm khái niệm |
| TD-67 | Tài khoản tạo / đặt lại mật khẩu / khóa bằng lệnh `dotnet run -- <lệnh>` đọc biến môi trường | D-02; không lộ mật khẩu trong source |
| TD-68 | Cookie session + antiforgery + rate limit đăng nhập (giữ) | Bảo mật cơ bản |
| TD-69 | Một migration baseline `InitialClassFund`; 14 migration cũ (chưa phát hành) đã xóa | D-08 |
| TD-70 | Giữ port `IAppDb` (IQueryable + thực thi async) nhưng bỏ khóa dòng | Application không phụ thuộc EF (dependency rule M0) |

## Dự kiến cho R2 (đã thiết kế, CHƯA triển khai – 2026-11-04)
| Mã | Quyết định | Thay thế |
|---|---|---|
| TD-71 | `FundMember` (UserId UNIQUE, Role) + `RolePermissions` trong Domain; `CurrentMember` scoped đọc vai trò từ DB mỗi request | TD-65 (`FundContext` theo owner), TD-66 (5 permission cố định) |
| TD-72 | 7 permission: Fund.View, Ledger.ViewFull, Ledger.ViewPublic, Transaction.Manage, Category.Manage, Fund.Manage, Member.Manage; `[RequirePermission]` hỗ trợ "một trong các" | TD-66 |
| TD-73 | Phạm vi công khai áp trong truy vấn Application (projection `PublicTransactionDto`), lọc trước phân trang / đếm | – |
| TD-74 | `MustChangePassword` → 403 `password.change_required` trừ me / change-password / logout | – |
| TD-75 | Thao tác thành viên (đổi vai trò, khóa) chạy SERIALIZABLE, đếm thủ quỹ hoạt động trong cùng giao dịch; retry 40001 đã có | – |
| TD-76 | `UpdatedBy` / `CancelledBy` cột nullable + CHECK `NOT VALID`; backfill từ AuditLog trong migration `AddFundMembers` | – |
| TD-77 | Mật khẩu tạm sinh bằng `RandomNumberGenerator`, chỉ trả trong response; không log body `/members*`, `/auth/*` | TD-67 vẫn giữ cho CLI |

TD-65, TD-66 giữ nguyên trong bảng trên để lưu lịch sử. **2026-11-05:** TD-71 → TD-77 (R2) **không triển khai** – thay bằng TD-80 → TD-88 (R3). TD-65, TD-66 thay bởi TD-81, TD-82.

| Mã | Quyết định (R3, 2026-11-05 – thiết kế kỹ thuật hiện hành; **Not Implemented**: mã nguồn đã xóa cùng ngày) | Thay |
|---|---|---|
| TD-80 | `User.Kind` (Teacher / Student / Parent) cố định; học sinh đăng nhập bằng `Username`, giáo viên / phụ huynh bằng email; `POST /auth/login { login }` | – |
| TD-81 | `AccessService` (scoped, nhớ trong request) suy ra vai trò trong quỹ từ `Fund.OwnerUserId` → `Student` + `TreasurerAssignment` → `Student` → `ParentLink`; `RolePermissions` (Domain) ánh xạ vai trò → permission | TD-65, TD-71 |
| TD-82 | 10 permission (`Fund.Create` cấp tài khoản; 9 permission theo quỹ); `PermissionAuthorizationHandler` đọc route `fundId`, ném `NotFoundException` / `ForbiddenAccessException(code)` → ProblemDetails qua `GlobalExceptionHandler`; service kiểm tra lại | TD-66, TD-72 |
| TD-83 | API theo quỹ `/funds/{fundId}/…`; bỏ `/fund`, `/transactions`, `/categories` (không có consumer) | – |
| TD-84 | Bản công khai: một `TransactionRow` + `ToDto(full)`; trường nội bộ null + `JsonIgnore(WhenWritingNull)`; lọc `IsPublic` trong `Visible(scope)` trước mọi điều kiện | TD-73 |
| TD-85 | Thu hồi / phân công thủ quỹ: `IAppDb.LockAsync<Fund>` (`SELECT … FOR UPDATE`) trong `InTransactionAsync` (Read Committed + retry); UNIQUE lọc chống phân công trùng | TD-75 |
| TD-86 | `Credentials.TemporaryPassword()` – `RandomNumberGenerator`, 16 ký tự không gây nhầm; `SuggestUsername` bỏ dấu; response có `Cache-Control: no-store`; AuditLog không chứa mật khẩu; request logging không ghi body | TD-77 |
| TD-87 | Migration `TeacherStudentParent` bổ sung: default `Kind = 'Teacher'` rồi DROP DEFAULT; backfill `UpdatedBy` / `CancelledBy` từ AuditLog (`DISTINCT ON … ORDER BY OccurredAt DESC`); NoDelete 3 bảng mới; không tạo phân công | TD-76 |
| TD-88 | CLI `create-teacher` (quỹ tùy chọn) thay `create-treasurer`; `SCHOOLFUND_LOGIN` (fallback `SCHOOLFUND_EMAIL`) | TD-67 (một phần) |

## Đã loại bỏ
Package: không thêm / bớt package NuGet. Mã: email sender, file storage, permission evaluator theo phạm vi, mọi service nghiệp vụ cũ.
